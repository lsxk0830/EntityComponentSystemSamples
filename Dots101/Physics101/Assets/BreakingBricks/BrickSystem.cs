using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace BreakingBricks
{
    // system 在碰撞检测和求解器的每次迭代后运行
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct BrickSystem : ISystem
    {
        private bool hasSpawnedBricks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<BreakingBricks.Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            // 生成砖块
            if (!hasSpawnedBricks)
            {
                hasSpawnedBricks = true;

                state.EntityManager.Instantiate(config.BrickPrefab, config.NumBricksSpawn, Allocator.Temp);

                var rand = new Random(123);

                foreach (var (brickTransform, color, collider) in
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<URPMaterialPropertyBaseColor>,
                                 RefRW<PhysicsCollider>>()
                             .WithAll<Brick>())
                {
                    var pos = rand.NextFloat3(config.SpawnBoundsMin, config.SpawnBoundsMax);
                    brickTransform.ValueRW.Position = pos;

                    color.ValueRW.Value = config.FullHitpointsColor;
                }
            }

            // 需要获取碰撞事件
            var sim = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();

            // 要访问主线程上的碰撞事件，我们必须同步任何出色的物理模拟 jobs
            sim.FinalJobHandle.Complete();

            // 需要获取碰撞事件的详细信息（估计脉冲）
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            const float minImpactThreshold = 2f; // 忽略低于此的影响（以有效忽略静止接触）
            var strengthModifier = config.ImpactStrength;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var collisionEvent in sim.CollisionEvents)
            {
                // 检查两个物体中的一个是否是砖块，另一个是否是球
                Entity brickEntity;
                Entity ballEntity;

                // Note 在一次物理更新中，一对之间的碰撞
                // 物体的数量会产生一次碰撞事件，而不是两次。
                // API 不保证哪个实体是 EntityA，哪个实体是 EntityB，
                // 所以你必须测试这两种可能性。

                if (SystemAPI.HasComponent<Brick>(collisionEvent.EntityA) &&
                    SystemAPI.HasComponent<Ball>(collisionEvent.EntityB))
                {
                    brickEntity = collisionEvent.EntityA;
                    ballEntity = collisionEvent.EntityB;
                }
                else if (SystemAPI.HasComponent<Brick>(collisionEvent.EntityB) &&
                         SystemAPI.HasComponent<Ball>(collisionEvent.EntityA))
                {
                    brickEntity = collisionEvent.EntityB;
                    ballEntity = collisionEvent.EntityA;
                }
                else
                {
                    continue;
                }

                var details = collisionEvent.CalculateDetails(ref physicsWorld);

                // 忽略休息时的接触
                if (details.EstimatedImpulse < minImpactThreshold)
                {
                    continue;
                }

                // 减少砖块生命值
                var brick = SystemAPI.GetComponentRW<Brick>(brickEntity);
                brick.ValueRW.Hitpoints -= strengthModifier * details.EstimatedImpulse;

                // 如果生命值低于 0，则摧毁砖块
                if (brick.ValueRO.Hitpoints <= 0)
                {
                    ecb.DestroyEntity(brickEntity);
                }
                else
                {
                    // 更新被击中的砖块的颜色
                    var color = SystemAPI.GetComponentRW<URPMaterialPropertyBaseColor>(brickEntity);
                    color.ValueRW.Value = math.lerp(config.EmptyHitpointsColor, config.FullHitpointsColor,
                        brick.ValueRO.Hitpoints);
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}