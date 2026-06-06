using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.Samples
{
    /// <summary>
    /// System 负责：
    /// - 创建 ClientOnlyCollection 和必要的元数据，用于仅使用 client-components 处理 ghosts。
    /// -向所有 ghosts（存在或不存在 client-仅 components）添加状态 component、ClientOnlyBackup。
    /// - 每个完整刻度备份仅 client 的 components 数据，并将它们存储在 ClientOnlyBackup 缓冲区内。
    /// <para>
    /// System 用于制作所有 client-仅 component 状态的备份。prediction 循环末尾的 system run，
    /// 并将每个完整 predicted 刻度的 components 状态存储在 <see cref="ClientOnlyBackup"/> 历史缓冲区内。
    /// 不保存部分报价。
    /// </para>
    /// <para>
    /// 备份由 components 数据的内存副本组成，并且如果某些 component 也实现了
    /// <see cref="IEnableableComponent"/>，它们的使能位。
    /// </para>
    /// <remarks>
    /// <see cref="ClientOnlyBackup"/> 缓冲区的大小不固定并且可以增长，以适应延迟和延迟
    /// ghosts 更新频率。缓冲区的大小仍然受到限制，因为最旧的备份已被删除并且
    /// 插槽被重复使用。
    /// </remarks>
    /// <para>
    /// 然后，当从 server 接收到新的 snapshot 时，保存的 components 状态用于恢复 components 数据。
    /// 有关详细信息，请参阅 <see cref="ClientOnlyComponentRestoreSystem"/>。
    /// </para>
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostPredictionHistorySystem))]
    public partial struct ClientOnlyComponentBackupSystem : ISystem
    {
        private const int InitialBackupCapacity = 16;
        private EntityQuery m_predictedGhostsWithClientOnlyBackup;
        private EntityQuery m_predictedGhostsNotProcessed;
        private EntityQuery m_predictedPrespawendGhostsNotProcessed;
        private EntityQuery m_destroyedGhostsWithClientOnlyBackup;

        private EntityStorageInfoLookup m_childEntityLookup;
        private BufferTypeHandle<LinkedEntityGroup> m_linkedEntityGroupHandle;
        private ComponentTypeHandle<ClientOnlyBackup> m_backupTypeHandle;
        private ComponentTypeHandle<GhostType> m_ghostTypeHandle;

        private NativeList<ComponentType> m_clientOnlyComponentTypes;
        private NativeList<ClientOnlyBackupInfo> m_clientOnlyBackupInfoCollection;
        private NativeHashMap<GhostType, ClientOnlyBackupMetadata> m_ghostTypeToPrefabMetadata;
        private ClientOnlyTypeHandleList m_clientOnlyTypeHandleList;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var queryBuilder = new EntityQueryBuilder(Allocator.Temp);
            queryBuilder.WithAll<Simulate>();
            queryBuilder.WithAll<PredictedGhost>();
            queryBuilder.WithAll<ClientOnlyBackup>();
            m_predictedGhostsWithClientOnlyBackup = state.GetEntityQuery(queryBuilder);
            queryBuilder.Reset();
            queryBuilder.WithAll<PredictedGhost>();
            queryBuilder.WithAll<GhostType>();
            queryBuilder.WithNone<ClientOnlyProcessed>();
            queryBuilder.WithNone<PreSpawnedGhostIndex>();
            m_predictedGhostsNotProcessed = state.GetEntityQuery(queryBuilder);
            queryBuilder.Reset();
            queryBuilder.WithAll<PredictedGhost>();
            queryBuilder.WithAll<GhostType>();
            queryBuilder.WithNone<ClientOnlyProcessed>();
            queryBuilder.WithAll<PreSpawnedGhostIndex>();
            m_predictedPrespawendGhostsNotProcessed = state.GetEntityQuery(queryBuilder);
            queryBuilder.Reset();
            queryBuilder.WithAll<ClientOnlyBackup>();
            queryBuilder.WithNone<PredictedGhost>();
            m_destroyedGhostsWithClientOnlyBackup = state.GetEntityQuery(queryBuilder);

            m_clientOnlyBackupInfoCollection = new NativeList<ClientOnlyBackupInfo>(Allocator.Persistent);
            m_clientOnlyComponentTypes = new NativeList<ComponentType>(32, Allocator.Persistent);
            m_ghostTypeToPrefabMetadata = new NativeHashMap<GhostType, ClientOnlyBackupMetadata>(128, Allocator.Persistent);
            m_clientOnlyTypeHandleList = default(ClientOnlyTypeHandleList);

            m_backupTypeHandle = state.GetComponentTypeHandle<ClientOnlyBackup>();
            m_ghostTypeHandle = state.GetComponentTypeHandle<GhostType>(true);
            m_childEntityLookup = state.GetEntityStorageInfoLookup();
            m_linkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);

            //创建单例。
            var types = new NativeArray<ComponentType>(1, Allocator.Temp);
            types[0] = ComponentType.ReadWrite<ClientOnlyCollection>();
            var singleton = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(types));
            state.EntityManager.SetComponentData(singleton, new ClientOnlyCollection
            {
                ProcessedPrefabs = 0,
                ClientOnlyComponentTypes = m_clientOnlyComponentTypes,
                BackupInfoCollection = m_clientOnlyBackupInfoCollection,
                GhostTypeToPrefabMetadata = m_ghostTypeToPrefabMetadata
            });

            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<EnableClientOnlyBackup>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_clientOnlyComponentTypes.Dispose();
            m_clientOnlyBackupInfoCollection.Dispose();
            m_ghostTypeToPrefabMetadata.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var clientOnlyCollection = ref SystemAPI.GetSingletonRW<ClientOnlyCollection>().ValueRW;
            if(clientOnlyCollection.ClientOnlyComponentTypes.Length == 0)
                return;
            var ghostCollection = SystemAPI.GetSingleton<GhostCollection>();
            if(!ghostCollection.IsInGame || ghostCollection.NumLoadedPrefabs == 0)
                return;

            if (clientOnlyCollection.ProcessedPrefabs < ghostCollection.NumLoadedPrefabs)
            {
                var ghostPrefabs = SystemAPI.GetSingletonBuffer<GhostCollectionPrefab>(true);
                for (int i = clientOnlyCollection.ProcessedPrefabs; i < ghostCollection.NumLoadedPrefabs; ++i)
                {
                    clientOnlyCollection.ProcessGhostTypePrefab(ghostPrefabs[i].GhostType, ghostPrefabs[i].GhostPrefab, state.EntityManager);
                    ++clientOnlyCollection.ProcessedPrefabs;
                }
            }
            if(clientOnlyCollection.ProcessedPrefabs == 0)
                return;

            if (!m_destroyedGhostsWithClientOnlyBackup.IsEmpty)
            {
                var job = new DisposeBackupJob();
                job.Run(m_destroyedGhostsWithClientOnlyBackup);
                state.EntityManager.RemoveComponent<ClientOnlyBackup>(m_destroyedGhostsWithClientOnlyBackup);
            }

            //向 ghost 添加必要的状态以备份 components 和缓冲区。
            if (!m_predictedGhostsNotProcessed.IsEmpty)
            {
                var entities = m_predictedGhostsNotProcessed.ToEntityArray(Allocator.Temp);
                var ghostTypes = m_predictedGhostsNotProcessed.ToComponentDataArray<GhostType>(Allocator.Temp);
                //将 entities 标记为已处理
                state.EntityManager.AddComponent<ClientOnlyProcessed>(m_predictedGhostsNotProcessed);
                for(int ent=0;ent<entities.Length;++ent)
                {
                    var ghostType = ghostTypes[ent];
                    //如果类型没有任何 client 只有 component，则跳过。
                    if(!clientOnlyCollection.GhostTypeToPrefabMetadata.ContainsKey(ghostType))
                        continue;
                    //像这样一一添加 component 是非常非常慢的。理想情况下，我想添加 component “per chunk”
                    //但这实际上是不可能的（或者说并不容易实现）
                    var clientOnlyMetadata = clientOnlyCollection.GhostTypeToPrefabMetadata[ghostType];
                    var clientOnlyBackup = new ClientOnlyBackup(slotSize:clientOnlyMetadata.backupSize, capacity:InitialBackupCapacity);
                    state.EntityManager.AddComponentData(entities[ent], clientOnlyBackup);
                }
            }

            //预生成的 ghosts 有一个特殊的路径，因为处理有点不同。特别是我们可以分配
            //已处理的标志到 query（最快的方法），但我们需要一一检查 entities。
            //这在结构性变化方面产生了相当大的负担。
            if (!m_predictedPrespawendGhostsNotProcessed.IsEmpty)
            {
                var entities = m_predictedPrespawendGhostsNotProcessed.ToEntityArray(Allocator.Temp);
                var ghostTypes = m_predictedPrespawendGhostsNotProcessed.ToComponentDataArray<GhostType>(Allocator.Temp);
                state.EntityManager.AddComponent<ClientOnlyProcessed>(m_predictedPrespawendGhostsNotProcessed);
                for(int ent=0;ent<entities.Length;++ent)
                {
                    var ghostType = ghostTypes[ent];
                    if (!clientOnlyCollection.GhostTypeToPrefabMetadata.ContainsKey(ghostType))
                        continue;
                    var clientOnlyMetadata = clientOnlyCollection.GhostTypeToPrefabMetadata[ghostType];
                    var clientOnlyBackup = new ClientOnlyBackup(slotSize:clientOnlyMetadata.backupSize, capacity:InitialBackupCapacity);
                    state.EntityManager.AddComponentData(entities[ent], clientOnlyBackup);
                }
            }

            //仅备份完整的 server 勾选。部分报价始终从最后一个完整模拟报价开始，因此无需备份
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (networkTime.IsPartialTick)
                return;
            m_childEntityLookup.Update(ref state);
            m_linkedEntityGroupHandle.Update(ref state);
            m_backupTypeHandle.Update(ref state);
            m_ghostTypeHandle.Update(ref state);
            m_clientOnlyTypeHandleList.CreateOrUpdateTypeHandleList(ref state, clientOnlyCollection.ClientOnlyComponentTypes.AsArray());
            var backupJob = new BackupJob
            {
                childEntityLookup = m_childEntityLookup,
                ghostTypeHandle = m_ghostTypeHandle,
                linkedEntityGroupHandle = m_linkedEntityGroupHandle,
                componentTypeHandles = m_clientOnlyTypeHandleList,
                clientOnlyComponentCollection = clientOnlyCollection.BackupInfoCollection,
                prefabMetadata = clientOnlyCollection.GhostTypeToPrefabMetadata,
                backupTypeHandle = m_backupTypeHandle,
                serverTick = networkTime.ServerTick,
                netDebug = SystemAPI.GetSingleton<NetDebug>()
            };
            state.Dependency = backupJob.ScheduleParallel(m_predictedGhostsWithClientOnlyBackup, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClientOnlyBackup))]
        [WithNone(typeof(PredictedGhost))]
        partial struct DisposeBackupJob : IJobEntity
        {
            public void Execute(ref ClientOnlyBackup backup)
            {
                backup.Dispose();
            }
        }

        //帮助编写 component 备份的小课程。抽象一些指针操作并添加一些
        //boundary checks (in the editor)
        unsafe ref struct ClientOnlyBackupWriter
        {
            private readonly int* backupEnableBitPtr;
            private uint* backupTick;
            private byte* backupCompDataPtr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private byte* beginCompDataPtr;
            private int backupSize;
#endif

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckBounds()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if((backupCompDataPtr - beginCompDataPtr) >= backupSize)
                    throw new ArgumentOutOfRangeException();
#endif
            }

            public ClientOnlyBackupWriter(byte* bufPtr, int compDataOffset, int slotSize)
            {
                backupTick = (uint*)bufPtr;
                backupEnableBitPtr = (int*)(bufPtr + sizeof(uint));
                backupCompDataPtr = (bufPtr + compDataOffset);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                beginCompDataPtr = backupCompDataPtr;
                backupSize = slotSize;
#endif
            }

            //跳过 component/缓冲区备份。将数据重置为 0 并推进备份指针
            public void Skip(in ClientOnlyBackupInfo comp)
            {
                CheckBounds();
                UnsafeUtility.MemClear(backupCompDataPtr, comp.ComponentSize);
                backupCompDataPtr += comp.ComponentSize;
            }

            public void WriteTick(NetworkTick serverTick)
            {
                *backupTick = serverTick.SerializedData;
            }

            //将 component 的启用位存储在备份内。备份槽数据格式为
            //  4 字节 4 字节 4 字节
            // [勾选][EnableBits 0][EnableBits 1]
            //              3130 ...   0  64 ...    32
            // 位从左到右存储
            public void BackupEnableBitForComponent(int compIdx, int ent, [ReadOnly] long* bitArrayPtr)
            {
                int entIdx = 1 << (ent & 0x3f);
                long compEnableFroEntity = ((bitArrayPtr[ent >> 6] >> entIdx) & 0x1);
                int bitIdx = 1 << (compIdx & 0x1f);
                backupEnableBitPtr[compIdx >> 5] &= ~bitIdx;
                backupEnableBitPtr[compIdx >> 5] |= (int)(bitIdx * compEnableFroEntity);
            }

            //将 component 数据存储到备份缓冲区中。
            public void BackupComponent([ReadOnly] byte* compDataPtr, in ClientOnlyBackupInfo comp)
            {
                CheckBounds();
                //TODO: 对于小 component 数据大小（如 8/64 字节）进行优化可能更好
                UnsafeUtility.MemCpy(backupCompDataPtr, compDataPtr, comp.ComponentSize);
                backupCompDataPtr += comp.ComponentSize;
            }
        }

        [BurstCompile]
        unsafe struct BackupJob : IJobChunk
        {
            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public ComponentTypeHandle<GhostType> ghostTypeHandle;
            public ComponentTypeHandle<ClientOnlyBackup> backupTypeHandle;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupHandle;

            [ReadOnly] public ClientOnlyTypeHandleList componentTypeHandles;
            [ReadOnly] public NativeList<ClientOnlyBackupInfo> clientOnlyComponentCollection;
            [ReadOnly] public NativeHashMap<GhostType, ClientOnlyBackupMetadata> prefabMetadata;
            public NetworkTick serverTick;
            public NetDebug netDebug;
            public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                //查找 ghost 类型。如果 chunk 包含 predicted prespawed-ghost 并且类型尚未分配
                //然而，我们跳过了备份。
                var ghostTypes = chunk.GetNativeArray(ref ghostTypeHandle);
                var firstGhostType = ghostTypes[0];
                //检索备份元数据。在调度 jobs 之前，这应该已经由 system 构建
                if (!prefabMetadata.TryGetValue(firstGhostType, out var metadata))
                {
                    netDebug.LogError($"Unable to find client-only backup metadata for ghost with ghost type {firstGhostType}");
                    return;
                }
                //准备 thw writers 并将 ghost 数据复制到备份缓冲区中。
                //Components 和缓冲区数据按 entity 进行备份，根实体和子实体之间的代码略有不同。
                var backupWriters = stackalloc ClientOnlyBackupWriter[chunk.Count];
                InitBackupWriters(chunk, metadata, backupWriters);
                BackupComponents(chunk, metadata, backupWriters);
            }

            private void BackupComponents(in ArchetypeChunk chunk, in ClientOnlyBackupMetadata metadata, ClientOnlyBackupWriter *backupWriters)
            {
                int chunkEntityCount = chunk.Count;
                //存储当前插槽的 server 刻度
                for (int ent = 0; ent < chunkEntityCount; ++ent)
                    backupWriters[ent].WriteTick(serverTick);

                //备份整个 chunk 的所有根 component。
                var iterEnd = metadata.componentBegin + metadata.numRootComponents;
                var compIdx = metadata.componentBegin;
                //只是添加一些边界检查的便捷方法
                var typeHandles = new UnsafeList<DynamicComponentTypeHandle>(componentTypeHandles.Ptr, clientOnlyComponentCollection.Length);
                for (;compIdx < iterEnd; ++compIdx)
                {
                    var comp = clientOnlyComponentCollection[compIdx];
                    //不支持缓冲区
                    Unity.Assertions.Assert.IsFalse(comp.ComponentType.IsBuffer);
                    var typeHandle = typeHandles[comp.ComponentIndex];
                    if (!chunk.Has(ref typeHandle))
                    {
                        for (int ent = 0; ent < chunkEntityCount; ++ent)
                            backupWriters[ent].Skip(comp);
                        continue;
                    }
                    if (comp.ComponentType.IsEnableable)
                    {
                        var handle = typeHandle;
                        var bitArray = chunk.GetEnableableBits(ref handle);
                        var bitArrayPtr = (long*)UnsafeUtility.AddressOf(ref bitArray);
                        for (int ent = 0; ent < chunkEntityCount; ++ent)
                        {
                            backupWriters[ent].BackupEnableBitForComponent(compIdx, ent, bitArrayPtr);
                        }
                    }
                    var compDataPtr = (byte*)chunk
                        .GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, comp.ComponentSize)
                        .GetUnsafeReadOnlyPtr();
                    for (int ent = 0; ent < chunkEntityCount; ++ent)
                    {
                        backupWriters[ent].BackupComponent(compDataPtr, comp);
                        compDataPtr += comp.ComponentSize;
                    }
                }
                //备份所有子 entities components。
                if (!chunk.Has(ref linkedEntityGroupHandle))
                    return;
                var entityGroup = chunk.GetBufferAccessor(ref linkedEntityGroupHandle);
                for (int childCompIdx = compIdx; childCompIdx < metadata.componentEnd; ++childCompIdx)
                {
                    var comp = clientOnlyComponentCollection[childCompIdx];
                    var typeHandle = typeHandles[comp.ComponentIndex];
                    for (int ent = 0; ent < chunkEntityCount; ++ent)
                    {
                        if (entityGroup[ent].Length <= comp.EntityIndex)
                        {
                            backupWriters[ent].Skip(comp);
                            continue;
                        }
                        var childEnt = entityGroup[ent][comp.EntityIndex].Value;
                        var childChunk = childEntityLookup[childEnt].Chunk;
                        var indexInChunk = childEntityLookup[childEnt].IndexInChunk;
                        if (!childChunk.Has(ref typeHandle))
                        {
                            backupWriters[ent].Skip(comp);
                            continue;
                        }
                        if (comp.ComponentType.IsEnableable)
                        {
                            var handle = typeHandle;
                            var bitArray = childChunk.GetEnableableBits(ref handle);
                            var bitArrayPtr = (long*)UnsafeUtility.AddressOf(ref bitArray);
                            backupWriters[ent].BackupEnableBitForComponent(childCompIdx, indexInChunk, bitArrayPtr);
                        }
                        var compDataPtr = (byte*)childChunk
                            .GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, comp.ComponentSize)
                            .GetUnsafeReadOnlyPtr();
                        compDataPtr += comp.ComponentSize * indexInChunk;
                        backupWriters[ent].BackupComponent(compDataPtr, comp);
                    }
                }
            }

            private void InitBackupWriters(ArchetypeChunk chunk, in ClientOnlyBackupMetadata metadata, ClientOnlyBackupWriter* backupWriters)
            {
                var enableBitsIntSize = ClientOnlyBackup.EnableBitByteSize(metadata.componentEnd - metadata.componentBegin);
                var compDataStartOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) + enableBitsIntSize);
                //使用原始指针通过 ref 访问 component 数据。这用于获取和增长缓冲区。
                var states = (ClientOnlyBackup*)chunk.GetNativeArray(ref backupTypeHandle).GetUnsafePtr();
                for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                {
                    states[ent].GrowBufferIfFull(metadata.backupSize);
                    var backupSlotIndex = states[ent].AcquireBackupSlot();
                    var slotOffset = metadata.backupSize * backupSlotIndex;
                    byte* backupDataPtr = states[ent].ComponentBackup.Ptr + slotOffset;
                    backupWriters[ent] = new ClientOnlyBackupWriter(backupDataPtr, compDataStartOffset, metadata.backupSize);
                }
            }
        }
    }
}
