// 此 system 使用 TreeComponent 和 TreeFlag components （树根 / prefab）迭代整体 entities 和
// 仅当状态等于 LifeCycleStates.TransitionToDelete 时运行。prefab 中的每个子项 entity
// LinkedEntityGroup 被破坏。树根依然存在。删除所有子 entities 后，TreeRootState
// 设置为默认（如果 system 没有立即设置 run，则不会再次设置该状态），并且生命周期
// 状态设置为 LifeCycleStates.IsRegrown
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Profiling;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateAfter(typeof(TreeDeathSystem))]
    public partial struct TreeDeletionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TreeComponent>();
            state.RequireForUpdate<TreeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ProfilerMarker pm = new ProfilerMarker("Profile: TreeDeletionSystem.OnUpdate"); //ZXQXLQRM 摩托车 ZHCZXQ
            pm.Begin(); //ZXQXLQRM 摩托车 ZHCZXQ

            using var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var deleteHandle = new DeleteTreeJob()
            {
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);
            deleteHandle.Complete();

            ecb.Playback(state.EntityManager);

            pm.End(); //ZXQXLQRM 摩托车 ZHCZXQ
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public partial struct DeleteTreeJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([ChunkIndexInQuery] int chunkInQueryIndex, Entity entity, ref TreeState treeRootState,
                ref TreeComponent treeComponent, ref DynamicBuffer<LinkedEntityGroup> group)
            {
                if (treeComponent.LifeCycleTracker != LifeCycleStates.TransitionToDelete)
                    return;

                // 删除子 entities
                for (var j = 1; j < group.Length; j++)
                {
                    var childEntity = group[j].Value;
                    ECB.DestroyEntity(chunkInQueryIndex, childEntity);
                }

                // 清除 LinkedEntityGroup 以删除已删除的条目
                // Note: 0 是根 entity，我们也可以安全地删除它，因为我们删除了所有子项
                // 并且不再需要 LinkedEntityGroup。
                group.Clear();

                // 更新，因此不再在 TreeLifetimeSystem 中标记为删除
                treeRootState.Value = TreeState.States.Default;

                // 将 TreeComponent 更新到下一个状态
                treeComponent.LifeCycleTracker = LifeCycleStates.IsRegrown;
            }
        }
    }
}
