using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine.Assertions;

namespace Samples.CustomChunkSerializer
{
    [BurstCompile]
    public struct ChunkSerializer
    {
        //关于 snapshot 缓冲存储器布局
        //snapshot 缓冲区包含此格式的条目
        // [tick][ent1 data][ent2 data] ... [entN data]
        // 数据具有以下布局：
        // uint 刻度
        // 4 字节对齐的更改掩码位
        // （选修的）
        // 4 字节对齐的使能位状态
        // padding (to 16 bytes)
        // component1 (aligned to 16 byte boundary)
        // component2 (aligned to 16 byte boundary)
        // ..
        // componentN (aligned to 16 byte boundary)

        //关于位大小内存布局。
        //bitStartAndSize 包含 ghost 数据的起始 uint 和位 len。
        //有以下布局
        // [组件 1][组件 2
        // |ent1 开始|ent1 len|..|entN 开始|entN len||ent1 开始|ent1 len|..|entN 开始|entN len|
        //
        // 步幅为 NumComponent * (endIndex - startIndex)，其中 endIndex 和 startIndex 是第一个和最后一个
        // chunk 中的相关 entity 索引。

        public static PortableFunctionPointer<GhostPrefabCustomSerializer.CollectComponentDelegate> CollectComponentFunc =
                new PortableFunctionPointer<GhostPrefabCustomSerializer.CollectComponentDelegate>(CollectComponents);

        public static PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkSerializerDelegate> SerializerFunc =
            new PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkSerializerDelegate>(SerializeChunk);

        public static PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkPreserializeDelegate> PreSerializerFunc =
            new PortableFunctionPointer<GhostPrefabCustomSerializer.ChunkPreserializeDelegate>(PreSerializeChunk);

