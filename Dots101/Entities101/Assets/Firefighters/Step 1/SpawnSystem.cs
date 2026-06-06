using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Tutorials.Firefighters
{
    public partial struct SpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            var config = SystemAPI.GetSingleton<Config>();
            var rand = new Random(123);

            var bucketEntities = new NativeArray<Entity>(config.NumBuckets, Allocator.Temp);

            // 产卵桶
            {
                // struct components 返回并按值传递（作为副本）！
                var bucketTransform = state.EntityManager.GetComponentData<LocalTransform>(config.BucketPrefab);
                bucketTransform.Position.y = (bucketTransform.Scale / 2); // 每个桶都相同

                for (int i = 0; i < config.NumBuckets; i++)
                {
                    var bucketEntity = state.EntityManager.Instantiate(config.BucketPrefab);
                    bucketEntities[i] = bucketEntity;

                    bucketTransform.Position.x = rand.NextFloat(0.5f, config.GroundNumColumns - 0.5f);
                    bucketTransform.Position.z = rand.NextFloat(0.5f, config.GroundNumRows - 0.5f);
                    bucketTransform.Scale = config.BucketEmptyScale;

                    state.EntityManager.SetComponentData(bucketEntity, bucketTransform);
                }
            }

            // 产卵队
            {
                int numBotsPerTeam = config.NumPassersPerTeam + 1;
                int douserIdx = (config.NumPassersPerTeam / 2);
                for (int teamIdx = 0; teamIdx < config.NumTeams; teamIdx++)
                {
                    var teamEntity = state.EntityManager.CreateEntity();
                    var team = new Team
                    {
                        Bucket = bucketEntities[teamIdx]
                    };
                    state.EntityManager.AddComponent<RepositionLine>(teamEntity);
                    var memberBuffer = state.EntityManager.AddBuffer<TeamMember>(teamEntity);
                    memberBuffer.Capacity = numBotsPerTeam;
                    var teamColor = new float4(rand.NextFloat3(), 1);

                    // 生成机器人
                    for (int botIdx = 0; botIdx < numBotsPerTeam; botIdx++)
                    {
                        var botEntity = state.EntityManager.Instantiate(config.BotPrefab);

                        var x = rand.NextFloat(0.5f, config.GroundNumColumns - 0.5f);
                        var z = rand.NextFloat(0.5f, config.GroundNumRows - 0.5f);

                        state.EntityManager.SetComponentData(botEntity, LocalTransform.FromPosition(x, 1, z));
                        state.EntityManager.SetComponentData(botEntity, new URPMaterialPropertyBaseColor
                        {
                            Value = teamColor
                        });

                        // 指定填充物
                        if (botIdx == 0)
                        {
                            team.Filler = botEntity;
                        }

                        memberBuffer.Add(new TeamMember { Bot = botEntity });
                    }

                    // 将每个机器人与队列中的下一个机器人连接起来，形成一个传递环
                    for (int botIdx = 0; botIdx < memberBuffer.Length; botIdx++)
                    {
                        Entity nextBot;
                        if (botIdx == memberBuffer.Length - 1)
                        {
                            nextBot = memberBuffer[0].Bot;   // 接下来是填充物
                        }
                        else
                        {
                            nextBot = memberBuffer[botIdx + 1].Bot;
                        }

                        state.EntityManager.SetComponentData(memberBuffer[botIdx].Bot, new Bot
                        {
                            NextBot = nextBot,
                            Team = teamEntity,
                            IsFiller = (botIdx == 0),
                            IsDouser = (botIdx == douserIdx),
                        });
                    }

                    state.EntityManager.AddComponentData(teamEntity, team);
                }
            }

            // 产卵池
            {
                var bounds = new NativeArray<float4>(4, Allocator.Temp);

                const float innerMargin = 2; // 地面边缘和池塘区域之间的边缘
                const float outerMargin = innerMargin + 3;
                float width = config.GroundNumColumns;
                float height = config.GroundNumRows;

                // 地面细胞区域周围的 4 个侧面
                // x、y 为左下角；z,w 是右上角
                bounds[0] = new float4(0.5f, -outerMargin, width - 0.5f, -innerMargin); // 底部
                bounds[1] = new float4(0.5f, height + innerMargin, width - 0.5f, height + outerMargin); // 顶部
                bounds[2] = new float4(-outerMargin, 0.5f, -innerMargin, height - 0.5f); // 左边
                bounds[3] = new float4(width + innerMargin, 0.5f, width + outerMargin, height - 0.5f); // 正确的

                var pondTransform = state.EntityManager.GetComponentData<LocalTransform>(config.PondPrefab);
                for (int i = 0; i < 4; i++)
                {
                    var bottomLeft = bounds[i].xy;
                    var topRight = bounds[i].zw;

                    for (int j = 0; j < config.NumPondsPerEdge; j++)
                    {
                        var pondEntity = state.EntityManager.Instantiate(config.PondPrefab);

                        var pos = rand.NextFloat2(bottomLeft, topRight);
                        pondTransform.Position = new float3(pos.x, 0, pos.y);
                        state.EntityManager.SetComponentData(pondEntity, pondTransform);
                    }
                }
            }

            // 产卵场
            {
                var groundCellTransform = state.EntityManager.GetComponentData<LocalTransform>(config.GroundCellPrefab);
                groundCellTransform.Position.y = -(config.GroundCellYScale / 2);

                for (int column = 0; column < config.GroundNumColumns; column++)
                {
                    for (int row = 0; row < config.GroundNumRows; row++)
                    {
                        var groundCellEntity = state.EntityManager.Instantiate(config.GroundCellPrefab);
                        groundCellTransform.Position.x = column + 0.5f;
                        groundCellTransform.Position.z = row + 0.5f;
                        state.EntityManager.SetComponentData(groundCellEntity, groundCellTransform);
                        state.EntityManager.SetComponentData(groundCellEntity, new URPMaterialPropertyBaseColor
                        {
                            Value = config.MinHeatColor
                        });
                    }
                }
            }

            // 生成热图
            {
                var entity = state.EntityManager.CreateEntity();
                var heatBuffer = state.EntityManager.AddBuffer<Heat>(entity);

                // 初始化热缓冲区
                {
                    heatBuffer.Length = config.GroundNumColumns * config.GroundNumRows;
                    // 将每个单元格设置为零
                    for (int i = 0; i < heatBuffer.Length; i++)
                    {
                        heatBuffer[i] = new Heat { Value = 0f };
                    }
                }

                // 随机点燃一些细胞
                {
                    for (int i = 0; i < config.NumInitialCellsOnFire; i++)
                    {
                        var randomIdx = rand.NextInt(0, heatBuffer.Length);
                        heatBuffer[randomIdx] = new Heat { Value = 1f };
                    }
                }

                // 移动地面单元，使其 query 迭代顺序对应于热缓冲区的索引。
                // （只要 query 匹配的 entities 集合保持不变，则 query 迭代顺序将保持不变。
                // 因此，这将使根据热量数据更新地面单元的颜色和高度变得容易/快速。）
                {
                    var x = 0;
                    var z = 0;

                    foreach (var trans in
                             SystemAPI.Query<RefRW<LocalTransform>>()
                                 .WithAll<GroundCell>())
                    {
                        trans.ValueRW.Position.x = x + 0.5f;
                        trans.ValueRW.Position.z = z + 0.5f;

                        x++;
                        if (x >= config.GroundNumColumns)
                        {
                            x = 0;
                            z++;
                        }
                    }
                }
            }
        }
    }
}
