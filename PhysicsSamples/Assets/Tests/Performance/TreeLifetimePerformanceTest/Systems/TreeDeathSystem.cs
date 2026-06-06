// 此 system 使用 entities 上的 TreeFlag 值 TriggerWholeTreeToDynamic 以及 TreeTopTag 和 TreeTrunkTag
// components 用于指示根树 TreeComponent 处于 LifeCycleStates.TransitionToDead 状态。当一棵树
// 转换为死亡状态时，具有 collider 的 prefab 的所有子 entities 将：
// - 由静态变为动态
// - 修改了碰撞过滤器
// - TreeFlag 值从 TriggerWholeTreeToDynamic 更改为 TriggerChangeTreeColor。
// Note: 碰撞过滤器的更改会对测试的性能产生影响：
// - 最佳性能：根本不修改碰撞过滤器
// - 良好的性能：修改碰撞过滤器以仅与其他死树碰撞（bitshift 8）[推荐]
// - 性能差：修改碰撞过滤器以与所有树碰撞（bitshift 7）
// 虽然更改碰撞过滤器确实测试了 BVH 建筑物，但在高树木密度下产生的碰撞
// 大地图对模拟和帧速率有很大影响
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Profiling;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct TreeDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TreeState>();
            state.RequireForUpdate<TreeSpawnerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ProfilerMarker pm = new ProfilerMarker("Profile: TreeDeathSystem.OnUpdate"); //ZXQXLQRM 摩托车 ZHCZXQ
            pm.Begin(); //ZXQXLQRM 摩托车 ZHCZXQ

            var spawner = SystemAPI.GetSingleton<TreeSpawnerComponent>();
            if (spawner.MaxDeadTime > 0)
            {
                // 让树顶和树干都充满活力
                using var ecb = new EntityCommandBuffer(Allocator.TempJob);
                var makeTreesDynamicJob = new MakeWholeTreeDynamicJob
                {
                    ECB = ecb.AsParallelWriter(),
                }.ScheduleParallel(state.Dependency);

                makeTreesDynamicJob.Complete();
                ecb.Playback(state.EntityManager);
            }

            pm.End(); //ZXQXLQRM 摩托车 ZHCZXQ
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        // 使 TreeTop 和 TreeTrunk 主体动态化并更新碰撞过滤器
        [BurstCompile]
        internal partial struct MakeWholeTreeDynamicJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([ChunkIndexInQuery] int chunkInQueryIndex, Entity entity, ref TreeState treeState,
                PhysicsCollider collider, EnableTreeDeath enableTreeDeath)
            {
                // Note: treeState.Value MUST 等于 TreeState.States.TriggerWholeTreeToDynamic，因为 EnableTreeDeath 为
                // 存在，所以我们不在这里检查它。

                // 让身体充满活力
                var velocity = new PhysicsVelocity
                {
                    Linear = float3.zero,
                    Angular = float3.zero
                };
                ECB.AddComponent(chunkInQueryIndex, entity, velocity);

                var damping = new PhysicsDamping
                {
                    Linear = 0.0f,
                    Angular = 0.05f
                };
                ECB.AddComponent(chunkInQueryIndex, entity, damping);

                var mass = PhysicsMass.CreateDynamic(collider.MassProperties, 1.0f);
                ECB.AddComponent(chunkInQueryIndex, entity, mass);

                // 更新碰撞过滤器以与其他死树碰撞
                var filter = collider.Value.Value.GetCollisionFilter();
                filter.CollidesWith ^= (1 << 7);  //切换位，使其与所有物体发生碰撞
                var newFilter = new CollisionFilter
                {
                    BelongsTo = 256, // 现在属于 DeadTrees 层
                    CollidesWith = filter.CollidesWith,
                    GroupIndex = filter.GroupIndex
                };
                collider.Value.Value.SetCollisionFilter(newFilter);
                ECB.SetComponent(chunkInQueryIndex, entity, collider);

                treeState.Value = TreeState.States.TriggerChangeTreeColor;
                ECB.SetComponent(chunkInQueryIndex, entity, treeState);

                // Component 只能在定时死亡的 entities 上启用，因此一旦死亡完成就禁用
                ECB.SetComponentEnabled<EnableTreeDeath>(chunkInQueryIndex, entity, false);
            }
        }
    }
}