        //自定义方法以特定顺序注册 component 类型，以便可以编写 chunk 序列化器
        //更容易。
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostPrefabCustomSerializer.CollectComponentDelegate))]
        public static void CollectComponents(IntPtr componentTypesPtr, IntPtr componentCountPtr)
        {
            ref var componentTypes = ref GhostComponentSerializer.TypeCast<NativeList<ComponentType>>(componentTypesPtr);
            ref var componentCount = ref GhostComponentSerializer.TypeCast<NativeArray<int>>(componentCountPtr);
            //根
            componentTypes.Add(ComponentType.ReadWrite<GhostOwner>());
            componentTypes.Add(ComponentType.ReadWrite<LocalTransform>());
            componentTypes.Add(ComponentType.ReadWrite<IntCompo1>());
            componentTypes.Add(ComponentType.ReadWrite<IntCompo2>());
            componentTypes.Add(ComponentType.ReadWrite<IntCompo3>());
            componentTypes.Add(ComponentType.ReadWrite<FloatCompo1>());
            componentTypes.Add(ComponentType.ReadWrite<FloatCompo2>());
            componentTypes.Add(ComponentType.ReadWrite<FloatCompo3>());
            componentTypes.Add(ComponentType.ReadWrite<InterpolatedOnlyComp>());
            componentTypes.Add(ComponentType.ReadWrite<OwnerOnlyComp>());
            componentTypes.Add(ComponentType.ReadWrite<Buf1>());
            componentTypes.Add(ComponentType.ReadWrite<Buf2>());
            componentTypes.Add(ComponentType.ReadWrite<Buf3>());
            componentCount[0] = 13;
            //儿童 1
            componentTypes.Add(ComponentType.ReadWrite<IntCompo1>());
            componentTypes.Add(ComponentType.ReadWrite<FloatCompo1>());
            componentTypes.Add(ComponentType.ReadWrite<Buf1>());
            componentCount[1] = 3;
            //儿童 2
            componentTypes.Add(ComponentType.ReadWrite<IntCompo2>());
            componentTypes.Add(ComponentType.ReadWrite<FloatCompo2>());
            componentTypes.Add(ComponentType.ReadWrite<Buf2>());
            componentCount[2] = 3;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostPrefabCustomSerializer.CollectComponentDelegate))]
        private static unsafe void PreSerializeChunk(in ArchetypeChunk chunk, in GhostCollectionPrefabSerializer typeData,
            in DynamicBuffer<GhostCollectionComponentIndex> componentIndices,
            ref GhostPrefabCustomSerializer.Context context)
        {
            var indices = (GhostCollectionComponentIndex*)componentIndices.GetUnsafeReadOnlyPtr() + typeData.FirstComponent;
            CopyComponentsToSnapshot(chunk, ref context, typeData, indices);
        }

        const int BaselinesPerEntity = 4;
        //所做的假设：
        // - components 永远不会被删除（所以我们不检查是否存在）
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(GhostPrefabCustomSerializer.ChunkSerializerDelegate))]
        private static unsafe void SerializeChunk(ref ArchetypeChunk chunk,
            in GhostCollectionPrefabSerializer typeData,
            in DynamicBuffer<GhostCollectionComponentIndex> componentIndices,
            ref GhostPrefabCustomSerializer.Context context,
            ref DataStreamWriter tempWriter,
            in StreamCompressionModel compressionModel, ref int lastSerializedEntity)
        {
            var baselinesPerEntity = (IntPtr*)context.baselinePerEntityPtr;
            var sameBaselinePerEntity = (int*)context.sameBaselinePerEntityPtr;
            int* entityBitAndSize = (int*)context.entityStartBit;
            var entityCount = context.endIndex - context.startIndex;
            int* componentBitsSize = entityBitAndSize + 2*entityCount;
            int compBitSizeStride = 2*entityCount;
            var indices = (GhostCollectionComponentIndex*)componentIndices.GetUnsafeReadOnlyPtr() + typeData.FirstComponent;
            if(context.hasPreserializedData == 0)
                CopyComponentsToSnapshot(chunk, ref context, typeData, indices);

            int* dynamicDataSizePerEntity = (int*)context.dynamicDataSizePerEntityPtr;
            for (int ent = context.startIndex; ent < context.endIndex; ++ent)
            {
                var old = tempWriter;
                var entOffset = ent - context.startIndex;
                //避免序列化不相关的 entities
                var sameBaselineCount = sameBaselinePerEntity[entOffset];
                if (sameBaselineCount < 0)
                {
                    // 这是一个无关的 ghost，请勿发送。无需重置
                    //位大小和起始位，因为外部也进行相同的检查。
                    continue;
                }

                //对于 chunk 序列化器，写入器包含不同格式的数据：
                //我们直接在这里对“entity”进行序列化。
                //这提供了能够提前退出而不需要序列化其他 entities 的优点，如果
                //他们不适合（或者至少在第一个失败后提前退出）
                //理想情况下（这是第二阶段），我们现在可以直接写入真实的数据流。现在
                //这是不可能的，因为我们之前添加了缓冲区的大小和 ghosts 数据（增量压缩）
                //ghost 流。
                var snapshotData = context.snapshotDataPtr + entOffset*context.snapshotStride;
                var changeMaskData = snapshotData + sizeof(int);
                var currentWrittenBits = tempWriter.LengthInBits;
                var baseline0Ptr = baselinesPerEntity[BaselinesPerEntity*entOffset];
                var baseline1Ptr = baselinesPerEntity[BaselinesPerEntity*entOffset + 1];
                var baseline2Ptr = baselinesPerEntity[BaselinesPerEntity*entOffset + 2];
                //如果没有基线的缓冲区不存在，这可以是 IntPtrZero
                var dynamicDataBaselinePtr = baselinesPerEntity[BaselinesPerEntity*entOffset + 3];
                //这需要用全零覆盖 snapshot 数据或保留当前 predicted 基线（最佳）
                var ghostSendType = GhostSendType.AllClients;
                var sendToOwner = SendToOwnerType.All;
                //comp 位大小已指向第一个 component 条目的 entityBitSize[1]。
                var compBitSize = componentBitsSize + 2*entOffset + 1;
                if (typeData.PredictionOwnerOffset != 0)
                {
                    var isOwner = (context.networkId == *(int*)((byte*)snapshotData + typeData.PredictionOwnerOffset));
                    if (typeData.PartialSendToOwner != 0)
                        sendToOwner = isOwner ? SendToOwnerType.SendToOwner : SendToOwnerType.SendToNonOwner;
                    if (typeData.PartialComponents != 0 && typeData.OwnerPredicted != 0)
                        ghostSendType = isOwner ? GhostSendType.OnlyPredictedClients : GhostSendType.OnlyInterpolatedClients;
                }
                if (baseline2Ptr != IntPtr.Zero)
                {
                    SerializeWithThreeBaselines(snapshotData, context.snapshotOffset, sendToOwner, ghostSendType,
                        indices, ref tempWriter, compressionModel, baseline0Ptr, baseline1Ptr, baseline2Ptr,
                        changeMaskData, context.snapshotDynamicDataPtr, dynamicDataBaselinePtr,
                        ref dynamicDataSizePerEntity[entOffset], compBitSize, compBitSizeStride);
                }
                //单基线
                else if (baseline0Ptr != IntPtr.Zero)
                {
                    SerializeWithSingleBaseline(snapshotData, context.snapshotOffset, sendToOwner, ghostSendType,
                        indices, ref tempWriter, compressionModel, baseline0Ptr,
                        changeMaskData, context.snapshotDynamicDataPtr, dynamicDataBaselinePtr,
                        ref dynamicDataSizePerEntity[entOffset], compBitSize, compBitSizeStride);
                }
                //没有基线，我们传递一个指向包含全零的基线的指针。
                //这样做的优点是可以扩展，允许使用“初始值”
                //优化，发送 prefab 初始数据的增量。
                else
                {
                    SerializeWithSingleBaseline(snapshotData, context.snapshotOffset, sendToOwner, ghostSendType,
                        indices, ref tempWriter, compressionModel, context.zeroBaseline,
                        changeMaskData, context.snapshotDynamicDataPtr, IntPtr.Zero,
                        ref dynamicDataSizePerEntity[entOffset], compBitSize, compBitSizeStride);
                }
                //计算每个 ghosts 的位数。
                entityBitAndSize[2*entOffset] = currentWrittenBits / 32;
                entityBitAndSize[2*entOffset+1] = tempWriter.LengthInBits - currentWrittenBits;
                var missing = 32 - tempWriter.LengthInBits & 31;
                if (missing < 32)
                    tempWriter.WriteRawBits(0, missing);
                if (tempWriter.HasFailedWrites)
                {
                    //如果我们能够存储至少一个 entity 则不需要标记流
                    //失败了。
                    //Rollback 在这里并让外循环序列化完整的 entities。
                    if (entOffset > 0)
                    {
                        tempWriter = old;
                        lastSerializedEntity = ent - 1;
                    }
                    break;
                }
            }
        }

        private static unsafe void CopyComponentsToSnapshot(ArchetypeChunk chunk,
            ref GhostPrefabCustomSerializer.Context context,
            in GhostCollectionPrefabSerializer typeData,
            GhostCollectionComponentIndex* indices)
        {
            var ghostChunkComponentTypesPtr = (DynamicComponentTypeHandle*)context.ghostChunkComponentTypes;

            var maskOffset = 0;
            var snapshotOffset = context.snapshotOffset;
            var dynamicSnapshotOffset = context.dynamicDataOffset;
            var changeMaskUints = GhostComponentSerializer.ChangeMaskArraySizeInUInts(typeData.ChangeMaskBits);
            var snapshotPtr = context.snapshotDataPtr;
            var enableBits = (byte*)(snapshotPtr + sizeof(int) + 4*changeMaskUints);

            //ROOT COMPONENTS
            //GENERATE ONLY THIS
            new Unity_NetCode_Generated_Unity_NetCode.Unity_NetCode_Generated_Unity_NetCode_GhostOwnerGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr, indices[0], snapshotPtr, ref snapshotOffset);
            new Unity_NetCode_Generated_Unity_Transforms.Unity_NetCode_Generated_Unity_Transforms_TransformDefaultVariantGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[1], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[2].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr, indices[2], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[3].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr, indices[3], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[4].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo3GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[4], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[5].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[5], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[6].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[6], snapshotPtr, ref snapshotOffset);
            CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
                ref ghostChunkComponentTypesPtr[indices[7].ComponentIndex], enableBits, ref maskOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo3GhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[7], snapshotPtr, ref snapshotOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_InterpolatedOnlyCompGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[8], snapshotPtr, ref snapshotOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_OwnerOnlyCompGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[9], snapshotPtr, ref snapshotOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer().CopyBufferToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[10], snapshotPtr, ref snapshotOffset, ref dynamicSnapshotOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer().CopyBufferToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[11], snapshotPtr, ref snapshotOffset, ref dynamicSnapshotOffset);
            new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf3GhostComponentSerializer().CopyBufferToSnapshot(chunk, ref context,
                ghostChunkComponentTypesPtr,indices[12], snapshotPtr, ref snapshotOffset, ref dynamicSnapshotOffset);

            //CHILD COMPONENTS
            var linkedGroup = chunk.GetBufferAccessor(ref context.linkedEntityGroupTypeHandle);
            for (int ent = context.startIndex; ent < context.endIndex; ++ent)
            {
                var childEnableMaskOffset = maskOffset;
                var childSnapshotOffset = snapshotOffset;

                //GENERATE ONLY THIS
                var childEnt = linkedGroup[ent][1].Value;
                var childEntityStorageInfo = context.childEntityLookup[childEnt];
                CustomGhostSerializerHelpers.CopyEnableBits(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, childEntityStorageInfo.IndexInChunk+1, context.snapshotStride,
                    ref ghostChunkComponentTypesPtr[indices[13].ComponentIndex], enableBits, ref childEnableMaskOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer().CopyChildComponentToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[13], snapshotPtr, ref childSnapshotOffset);
                CustomGhostSerializerHelpers.CopyEnableBits(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, childEntityStorageInfo.IndexInChunk+1, context.snapshotStride,
                    ref ghostChunkComponentTypesPtr[indices[14].ComponentIndex], enableBits, ref childEnableMaskOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer().CopyChildComponentToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[14], snapshotPtr, ref childSnapshotOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer().CopyChildBufferToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[15], snapshotPtr, ref childSnapshotOffset, ref dynamicSnapshotOffset);

                childEnt = linkedGroup[ent][2].Value;
                childEntityStorageInfo = context.childEntityLookup[childEnt];
                CustomGhostSerializerHelpers.CopyEnableBits(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, childEntityStorageInfo.IndexInChunk+1, context.snapshotStride,
                    ref ghostChunkComponentTypesPtr[indices[16].ComponentIndex], enableBits, ref childEnableMaskOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer().CopyChildComponentToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[16], snapshotPtr, ref childSnapshotOffset);
                CustomGhostSerializerHelpers.CopyEnableBits(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, childEntityStorageInfo.IndexInChunk+1, context.snapshotStride,
                    ref ghostChunkComponentTypesPtr[indices[17].ComponentIndex], enableBits, ref childEnableMaskOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer().CopyChildComponentToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[17], snapshotPtr, ref childSnapshotOffset);
                new CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer().CopyChildBufferToSnapshot(childEntityStorageInfo.Chunk, childEntityStorageInfo.IndexInChunk, ref context,
                    ghostChunkComponentTypesPtr,indices[18], snapshotPtr, ref childSnapshotOffset, ref dynamicSnapshotOffset);

                Assert.IsTrue(childEnableMaskOffset <= typeData.EnableableBits);

                snapshotPtr += context.snapshotStride;
                enableBits += context.snapshotStride;
            }
        }

        private static unsafe void SerializeWithSingleBaseline(IntPtr snapshotData, int snapshotOffset,
            SendToOwnerType sendToOwnerMask, GhostSendType sendTypeMask,
            GhostCollectionComponentIndex* indices,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel, IntPtr baseline0Ptr,
            IntPtr changeMaskData,
            IntPtr dynamicSnapshotData, IntPtr baselineDynamicData, ref int dynamicDataSizePerEntity,
            int* compBitSize, int compBitSizeStride)
        {
            var changeMaskOffset = 0;
            //GENERATE ONLY THIS
            compBitSize[0*compBitSizeStride] = default(Unity_NetCode_Generated_Unity_NetCode.Unity_NetCode_Generated_Unity_NetCode_GhostOwnerGhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[1*compBitSizeStride] = default(Unity_NetCode_Generated_Unity_Transforms.Unity_NetCode_Generated_Unity_Transforms_TransformDefaultVariantGhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[2*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[3*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[4*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo3GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[5*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[6*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[7*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo3GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[8*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_InterpolatedOnlyCompGhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel, (int)(indices[8].SendMask & sendTypeMask) | (int)(indices[8].SendToOwner & sendToOwnerMask));
            compBitSize[9*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_OwnerOnlyCompGhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel, (int)(indices[9].SendMask & sendTypeMask) | (int)(indices[9].SendToOwner & sendToOwnerMask));
            compBitSize[10*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[11*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[12*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf3GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[13*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[14*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[15*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[16*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[17*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer).SerializeComponentSingleBaseline(snapshotData,baseline0Ptr,changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref writer, compressionModel);
            compBitSize[18*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
        }

        private static unsafe void SerializeWithThreeBaselines(IntPtr snapshotData, int snapshotOffset,
            SendToOwnerType sendToOwnerMask, GhostSendType sendTypeMask,
            GhostCollectionComponentIndex* indices,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel,
            IntPtr baseline0Ptr, IntPtr baseline1Ptr, IntPtr baseline2Ptr, IntPtr changeMaskData,
            IntPtr dynamicSnapshotData, IntPtr baselineDynamicData, ref int dynamicDataSizePerEntity,
            int* compBitSize, int compBitSizeStride)
        {
            var predictor = new GhostDeltaPredictor(
                new NetworkTick { SerializedData = GhostComponentSerializer.TypeCast<uint>(snapshotData) },
                new NetworkTick { SerializedData = GhostComponentSerializer.TypeCast<uint>(baseline0Ptr) },
                new NetworkTick { SerializedData = GhostComponentSerializer.TypeCast<uint>(baseline1Ptr) },
                new NetworkTick { SerializedData = GhostComponentSerializer.TypeCast<uint>(baseline2Ptr) });
            var changeMaskOffset = 0;

            //GENERATE ONLY THIS
            compBitSize[0*compBitSizeStride] = default(Unity_NetCode_Generated_Unity_NetCode.Unity_NetCode_Generated_Unity_NetCode_GhostOwnerGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[1*compBitSizeStride] = default(Unity_NetCode_Generated_Unity_Transforms.Unity_NetCode_Generated_Unity_Transforms_TransformDefaultVariantGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[2*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[3*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[4*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo3GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[5*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[6*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[7*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo3GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[8*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_InterpolatedOnlyCompGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel, (int)(indices[8].SendMask & sendTypeMask) | (int)(indices[8].SendToOwner & sendToOwnerMask));
            compBitSize[9*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_OwnerOnlyCompGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel, (int)(indices[9].SendMask & sendTypeMask) | (int)(indices[9].SendToOwner & sendToOwnerMask));
            compBitSize[10*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[11*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[12*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf3GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
            compBitSize[13*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo1GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[14*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo1GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[15*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf1GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity,ref writer, compressionModel);
            compBitSize[16*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_IntCompo2GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[17*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_FloatCompo2GhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,baseline1Ptr, baseline2Ptr, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
            compBitSize[18*compBitSizeStride] = default(CustomSerializer_Generated_Samples_CustomChunkSerializer.CustomSerializer_Generated_Samples_CustomChunkSerializer_Buf2GhostComponentSerializer).SerializeBuffer(snapshotData,baseline0Ptr, dynamicSnapshotData, baselineDynamicData, changeMaskData,ref changeMaskOffset, ref snapshotOffset, ref dynamicDataSizePerEntity, ref writer, compressionModel);
        }
    }
}
