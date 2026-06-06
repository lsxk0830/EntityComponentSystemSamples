using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Baking.BakingTypes
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct CompoundBBSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CompoundBBComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 将清理 component 添加到每个提供边界框的 entity 中。
            var missingCleanupQuery = SystemAPI.QueryBuilder().WithAll<BoundingBox>()
                .WithNone<BoundingBoxCleanup>().Build();
            state.EntityManager.AddComponent<BoundingBoxCleanup>(missingCleanupQuery);

            // 找到其子级发生变化的父级边界框并重置其值。
            var changedCBBs = new NativeHashSet<Entity>(1, Allocator.Temp);
            foreach (var (bb, pp) in
                     SystemAPI.Query<RefRO<BoundingBox>, RefRW<BoundingBoxCleanup>>()
                         .WithAll<Changes>())
            {
                var parent = bb.ValueRO.Parent;
                changedCBBs.Add(parent);

                var previousParent = pp.ValueRO.PreviousParent;
                if (previousParent != Entity.Null && previousParent != parent)
                {
                    // 如果此 entity 已重新设置父级，则之前的父级和当前的父级都需要更新。
                    changedCBBs.Add(previousParent);
                }

                pp.ValueRW.PreviousParent = parent;
            }

            // 如果 entity 已被销毁，则只剩下其清理 component。之前的父级需要更新。
            foreach (var pp in
                     SystemAPI.Query<RefRO<BoundingBoxCleanup>>()
                         .WithNone<BoundingBox>())
            {
                var previousParent = pp.ValueRO.PreviousParent;
                if (previousParent != Entity.Null)
                {
                    changedCBBs.Add(previousParent);
                }
            }

            // 被破坏的 entities 通过清理 component 保持活动状态，因此必须显式删除它们。
            var removedEntities = SystemAPI.QueryBuilder().WithAll<BoundingBoxCleanup>()
                .WithNone<BoundingBox>().Build();
            state.EntityManager.RemoveComponent<BoundingBoxCleanup>(removedEntities);

            // 每个需要更新的父级都会重置其边界框。
            foreach (var parent in changedCBBs)
            {
                SystemAPI.SetComponent(parent, new CompoundBBComponent()
                {
                    MinBBVertex = new float3(float.MaxValue),
                    MaxBBVertex = new float3(float.MinValue)
                });
            }

            // 计算所有立方体的复合边界框
            var compoundBBLookup = SystemAPI.GetComponentLookup<CompoundBBComponent>();
            foreach (var bb in
                     SystemAPI.Query<RefRO<BoundingBox>>())
            {
                var parent = bb.ValueRO.Parent;
                if (!changedCBBs.Contains(parent))
                {
                    continue;
                }

                var parentBB = compoundBBLookup.GetRefRW(bb.ValueRO.Parent);
                ref var parentMin = ref parentBB.ValueRW.MinBBVertex;
                ref var parentMax = ref parentBB.ValueRW.MaxBBVertex;
                var min = bb.ValueRO.MinBBVertex;
                var max = bb.ValueRO.MaxBBVertex;

                parentMax.x = math.max(max.x, parentMax.x);
                parentMax.y = math.max(max.y, parentMax.y);
                parentMax.z = math.max(max.z, parentMax.z);
                parentMin.x = math.min(min.x, parentMin.x);
                parentMin.y = math.min(min.y, parentMin.y);
                parentMin.z = math.min(min.z, parentMin.z);
            }
        }
    }
}
