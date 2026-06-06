using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel;

namespace Samples.HelloNetcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    [UpdateAfter(typeof(GhostSpawnClassificationSystem))]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    [BurstCompile]
    public partial struct GrenadeClassificationSystem : ISystem
    {
        SnapshotDataLookupHelper m_SnapshotDataLookupHelper;
        BufferLookup<PredictedGhostSpawn> m_PredictedGhostSpawnLookup;
        ComponentLookup<GrenadeData> m_GrenadeDataLookup;
        // ghost 型（手榴弹）此分类 system 将处理
        int m_GhostType;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_SnapshotDataLookupHelper = new SnapshotDataLookupHelper(ref state,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
            m_PredictedGhostSpawnLookup = state.GetBufferLookup<PredictedGhostSpawn>();
            m_GrenadeDataLookup = state.GetComponentLookup<GrenadeData>();
            state.RequireForUpdate<GhostSpawnQueue>();
            state.RequireForUpdate<PredictedGhostSpawnList>();
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<GrenadeSpawner>();
            m_GhostType = -1;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_GhostType == -1)
            {
                // 在 ghost prefab 列表中查找手榴弹 prefab entity，从那里我们可以找到该 prefab 的 ghost 类型
                var prefabEntity = SystemAPI.GetSingleton<GrenadeSpawner>().Grenade;
                var collectionEntity = SystemAPI.GetSingletonEntity<GhostCollection>();
                var ghostPrefabTypes = state.EntityManager.GetBuffer<GhostCollectionPrefab>(collectionEntity);
                for (int i = 0; i < ghostPrefabTypes.Length; ++i)
                {
                    if (ghostPrefabTypes[i].GhostPrefab == prefabEntity)
                    {
                        m_GhostType = i;
                        break;
                    }
                }
            }
            m_SnapshotDataLookupHelper.Update(ref state);
            m_PredictedGhostSpawnLookup.Update(ref state);
            m_GrenadeDataLookup.Update(ref state);
            var classificationJob = new GrenadeClassificationJob
            {
                snapshotDataLookupHelper = m_SnapshotDataLookupHelper,
                spawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>(),
                PredictedSpawnListLookup = m_PredictedGhostSpawnLookup,
                grenadeDataLookup = m_GrenadeDataLookup,
                ghostType = m_GhostType
            };
            state.Dependency = classificationJob.Schedule(state.Dependency);
        }

        [WithAll(typeof(GhostSpawnQueue))]
        [BurstCompile]
        partial struct GrenadeClassificationJob : IJobEntity
        {
            public SnapshotDataLookupHelper snapshotDataLookupHelper;
            public Entity spawnListEntity;
            public BufferLookup<PredictedGhostSpawn> PredictedSpawnListLookup;
            public ComponentLookup<GrenadeData> grenadeDataLookup;
            public int ghostType;

            public void Execute(DynamicBuffer<GhostSpawnBuffer> newSpawns, DynamicBuffer<SnapshotDataBuffer> data)
            {
                var predictedSpawnList = PredictedSpawnListLookup[spawnListEntity];
                var snapshotDataLookup = snapshotDataLookupHelper.CreateSnapshotBufferLookup();
                for (int i = 0; i < newSpawns.Length; ++i)
                {
                    var newGhostSpawn = newSpawns[i];
                    if (newGhostSpawn.GhostType != ghostType)
                        continue; // 不是手榴弹。

                    if (newGhostSpawn.SpawnType != GhostSpawnBuffer.Type.Predicted || newGhostSpawn.PredictedSpawnEntity != Entity.Null)
                        continue;

                    // 将所有手榴弹生成标记为机密，即使不是我们自己的 predicted 生成
                    // 否则，当其他玩家生成时，默认分类 system 可能会被拾取
                    // 当我们碰巧在尚未分类的 predictedSpawnList 中生成 predicted 时，它就会运行
                    newGhostSpawn.HasClassifiedPredictedSpawn = true;

                    // 查找新的 ghost 生成（来自 ghost snapshot），其与由处理的预测生成的 ghost 类型匹配
                    // 此分类为 system。匹配来自新生成的生成 ID 数据（通过在
                    // snapshot 数据）与 predicted 生成列表中 ghosts 的生成 IDs。当匹配时我们替换
                    // 该新生成的 ghost entity 与我们预测生成的 entity （因此生成不会导致
                    // 在新的实例中）。
                    for (int j = 0; j < predictedSpawnList.Length; ++j)
                    {
                        if (newGhostSpawn.GhostType == predictedSpawnList[j].ghostType)
                        {
                            if (snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(newGhostSpawn.GhostType, data, out GrenadeData grenadeData, i))
                            {
                                var spawnIdFromList = grenadeDataLookup[predictedSpawnList[j].entity].SpawnId;
                                if (grenadeData.SpawnId == spawnIdFromList)
                                {
                                    newGhostSpawn.PredictedSpawnEntity = predictedSpawnList[j].entity;
                                    predictedSpawnList.RemoveAtSwapBack(j);
                                    break;
                                }
                            }
                        }
                    }
                    newSpawns[i] = newGhostSpawn;
                }
            }
        }
    }
}
