//#定义 DEBUG_TREE_GROW_LOGGING

// 此 system 使用 TreeComponent（又名：树根 / prefab）迭代所有 entities。TreeComponent
// 倒计时计时器，跟踪树在其生命周期中的位置，树从以下位置转换：
// 生长>死亡>删除>重新生长，然后重复循环。system 对定时器进行倒计时。之间的过渡
// 生命周期状态在其他 systems 中完成。这项工作一般需要在 prefab 的子 entities 上完成
// LinkedEntityGroup，因此这个 system 将每个 entities 上的 TreeFlag 设置为 trigger 在其他 systems 中的工作。
// 生命周期状态更改为倒计时下一个计时器通常是在其他 systems 中完成的，这样我们就可以确定
// 工作完成了。所有修改的数据都写入运行的 EndFixedStepSimulationEntityCommandBufferSystem
// 在 PhysicsSystemGroup 之后。这意味着设置的某些数据不会被另一个 system 处理，直到
// 下一帧
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TreeSpawnerSystem))]
    [UpdateAfter(typeof(TreeRegrowSystem))]
    public partial struct TreeLifecycleSystem : ISystem
    {
#if DEBUG_TREE_GROW_LOGGING
        UnsafeAtomicCounter32 m_GrowingTreesCounter;
        int m_GrowingTreesCount;
#endif

        [BurstCompile]
        private partial struct LifecycleCountdownJob : IJobEntity
        {
            [NativeDisableUnsafePtrRestriction]
#if DEBUG_TREE_GROW_LOGGING
            public UnsafeAtomicCounter32 GrowingTreesCounter;
#endif
            [ReadOnly] public ComponentLookup<TreeState> TreeStateLookup;
            [ReadOnly] public ComponentLookup<TreeTopTag> TreeTopTagLookup;
            public EntityCommandBuffer.ParallelWriter ECB;
            public float GrowTreeProbability;

            // 收集所有 entities 和 TreeComponent （这些仅是 prefab 树根）
            private void Execute([ChunkIndexInQuery] int chunkInQueryIndex, Entity entity,
                ref TreeComponent treeComponent, in DynamicBuffer<LinkedEntityGroup> group)
            {
                var rootTreeState = TreeStateLookup[entity];

                switch (treeComponent.LifeCycleTracker)
                {
                    default:
                    case (LifeCycleStates.IsGrowing): // 倒计时 GrowTimer
                        treeComponent.GrowTimer--;

                        // 掷骰子看看我们是否能种植这棵树
                        var treeHash = entity.Index * 17 ^ entity.Version * 23;
                        var hash = (uint)treeComponent.GrowTimer * 327 ^ (uint)treeComponent.GrowTime * 1571;
                        var seed = math.max(1, hash ^ (uint)treeHash);
                        var random = new Random(seed);
                        var p = random.NextFloat(1.0f);

                        if (p <= GrowTreeProbability)
                        {
#if DEBUG_TREE_GROW_LOGGING
                            GrowingTreesCounter.Add(1);
#endif

                            if (group.Length > 1)
                            {
                                // 循环遍历 prefab LinkedEntityGroup entities
                                for (var i = 1; i < group.Length; i++) //跳过根目录
                                {
                                    var childEntity = group[i].Value;

                                    // 检查 entity 是否是树顶
                                    var isTreeTop = TreeTopTagLookup.HasComponent(childEntity);
                                    var childTreeState = TreeStateLookup[childEntity];

                                    // 对于树顶，从“默认”>“TriggerTreeGrowthSystem”切换标志
                                    if (isTreeTop && childTreeState.Value == TreeState.States.Default)
                                    {
                                        childTreeState.Value = TreeState.States.TriggerTreeGrowthSystem;
                                        ECB.SetComponent(chunkInQueryIndex, childEntity, childTreeState);

                                        // 增量宽相需要较小的 entities 集合才能使用，
                                        // 因此添加启用 EnableTreeGrowth component 进行 query 过滤
                                        ECB.SetComponentEnabled<EnableTreeGrowth>(chunkInQueryIndex, childEntity,
                                            true);
                                    }
                                }
                            }
                        }

                        // 定时器到期时更改状态
                        if (treeComponent.GrowTimer <= 0) treeComponent.LifeCycleTracker = LifeCycleStates.TransitionToDead;
                        break;

                    case (LifeCycleStates.TransitionToDead):
                        // 工作在 TreeDeathSystem 中完成
                        if (treeComponent.GrowTimer <= 0) // 验证生长计时器是否已过期
                        {
                            if (group.Length > 1)
                            {
                                bool allWorkDone = true;
                                // 循环遍历 prefab LinkedEntityGroup entities
                                for (var i = 1; i < group.Length; i++) //root 后启动
                                {
                                    var childEntity = group[i].Value;

                                    // 更新所有孩子的标志
                                    var childTreeState = TreeStateLookup[childEntity];
                                    switch (childTreeState.Value)
                                    {
                                        case TreeState.States.Default:
                                        {
                                            // 如果花费的时间超过一帧，则不想覆盖
                                            childTreeState.Value = TreeState.States.TriggerWholeTreeToDynamic;
                                            ECB.SetComponent(chunkInQueryIndex, childEntity, childTreeState);

                                            // 增量宽相需要较小的 entities 集合才能使用，
                                            // 因此添加启用 EnableTreeDeath component 进行 query 过滤
                                            ECB.SetComponentEnabled<EnableTreeDeath>(chunkInQueryIndex, childEntity, true);
                                            break;
                                        }
                                        case TreeState.States.TransitionToDeadDone:
                                        {
                                            // 当 TreeDeathSystem 完成后，我们将到达这里
                                            allWorkDone &= (childTreeState.Value == TreeState.States.TriggerChangeTreeColor);
                                            break;
                                        }
                                        default:
                                            break;
                                    }
                                }
                                // 仅当所有工作完成后才转换到下一个状态
                                if (allWorkDone)
                                {
                                    // 如果死时间为 0，则跳过“死”状态并直接删除
                                    treeComponent.LifeCycleTracker = treeComponent.DeadTime > 0 ? LifeCycleStates.IsDead : LifeCycleStates.TransitionToDelete;
                                }
                            }
                        }

                        break;

                    case (LifeCycleStates.IsDead): // 倒计时 DeathTimer
                        treeComponent.DeathTimer--;

                        // 除了倒计时器并在计时器到期时更改状态之外什么都不做
                        if (treeComponent.DeathTimer <= 0) treeComponent.LifeCycleTracker = LifeCycleStates.TransitionToDelete;
                        break;

                    case (LifeCycleStates.TransitionToDelete):
                        // 过渡：删除 LinkedEntityGroup 内的所有 entities
                        // TreeDeletionSystem 直接使用此生命周期状态。什么都不做。
                        if (rootTreeState.Value != TreeState.States.TriggerDeleteTrunkAndTop) // 以防万一这会滑倒框架
                        {
                            rootTreeState.Value = TreeState.States.TriggerDeleteTrunkAndTop;
                            ECB.SetComponent(chunkInQueryIndex, entity, rootTreeState);
                        }

                        // 转换到 TreeDeletionSystem 中的下一个状态
                        break;

                    case (LifeCycleStates.IsRegrown):
                        // 倒计时：树重生之前的时间
                        treeComponent.RegrowTimer--;

                        if (treeComponent.RegrowTimer <= 0) treeComponent.LifeCycleTracker = LifeCycleStates.TransitionToInsert;
                        break;

                    case (LifeCycleStates.TransitionToInsert):
                        // 过渡：重生树
                        // 工作在 TreeRegrowSystem 中完成
                        break;
                }
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TreeComponent>();
            state.RequireForUpdate<TreeSpawnerComponent>();
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();

#if DEBUG_TREE_GROW_LOGGING
            unsafe
            {
                fixed(int* countPtr = &m_GrowingTreesCount)
                {
                    m_GrowingTreesCounter = new UnsafeAtomicCounter32(countPtr);
                }
            }
#endif
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var treeStateLookup = SystemAPI.GetComponentLookup<TreeState>(isReadOnly: true);
            var treeTopTagLookup = SystemAPI.GetComponentLookup<TreeTopTag>(isReadOnly: true);

#if DEBUG_TREE_GROW_LOGGING
            UnityEngine.Debug.Log($"TreeLifecycleSystem.OnUpdate: Growing trees count: {m_GrowingTreesCount}");
            m_GrowingTreesCount = 0;
#endif

            var spawner = SystemAPI.GetSingleton<TreeSpawnerComponent>();

            // 计时器倒计时
            state.Dependency = new LifecycleCountdownJob
            {
#if DEBUG_TREE_GROW_LOGGING
                GrowingTreesCounter = m_GrowingTreesCounter,
#endif
                TreeStateLookup = treeStateLookup,
                TreeTopTagLookup = treeTopTagLookup,
                ECB = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                GrowTreeProbability = spawner.TreeGrowProbability
            }.ScheduleParallel(state.Dependency);
        }
    }
}
