using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.Samples
{
    /// <summary>
    /// System 用于恢复 predicted ghost 上存在的仅 component 的状态，当
    /// 收到来自 server 的新 snapshot。
    /// <para>
    /// system 必须在 <see cref="GhostUpdateSystem"/> 之后为 run（负责
    /// 更新所有 ghosts) 和<see cref="PredictedSimulationSystemGroup"/>之前的状态
    /// </para> 保证 predicted ghosts components 状态全部同步到相同的最后接收的报价。
    /// <para>
    /// 恢复过程从 <see cref="ClientOnlyBackup"/> 复制 component 数据和使能位
    /// 所有收到新 snapshot 的 ghosts。
    /// 如果在备份缓冲区中没有找到我们要恢复数据的 server，则 components 数据
    /// 保持不变。
    /// </para>
    /// <para>
    /// 在 component 数据恢复为最后接收到的刻度后，所有仅 client 的缓冲区
    /// 通过删除最旧的备份来缩小。尤其：
    /// <para>-对于已收到新 snapshot 的 ghosts，缓冲区为 cleared</para>
    /// <para>-对于 snapshot 中不存在的所有 ghosts，所有具有早于上次收到的刻度的备份的备份都是 removed.</para>
    /// </para>
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostUpdateSystem))]
    public partial struct ClientOnlyComponentRestoreSystem : ISystem
    {
        private EntityQuery predictedGhostsWithClientOnlyBackup;
        private EntityStorageInfoLookup childEntityLookup;
        private BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupHandle;
        private ComponentTypeHandle<ClientOnlyBackup> backupTypeHandle;
        private ComponentTypeHandle<GhostType> ghostTypeHandle;
        private ComponentTypeHandle<PredictedGhost> predictedGhostTypeHandle;
        //内部使其可供测试访问
        internal ClientOnlyTypeHandleList componentTypeHandles;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var queryBuilder = new EntityQueryBuilder(Allocator.Temp);
            queryBuilder.WithAll<PredictedGhost>();
            queryBuilder.WithAll<GhostType>();
            queryBuilder.WithAll<ClientOnlyBackup>();
            predictedGhostsWithClientOnlyBackup = state.GetEntityQuery(queryBuilder);

            linkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            childEntityLookup = state.GetEntityStorageInfoLookup();
            backupTypeHandle = state.GetComponentTypeHandle<ClientOnlyBackup>();
            ghostTypeHandle = state.GetComponentTypeHandle<GhostType>(true);
            predictedGhostTypeHandle = state.GetComponentTypeHandle<PredictedGhost>(true);

            state.RequireForUpdate<EnableClientOnlyBackup>();
            state.RequireForUpdate<ClientOnlyCollection>();
            state.RequireForUpdate<NetworkSnapshotAck>();
            state.RequireForUpdate(predictedGhostsWithClientOnlyBackup);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var clientOnlyCollection = SystemAPI.GetSingleton<ClientOnlyCollection>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            // 完成依赖关系才能访问 NetworkSnapshotAck（因为写在 NetworkStreamReceiveSystem 中）
            state.CompleteDependency();
            var ackComponent = SystemAPI.GetSingleton<NetworkSnapshotAck>();

            backupTypeHandle.Update(ref state);
            ghostTypeHandle.Update(ref state);
            predictedGhostTypeHandle.Update(ref state);
            childEntityLookup.Update(ref state);
            linkedEntityGroupHandle.Update(ref state);
            componentTypeHandles.CreateOrUpdateTypeHandleList(ref state, clientOnlyCollection.ClientOnlyComponentTypes.AsArray());

            //从备份缓冲区复制 component 数据并清除 client 仅备份缓冲区
            //在最后收到的 snapshot 上。
            var job = new RestoreFromBackup
            {
                ghostTypeHandle = ghostTypeHandle,
                predictedGhostComponentTypeHandle = predictedGhostTypeHandle,
                linkedEntityGroupHandle = linkedEntityGroupHandle,
                childEntityLookup = childEntityLookup,
                backupTypeHandle = backupTypeHandle,
                componentTypeHandles = componentTypeHandles,
                clientOnlyComponentCollection = clientOnlyCollection.BackupInfoCollection.AsArray().AsReadOnly(),
                prefabMetadata = clientOnlyCollection.GhostTypeToPrefabMetadata.AsReadOnly(),
                serverTick = networkTime.ServerTick,
                lastReceivedSnapshotByLocal = ackComponent.LastReceivedSnapshotByLocal,
                netDebug = SystemAPI.GetSingleton<NetDebug>()
            };
            state.Dependency = job.ScheduleParallel(predictedGhostsWithClientOnlyBackup, state.Dependency);
        }

        [BurstCompile]
        unsafe struct RestoreFromBackup : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<GhostType> ghostTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PredictedGhost> predictedGhostComponentTypeHandle;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupHandle;
            public ComponentTypeHandle<ClientOnlyBackup> backupTypeHandle;
            public EntityStorageInfoLookup childEntityLookup;
            public ClientOnlyTypeHandleList componentTypeHandles;
            public NativeArray<ClientOnlyBackupInfo>.ReadOnly clientOnlyComponentCollection;
            public NativeHashMap<GhostType, ClientOnlyBackupMetadata>.ReadOnly prefabMetadata;
            public NetworkTick serverTick;
            //从 server 收到的最新报价。可以清除该勾选之前的所有备份历史记录
            public NetworkTick lastReceivedSnapshotByLocal;
            public NetDebug netDebug;

            public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ghostComponents = chunk.GetNativeArray(ref ghostTypeHandle);
                var ghostType = ghostComponents[0];
                //如果未设置类型或在备份信息中尚未找到 ghost 类型，请提前退出。
                if (!prefabMetadata.TryGetValue(ghostType, out var metadata))
                {
                    netDebug.LogError($"Unable to find client-only backup metadata for ghost with ghost type {ghostType}");
                    return;
                }
                var predictedGhosts = chunk.GetNativeArray(ref predictedGhostComponentTypeHandle);
                //使用不安全指针来避免本地副本并让通过 ref 访问备份数据
                var backups = (ClientOnlyBackup*)chunk.GetNativeArray(ref backupTypeHandle).GetUnsafeReadOnlyPtr();
                for (int ent = 0; ent < chunk.Count; ++ent)
                {
                    //这是我们需要恢复的状态。可以是：
                    // - 最后一个完整的 prediction（如果 prediction 正在继续）
                    // - 最后收到的报价（如果有新数据）
                    // - 当前刻度（所以什么都不做）
                    var predictionStartTick = predictedGhosts[ent].PredictionStartTick;
                    //如果备份尚未 run 或刚刚生成 entity，请使用当前状态。
                    //IF 起始刻度是当前目标刻度，没有任何可备份的内容。
                    if (backups[ent].IsEmpty || !predictionStartTick.IsValid || predictionStartTick == serverTick)
                        continue;
                    var backupSlotIndex = backups[ent].GetSlotForTick(predictionStartTick, metadata.backupSize);
                    //如果没有可用的备份，则不执行任何操作并使用当前的 component 状态
                    if (backupSlotIndex < 0)
                        continue;
                    var bufferReader = new BackupReader(backupSlotIndex, backups[ent], metadata);
                    RestoreComponentsFromBackup(chunk, ref bufferReader, ent, metadata);
                    //将缓冲区的长度重置为 0。我们将重新预测从 prediction 开始的所有价格变动
                    //至此 entity
                    if(predictionStartTick == lastReceivedSnapshotByLocal)
                        backups[ent].Clear();
                }
                //从 server 中删除所有蜱虫小于或等于最后收到的蜱虫的备份。
                //Ghosts 不会再回滚到这个刻度。
                for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                    backups[ent].RemoveBackupsOlderThan(lastReceivedSnapshotByLocal, metadata.backupSize);
            }

            private void RestoreComponentsFromBackup(ArchetypeChunk chunk, ref BackupReader reader, int ent,
                in ClientOnlyBackupMetadata metadata)
            {
                //我们有一个有效的备份插槽可供使用。恢复 components 和缓冲区。
                var iterEnd = metadata.componentBegin + metadata.numRootComponents;
                var compIdx = metadata.componentBegin;
                var typeHandles = new UnsafeList<DynamicComponentTypeHandle>(componentTypeHandles.Ptr, clientOnlyComponentCollection.Length);
                for (;compIdx < iterEnd; ++compIdx)
                {
                    var comp = clientOnlyComponentCollection[compIdx];
                    var typeHandle = typeHandles[comp.ComponentIndex];
                    Assertions.Assert.IsFalse(comp.ComponentType.IsBuffer);
                    if (!chunk.Has(ref typeHandle))
                    {
                        reader.Skip(comp);
                        continue;
                    }
                    if (comp.ComponentType.IsEnableable)
                    {
                        var handle = typeHandle;
                        chunk.SetComponentEnabled(ref handle, ent, reader.IsEnabled(compIdx));
                    }
                    var compDataPtr = (byte*)chunk
                        .GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, comp.ComponentSize)
                        .GetUnsafePtr();
                    compDataPtr += comp.ComponentSize * ent;
                    reader.RestoreComponent(comp, compDataPtr);
                }
                //如果 chunk 没有链接的 entity 组，我们无法恢复任何子 component。
                if (!chunk.Has(ref linkedEntityGroupHandle))
                    return;

                var entityGroup = chunk.GetBufferAccessor(ref linkedEntityGroupHandle);
                for (var childCompIdx = compIdx; childCompIdx < metadata.componentEnd; ++childCompIdx)
                {
                    var comp = clientOnlyComponentCollection[childCompIdx];
                    Assertions.Assert.IsFalse(comp.ComponentType.IsBuffer);
                    var typeHandle = typeHandles[comp.ComponentIndex];
                    //NOTE: 这是一个安全条件，以防 entity 组发生更改并且某些子组被删除。
                    //然而，这是一个必要但不充分的条件：总是可以
                    //更改 entity 或删除并添加 entities，长度将相同。
                    //这并不能有力保证我们在这里期望的是哪个 entity，
                    //archetype 也不是。
                    if (entityGroup[ent].Length <= comp.EntityIndex)
                    {
                        reader.Skip(comp);
                        continue;
                    }
                    var childEnt = entityGroup[ent][comp.EntityIndex].Value;
                    var childChunk = childEntityLookup[childEnt].Chunk;
                    var indexInChunk = childEntityLookup[childEnt].IndexInChunk;
                    if (!childChunk.Has(ref typeHandle))
                    {
                        reader.Skip(comp);
                        continue;
                    }
                    if (comp.ComponentType.IsEnableable)
                    {
                        var handle = typeHandle;
                        childChunk.SetComponentEnabled(ref handle, ent, reader.IsEnabled(childCompIdx));
                    }
                    var compDataPtr = (byte*)childChunk
                        .GetDynamicComponentDataArrayReinterpret<byte>(ref typeHandle, comp.ComponentSize)
                        .GetUnsafeReadOnlyPtr();
                    compDataPtr += comp.ComponentSize * indexInChunk;
                    reader.RestoreComponent(comp, compDataPtr);
                }
            }
            //帮助从缓冲区读取备份并检查边界条件的小类（在编辑器中）
            struct BackupReader
            {
                private readonly uint* enableBits;
                private byte* compBackupPtr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                readonly byte* beginCompDataPtr;
                readonly int backupSize;
#endif

                public BackupReader(int backupSlotIndex, in ClientOnlyBackup backup, in ClientOnlyBackupMetadata metadata)
                {
                    var compDataSlotPtr = backup.ComponentBackup.Ptr + backupSlotIndex * metadata.backupSize;
                    var compDataOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) + ClientOnlyBackup.EnableBitByteSize(metadata.componentEnd - metadata.componentBegin));
                    enableBits = (uint*)(compDataSlotPtr + sizeof(uint));
                    compBackupPtr = compDataSlotPtr + compDataOffset;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    beginCompDataPtr = compBackupPtr;
                    backupSize = metadata.backupSize;
#endif
                }

                [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
                private void CheckBounds()
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if((compBackupPtr - beginCompDataPtr) >= backupSize)
                        throw new ArgumentOutOfRangeException();
#endif
                }

                public void Skip(in ClientOnlyBackupInfo comp)
                {
                    compBackupPtr += comp.ComponentSize;
                    CheckBounds();
                }
                public bool IsEnabled(int compIdx)
                {
                    int bitIdx = 1 << (compIdx & 0x1f);
                    return (enableBits[compIdx >> 5] & bitIdx) != 0;
                }
                public void RestoreComponent(in ClientOnlyBackupInfo comp, byte *compDataPtr)
                {
                    CheckBounds();
                    UnsafeUtility.MemCpy(compDataPtr, compBackupPtr, comp.ComponentSize);
                    compBackupPtr += comp.ComponentSize;
                }
            }
        }
    }
}
