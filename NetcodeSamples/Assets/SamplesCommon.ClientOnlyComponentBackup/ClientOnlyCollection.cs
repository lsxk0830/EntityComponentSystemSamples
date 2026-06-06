using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.Samples
{
    /// <summary>
    /// 包含所有已注册的仅 client 的 component 类型和 prefab 元数据的单例。
    /// <para>
    /// client 仅备份 systems 仅备份注册到 component 的状态
    /// <see cref="ClientOnlyCollection"/>。components <b>must</b> 在“进入”游戏之前注册。
    /// </para>
    /// </summary>
    public struct ClientOnlyCollection : IComponentData
    {
        internal NativeList<ComponentType> ClientOnlyComponentTypes;
        internal NativeList<ClientOnlyBackupInfo> BackupInfoCollection;
        internal NativeHashMap<GhostType, ClientOnlyBackupMetadata> GhostTypeToPrefabMetadata;
        internal int ProcessedPrefabs;
        /// <summary>
        /// 用于检查 component 是否可以注册的标志。第一个 ghost prefab处理完毕后，
        /// 无法将其他 component 添加到集合中。
        /// </summary>
        internal bool CanRegisterComponents => ProcessedPrefabs == 0;

        /// <summary>
        /// 调用此方法将 component 注册为仅 client 并使其成为备份的一部分。
        /// 注册必须在游戏开始前完成（连接进入游戏）。
        /// 一个好的做法是创建一个 system，即在创建 <see cref="CreateAfterAttribute"/> 之后
        /// <see cref="ClientOnlyComponentBackupSystem"/>（以便可以访问单例）并注册一次 component。
        /// </summary>
        /// <param name="componentType"></param>
        public void RegisterClientOnlyComponentType(in ComponentType componentType)
        {
            if (!CanRegisterComponents)
                throw new InvalidOperationException("cannot register client-only component after prefabs has been processed or the connection is in game");
            if (ClientOnlyComponentTypes.IndexOf(componentType) >= 0)
                return;
            ClientOnlyComponentTypes.Add(componentType);
        }

        internal void ProcessGhostTypePrefab(GhostType ghostType, Entity entity, EntityManager entityManager)
        {
            int first = BackupInfoCollection.Length;
            int componentBackupSize = 0;
            int numRootComponents = 0;
            AddComponentForEntity(entityManager, entity, 0, ref componentBackupSize);
            numRootComponents = BackupInfoCollection.Length - first;
            if (entityManager.HasBuffer<LinkedEntityGroup>(entity))
            {
                var leg = entityManager.GetBuffer<LinkedEntityGroup>(entity);
                for (int i = 1; i < leg.Length; i++)
                    AddComponentForEntity(entityManager, leg[i].Value, i, ref componentBackupSize);
            }
            if (BackupInfoCollection.Length != first)
            {
                //添加勾选并启用位掩码数组到备份大小。缓冲区大小根据缓冲区动态重新计算
                //内容由 job 提供
                var enableBitsSize = ClientOnlyBackup.EnableBitByteSize(BackupInfoCollection.Length - first);
                var compDataStartOffset = GhostComponentSerializer.SnapshotSizeAligned(sizeof(int) + enableBitsSize);
                componentBackupSize = GhostComponentSerializer.SnapshotSizeAligned(componentBackupSize + compDataStartOffset);
                GhostTypeToPrefabMetadata.Add(ghostType, new ClientOnlyBackupMetadata
                {
                    componentBegin = first,
                    componentEnd = BackupInfoCollection.Length,
                    numRootComponents = numRootComponents,
                    backupSize = componentBackupSize,
                });
            }
        }

        private void AddComponentForEntity(EntityManager entityManager, Entity entity, int entityIndex, ref int backupSize)
        {
            using var componentTypes = entityManager.GetComponentTypes(entity);
            foreach (var componentType in componentTypes)
            {
                int index = ClientOnlyComponentTypes.IndexOf(componentType);
                if(index < 0)
                    continue;

                //这引入了一点冗余，但至少不需要两次内存读取来获取该数据
                var info = new ClientOnlyBackupInfo
                {
                    ComponentType = componentType,
                    ComponentSize = componentType.IsBuffer
                        ? TypeManager.GetTypeInfo(componentType.TypeIndex).ElementSize
                        : TypeManager.GetTypeInfo(componentType.TypeIndex).TypeSize,
                    ComponentIndex = index,
                    EntityIndex = entityIndex
                };
                BackupInfoCollection.Add(info);
                Unity.Assertions.Assert.IsFalse(componentType.IsBuffer);
                backupSize += TypeManager.GetTypeInfo(componentType.TypeIndex).TypeSize;
            }
        }
    }
}
