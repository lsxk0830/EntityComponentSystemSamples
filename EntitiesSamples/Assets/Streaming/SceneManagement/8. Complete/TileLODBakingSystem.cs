using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Streaming.SceneManagement.CompleteSample
{
    // 此 system 将存储各部分的 LOD 距离元数据
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct TileLODBakingSystem : ISystem
    {
        // 无法进行 Burst 编译，因为它调用 SerializeUtility.GetSceneSectionEntity
        public void OnUpdate(ref SystemState state)
        {
            // 删除之前存储的所有数据，为增量 baking
            var cleaningQuery =  SystemAPI.QueryBuilder().WithAll<TileLODRange, SectionMetadataSetup>().Build();
            state.EntityManager.RemoveComponent<TileLODBaking>(cleaningQuery);

            var radiusQuery = SystemAPI.QueryBuilder().WithAll<TileLODBaking>().Build();
            var sectionLODs = radiusQuery.ToComponentDataArray<TileLODBaking>(Allocator.Temp);

            EntityQuery sectionEntityQuery = default;
            for (int index = 0; index < sectionLODs.Length; ++index)
            {
                // 在 baking 期间获取 entity 部分
                var sectionEntity = SerializeUtility.GetSceneSectionEntity(sectionLODs[index].Section,
                    state.EntityManager, ref sectionEntityQuery, true);

                // 将元信息添加到部分
                var lowerRadius = sectionLODs[index].LowerRadius;
                var higherRadius = sectionLODs[index].HigherRadius;
                state.EntityManager.AddComponentData(sectionEntity, new TileLODRange
                {
                    LowerRadiusSq = lowerRadius * lowerRadius,
                    HigherRadiusSq = higherRadius * higherRadius
                });
            }
        }
    }
}
