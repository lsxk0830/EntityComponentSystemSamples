using Tutorials.Kickball.Execute;
using Tutorials.Kickball.Step1;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tutorials.Kickball.Step2
{
    [UpdateAfter(typeof(ObstacleSpawnerSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct PlayerSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSpawner>();
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 我们只想在一帧中生成玩家。禁用 system 会阻止其在这一次之后再次更新。
            state.Enabled = false;

            var config = SystemAPI.GetSingleton<Config>();

            #if true
                // 更高级别 API
                // 这个“foreach query”由 source-gen 转换为类似于下面的 #else 的代码。
                // 对于每个具有 LocalTransform 和障碍物 component 的 entity，只读引用
                // LocalTransform 被分配给“obstacleTransform”。
                foreach (var obstacleTransform in
                         SystemAPI.Query<RefRO<LocalTransform>>().
                             WithAll<Obstacle>())
                {
                    // 从 prefab 创建玩家 entity。
                    var player = state.EntityManager.Instantiate(config.PlayerPrefab);

                    // 设置新玩家的变换（偏离障碍物的位置）。
                    state.EntityManager.SetComponentData(player, new LocalTransform
                    {
                        Position = new float3
                        {
                            x = obstacleTransform.ValueRO.Position.x + config.PlayerOffset,
                            y = 1,
                            z = obstacleTransform.ValueRO.Position.z + config.PlayerOffset
                        },
                        Scale = 1,  // 如果我们没有设置缩放和旋转，它们将默认为零（这很糟糕！）
                        Rotation = quaternion.identity
                    });
                }
            #else
                // 下层 API
                // 获取与所有 entities 匹配的 query，其中 LocalTransform 和障碍物 component。
                var query = SystemAPI.QueryBuilder().WithAll<LocalTransform, Obstacle>().Build();

                // 需要类型句柄来从 chunks 访问 component 数据数组。
                var localTransformTypeHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(true);

                // 执行 query：返回所有 chunks，其中 entities 与 query 匹配。
                var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
                foreach (var chunk in chunks)
                {
                    // 使用 LocalTransform 类型句柄从 chunk 获取 LocalTransform component 数据数组。
                    // 请注意，这不是副本！这是存储在 chunk 中的实际 component 数组，因此
                    // 修改其内容直接修改了 entities 的 LocalTransform 值。
                    // 因为该数组属于 chunk，所以您不需要（也不应该）处置它。
                    var localTransforms = chunk.GetNativeArray(ref localTransformTypeHandle);

                    // 迭代 chunk 中的每个 entity。
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        // 直接从 component 数据数组中读取 component 值。
                        var obstacleTransform = localTransforms[i];

                        // 与上面相同的播放器实例化代码。
                        var player = state.EntityManager.Instantiate(config.PlayerPrefab);
                        state.EntityManager.SetComponentData(player, new LocalTransform
                        {
                            Position = new float3
                            {
                                x = obstacleTransform.Position.x + config.PlayerOffset,
                                y = 1,
                                z = obstacleTransform.Position.z + config.PlayerOffset
                            },
                            Scale = 1,
                            Rotation = quaternion.identity
                        });
                    }
                }
            #endif
        }
    }
}
