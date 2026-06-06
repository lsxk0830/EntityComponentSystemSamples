// 此 system 使用 TreeTopTag 迭代 entities （注意：主干不会增长），并且仅在 TreeFlag 时运行
// 值等于 FlagSettings.TriggerTreeGrowthSystem。collider 大小增加，当增长完成时，
// treeState 设置为默认值。TreeLifecycleSystem 负责切换 TreeFlag，从而启用此 system。
// Note 正在修改的 entities 是静态体。
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Profiling;
using Unity.Transforms;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct TreeGrowthSystem : ISystem
    {
        private static readonly float growthRate = 0.2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TreeState>();
            state.RequireForUpdate<EnableTreeGrowth>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ProfilerMarker pm = new ProfilerMarker("Profile: TreeGrowthSystem.OnUpdate"); //ZXQXLQRM 摩托车 ZHCZXQ
            pm.Begin(); //ZXQXLQRM 摩托车 ZHCZXQ

            // run 的条件：具有 collider、TreeGrowthTag、treeState = TriggerTreeGrowth
            using var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new GrowTreeColliderJob
            {
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            // 树木生长完成后，移除 TreeGrowthTag component
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);

            pm.End(); //ZXQXLQRM 摩托车 ZHCZXQ
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        // 通过增加 collider 的大小来增大树顶
        [BurstCompile]
        public partial struct GrowTreeColliderJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([ChunkIndexInQuery] int chunkInQueryIndex, Entity entity, ref TreeState treeState,
                ref PhysicsCollider collider, ref PostTransformMatrix postTransformMatrix, EnableTreeGrowth enableTreeGrowth)
            {
                // Note: treeState.Value MUST 等于 TreeState.States.TriggerTreeGrowthSystem，因为 EnableTreeGrowth 为
                // 存在，所以我们不在这里检查它。

                // 这是一个已确定可以生长的树顶
                float3 oldSize = 1.0f;
                float3 newSize = 1.0f;
                unsafe
                {
                    // 抓住盒子指针
                    BoxCollider* bxPtr = (BoxCollider*)collider.ColliderPtr;
                    oldSize = bxPtr->Size;
                    var oldCenter = bxPtr->Center;

                    newSize = oldSize;
                    newSize.y += growthRate;

                    var newCenter = oldCenter;
                    newCenter.y += (growthRate * 0.5f);

                    var boxGeometry = bxPtr->Geometry;
                    boxGeometry.Size = newSize;
                    boxGeometry.Center = newCenter;
                    bxPtr->Geometry = boxGeometry;
                }

                // 现在调整盒子的图形表示
                float3 newScale = newSize / oldSize;
                postTransformMatrix.Value.c0 *= newScale.x;
                postTransformMatrix.Value.c1 *= newScale.y;
                postTransformMatrix.Value.c2 *= newScale.z;

                // 当生长完成后，设置 treeState 使树再次静态
                treeState.Value = TreeState.States.Default;

                // Component 只能在定时增长的 entities 上启用，因此在增长完成后禁用
                ECB.SetComponentEnabled<EnableTreeGrowth>(chunkInQueryIndex, entity, false);
            }
        }
    }
}
