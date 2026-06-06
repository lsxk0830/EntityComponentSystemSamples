#if !UNITY_DISABLE_MANAGED_COMPONENTS

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace Streaming.SceneManagement.CompleteSample
{
    // 此 system 将根据其与相关 entities 的距离加载/卸载这些部分
    [UpdateAfter(typeof(RequestPostLoadSystem))]
    partial struct TileLODSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var loadSection0Query = SystemAPI.QueryBuilder().WithAll<LoadSection0, ResolvedSectionEntity>().Build();

            // 处理第 0 节的加载。它们不是 LODs 的一部分，需要始终加载
            var sceneEntities = loadSection0Query.ToEntityArray(Allocator.Temp);
            foreach (var sceneEntity in sceneEntities)
            {
                var buffer = state.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
                if (buffer.Length > 0)
                {
                    state.EntityManager.AddComponent<RequestSceneLoaded>(buffer[0].SectionEntity);

                    // 如果某些缓冲区为空，我们无法使用 query 删除它
                    state.EntityManager.RemoveComponent<LoadSection0>(sceneEntity);
                }
            }

            // 我们需要每个部分的图块中心来检查距离。如果该部分中尚不存在，我们会将其复制到其中。
            var noTileEntityQuery = SystemAPI.QueryBuilder().WithAll<TileLODRange, SceneEntityReference>()
                .WithNone<TileEntity>().Build();
            var sectionEntities = noTileEntityQuery.ToEntityArray(Allocator.Temp);
            var sceneEntityReferences = noTileEntityQuery.ToComponentDataArray<SceneEntityReference>(Allocator.Temp);
            for (int index = 0; index < sceneEntityReferences.Length; ++index)
            {
                var tileEntity =
                    state.EntityManager.GetComponentData<TileEntity>(sceneEntityReferences[index].SceneEntity);
                state.EntityManager.AddComponentData(sectionEntities[index], tileEntity);
            }

            // 检查 LOD 部分距离
            NativeHashSet<Entity> toLoad = new NativeHashSet<Entity>(1, Allocator.Temp);

            var sectionQuery = SystemAPI.QueryBuilder().WithAll<TileLODRange, TileEntity, SceneSectionData>()
                .Build();
            sectionEntities = sectionQuery.ToEntityArray(Allocator.Temp);
            var tileEntities = sectionQuery.ToComponentDataArray<TileEntity>(Allocator.Temp);
            var lodRanges = sectionQuery.ToComponentDataArray<TileLODRange>(Allocator.Temp);

            // 根据到相关实体的距离查找应加载的所有部分
            for (int index = 0; index < tileEntities.Length; ++index)
            {
                var distanceComponent =
                    state.EntityManager.GetComponentData<DistanceToRelevant>(tileEntities[index].Value);
                float distanceValueSq = distanceComponent.DistanceSq;
                if (distanceValueSq >= lodRanges[index].LowerRadiusSq &&
                    distanceValueSq < lodRanges[index].HigherRadiusSq)
                {
                    toLoad.Add(sectionEntities[index]);
                }
            }

            // 缓存该部分的流状态
            NativeHashMap<Entity, SceneSystem.SectionStreamingState> streamingStateLookup =
                new NativeHashMap<Entity, SceneSystem.SectionStreamingState>(1, Allocator.Temp);
            foreach (Entity sectionEntity in sectionEntities)
            {
                var sectionState = SceneSystem.GetSectionStreamingState(state.WorldUnmanaged, sectionEntity);
                streamingStateLookup.Add(sectionEntity, sectionState);
            }

            // 根据之前的距离检查加载或卸载部分
            foreach (Entity sectionEntity in sectionEntities)
            {
                var sectionState = streamingStateLookup[sectionEntity];
                if (toLoad.Contains(sectionEntity))
                {
                    if (sectionState == SceneSystem.SectionStreamingState.Unloaded)
                    {
                        // 我们需要加载该部分
                        state.EntityManager.AddComponent<RequestSceneLoaded>(sectionEntity);
                    }
                }
                else if (sectionState != SceneSystem.SectionStreamingState.Unloaded)
                {
                    // 检查邻居以避免之前的 LOD 在新的 LOD 加载之前卸载
                    var sceneEntityReference =
                        state.EntityManager.GetComponentData<SceneEntityReference>(sectionEntity);
                    var sceneSectionEntities =
                        state.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntityReference.SceneEntity,
                            true);

                    int sectionsLoaded = 0;
                    for (int index = 1; index < sceneSectionEntities.Length; ++index)
                    {
                        var neighbourSectionState = streamingStateLookup[sceneSectionEntities[index].SectionEntity];
                        if (neighbourSectionState == SceneSystem.SectionStreamingState.Loaded)
                            ++sectionsLoaded;
                    }

                    // 如果至少有一个其他部分已加载，则卸载
                    if (sectionsLoaded > 1)
                    {
                        state.EntityManager.RemoveComponent<RequestSceneLoaded>(sectionEntity);
                    }
                }
            }
        }
    }
}

#endif
