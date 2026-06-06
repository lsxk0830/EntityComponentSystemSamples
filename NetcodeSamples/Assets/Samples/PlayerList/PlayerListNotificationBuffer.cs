using System;
using System.Diagnostics;
using Unity.Entities;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     存储玩家加入和断开连接事件的排序列表（I.e。通知）。
    ///     活动持续时间为 <see cref="EnablePlayerListsFeature.EventListEntryDurationSeconds" />。
    /// </summary>
    public struct PlayerListNotificationBuffer : IBufferElementData
    {
        public PlayerListEntry.ChangedRpc Event;
        /// <summary>Via <see cref="Stopwatch.GetTimestamp"/></summary>
        public float DurationLeft;
    }
}
