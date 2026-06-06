using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode.Samples
{
    /// <summary>
    /// Component 添加到所有 ghosts 中以标记它们已被预处理。
    /// </summary>
    public struct ClientOnlyProcessed : IComponentData
    {
    }

    /// <summary>
    /// 当 prediction 循环内仅存在 client 数据要备份时，Component 添加到所有 ghosts。
    /// </summary>
    internal struct ClientOnlyBackup : ICleanupComponentData, IDisposable
    {
        //包含 client 上存在的 component/缓冲区数据的原始副本
        //DATA LAYOUT
        // [勾选（32 位）][enablebits（对齐 4 字节）]... 填充。. | [计算数据]
        // compdata 从 16 字节对齐边界开始（为了 simd 访问）
        public UnsafeList<byte> ComponentBackup;
        /// <summary>
        /// 我们正在写入的下一个备份槽。
        /// </summary>
        private short m_backupWrPtr;
        /// <summary>
        /// 我们正在读取的下一个备份槽。
        /// </summary>
        private short m_backupRdPtr;
        /// <summary>
        /// 备份缓冲区中可用的备份槽数
        /// </summary>
        private short m_backupCapacity;
        /// <summary>
        /// 备份缓冲区中可用的备份槽数
        /// </summary>
        private short m_backupSlotUsed;

        /// <summary>
        /// 创建并初始化具有指定容量的仅 client 备份 component 缓冲区。
        /// </summary>
        /// <param name="slotSize"></param>
        /// <param name="capacity"></param>
        public ClientOnlyBackup(int slotSize, int capacity=32)
        {
            ComponentBackup = new UnsafeList<byte>(capacity*slotSize, Allocator.Persistent);
            for (int i=0;i<ComponentBackup.Length;++i)
            {
                ComponentBackup.ElementAt(i) = 0x1f;
            }
            m_backupCapacity = (short)capacity;
            m_backupSlotUsed = 0;
            m_backupWrPtr = 0;
            m_backupRdPtr = 0;
        }

        /// <summary>
        /// 释放 component 备份资源
        /// </summary>
        /// <returns></returns>
        public void Dispose()
        {
            ComponentBackup.Dispose();
        }

        /// <summary>
        /// 返回存储仅 client 的 component 使能位所需的 uint 数量。
        /// </summary>
        /// <param name="numComponents"></param>
        /// <returns></returns>
        private static int EnableBitIntSize(int numComponents)
        {
            return (numComponents + 31) / 32;
        }
        /// <summary>
        /// 保留空间的大小（以字节为单位）
        /// </summary>
        /// <param name="numComponents"></param>
        /// <returns></returns>
        internal static int EnableBitByteSize(int numComponents)
        {
            return sizeof(int) * EnableBitIntSize(numComponents);
        }


        public bool IsEmpty => m_backupSlotUsed == 0;
        public bool IsFull => m_backupSlotUsed == m_backupCapacity;
        public int UsedSlot => m_backupSlotUsed;
        public int Capacity => m_backupCapacity;

        public void Clear()
        {
            m_backupSlotUsed = 0;
            m_backupRdPtr = 0;
            m_backupWrPtr = 0;
        }

        public void Resize(int newCapacity, int slotSize)
        {
            if(newCapacity == m_backupCapacity)
                return;

            var oldBufferLength = ComponentBackup.Length;
            ComponentBackup.Resize(newCapacity*slotSize);
            m_backupCapacity = (short)newCapacity;
            //将环绕部分移动到最新分配的区域，使得 m_backupWrPtr > m_backupRdPtr
            //   |哇哇哇 | | r r r r r | | n n n n n n n n | |
            // 变成
            //   | - - - - - | | r r r r r | WW W W n n n n n |
            //                                      ^ --- 新的写入位置
            // 这只是最小化所需的内存移动次数
            // 理想情况下，当我们调整大小时，我们不希望有另一个 memcpy。但不幸的是，这是不可避免的，因为
            // 我们无法控制 UnsafeList 缓冲区重新分配。
            if (m_backupWrPtr <= m_backupRdPtr)
            {
                //将数据从 0 向上移动到 backupWr 指针前面
                if (m_backupWrPtr > 0)
                {
                    unsafe
                    {
                        var source = ComponentBackup.Ptr;
                        var dest = ComponentBackup.Ptr + oldBufferLength;
                        UnsafeUtility.MemMove(dest, source, m_backupWrPtr*slotSize);
                    }
                }
                //始终将 m_backupWrPtr 移到前面
                m_backupWrPtr = (short)(m_backupRdPtr + m_backupSlotUsed);
            }
        }

        public void GrowBufferIfFull(int slotSize)
        {
            if (m_backupSlotUsed == m_backupCapacity)
            {
                //长到两倍大
                Resize(m_backupCapacity * 2, slotSize);
            }
        }

        //返回可用于写入备份的插槽。在内部将环形缓冲区头推进到新位置
        public int AcquireBackupSlot()
        {
            Assertions.Assert.IsTrue(m_backupSlotUsed < m_backupCapacity);
            var current = m_backupWrPtr;
            //将备份指针前进到下一个位置
            m_backupWrPtr = (short)((m_backupWrPtr + 1) % m_backupCapacity);
            ++m_backupSlotUsed;
            return current;
        }

        //消耗所有具有小于或等于目标刻度的备份槽。减少消耗的缓冲区槽位和
        //提前读取位置。
        public void RemoveBackupsOlderThan(NetworkTick targetTick, int backupSize)
        {
            if (m_backupSlotUsed == 0)
                return;

            unsafe
            {
                var ptr = ComponentBackup.Ptr + m_backupRdPtr*backupSize;
                var tick = default(NetworkTick);
                while (m_backupSlotUsed > 0)
                {
                    tick.SerializedData = *(uint*)ptr;
                    if (!tick.IsValid || tick.IsNewerThan(targetTick))
                        break;
                    *(uint*)ptr = 0;
                    --m_backupSlotUsed;
                    ++m_backupRdPtr;
                    if (m_backupRdPtr >= m_backupCapacity)
                    {
                        m_backupRdPtr = 0;
                        ptr = ComponentBackup.Ptr;
                    }
                    else
                    {
                        ptr += backupSize;
                    }
                }
            }
        }

        // return the slot for the predictionStartTick or -1 if not found
        public readonly int GetSlotForTick(NetworkTick predictionStartTick, int backupSize)
        {
            Assertions.Assert.IsTrue(m_backupSlotUsed > 0);
            unsafe
            {
                var oldestBackupPtr = ComponentBackup.Ptr + m_backupRdPtr*backupSize;
                var tick = new NetworkTick{SerializedData = *(uint*)oldestBackupPtr};
                //如果刻度线相等，则使用此插槽
                if (tick == predictionStartTick)
                    return m_backupRdPtr;

                //如果我们拥有的最旧的刻度线较新，则只需使用此刻度线作为最佳近似值即可。无论如何，缓冲区中不存在较旧的蜱虫
                if (tick.IsNewerThan(predictionStartTick))
                    return m_backupRdPtr;

                //在正常情况下，client 领先于 server。
                //PredictionStartTick 通常应小于当前模拟报价。因此它应该在缓冲区中。
                //但是，如果 client 有点落后（例如：游戏连接初始或正在尝试缓存）
                //我们所做的最新模拟报价可能少于 server 收到的最新 snapshot。
                //如果由于任何原因 PredictionStartTick 大于我们拥有的最新备份刻度，则从备份恢复
                //没有意义，因为我们拥有的最佳近似值（就 prediction 而言）是 components 的当前状态
                var lastWrittenSlot = (m_backupWrPtr - 1) % m_backupCapacity;
                var latestBackupPtr = ComponentBackup.Ptr + lastWrittenSlot*backupSize;
                var latestTick = new NetworkTick{SerializedData = *(uint*)latestBackupPtr};
                if (predictionStartTick.IsNewerThan(latestTick))
                    return -1;

                //计算增量并将备份点移动到所需的插槽。
                int delta = predictionStartTick.TicksSince(tick);
                var slotIndex = (m_backupRdPtr + delta) % m_backupCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                tick.SerializedData = *(uint*)(ComponentBackup.Ptr + slotIndex*backupSize);
                //勾选应该是一样的
                Assertions.Assert.IsTrue(tick==predictionStartTick);
#endif
                return slotIndex;
            }
        }
    }

    /// <summary>
    /// 存储有关 component 类型（大小和类型）的信息以及用于检索的索引
    /// <see cref="Unity.Entities.DynamicComponentTypeHandle"/> 来自 <see cref="ClientOnlyCollection"/>。
    /// </summary>
    internal struct ClientOnlyBackupInfo
    {
        public ComponentType ComponentType;
        //备份内 component 的大小。如果 component 类型是缓冲区，则它是单个元素大小。
        public int ComponentSize;
        //client-only components 集合内的索引。用于检索对应的
        //动态 component 型手柄。
        public int ComponentIndex;
        //层次结构中的 entity 索引。0 是根。
        public int EntityIndex;
    }

    /// <summary>
    /// ClientOnlyBackupMetadata 结构体与包含仅包含 client 数据的每个 prefab 关联，它用于检索
    /// 对于 clientOnlyBackupInfo 集合内给定的 ghost 类型，ClientOnlyBackupInfo 列表。
    /// </summary>
    internal struct ClientOnlyBackupMetadata
    {
        //ClientOnlyBackupInfo 集合中的 [start, end) 范围。
        public int componentBegin;
        public int componentEnd;
        //根 entity 中的 num component
        public int numRootComponents;
        //component 备份的大小（固定）。它包括刻度、使能位和所有 component 数据。请参阅 ClientOnlyBackup 数据布局
        public int backupSize;
    }
}
