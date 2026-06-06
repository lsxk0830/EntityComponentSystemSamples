using System;
using Unity.Entities;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     与 <see cref="PlayerListEntry"/> 类似，但 <see cref="IBufferElementData"/> 除外，允许 client 将此数据存储在缓冲区中。
    ///     存储所有玩家及其状态（用户名、ping 等）。
    ///     索引映射到 NetworkId - 1。
    /// </summary>
    /// <remarks>
    ///     因此，这是自动排序的并且是确定性的。
    ///     将包含 NetworkId 尚未被重新使用的已断开连接的玩家。
    ///     当列表大小调整为一些备用容量时，还将包含默认条目。
    ///     取决于 NetworkId 的隐含规则。
    ///     列表用于允许隐式调整大小而无需释放。
    /// </remarks>
    public struct PlayerListBufferEntry : IBufferElementData
    {
        /// <summary>Stores 最后收到的此 player.</summary> 的 RPC
        public PlayerListEntry.ChangedRpc State;

        public bool IsCreated => State.NetworkId != default;

        /// <summary>
        /// 返回已连接玩家的数量。
        /// 为什么？缓冲区还可以包含断开连接的玩家。
        /// </summary>
        /// <param name="playerListBufferEntries"></param>
        /// <returns></returns>
        public static int CountNumConnectedPlayers(DynamicBuffer<PlayerListBufferEntry> playerListBufferEntries)
        {
            var count = 0;
            foreach (var entry in playerListBufferEntries)
            {
                if (entry.IsCreated && entry.State.IsConnected) count++;
            }
            return count;
        }
    }
}
