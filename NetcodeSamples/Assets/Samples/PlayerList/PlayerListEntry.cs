using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     存储给定“NetworkConnection”entity 的 <see cref="ChangedRpc.UpdateType"/>。
    ///     请参见 <see cref="ServerPlayerListSystem" /> 和 <see cref="ClientPlayerListSystem" />。
    /// </summary>
    public struct PlayerListEntry : ICleanupComponentData
    {
        /// <remarks>Sent by client.</remarks>
        public struct ClientRegisterUsernameRpc : IRpcCommand
        {
            public FixedString64Bytes Value;
        }

        /// 如果 clients 用户名是 invalid.</summary>，则 <summary>Sent 由 server 到 client
        public struct InvalidUsernameResponseRpc : IRpcCommand
        {
            public FixedString64Bytes RequestedUsername;
        }

        /// <remarks>Sent 由 server 任何时候 clients 用户名或状态 changes.</remarks>
        public struct ChangedRpc : IRpcCommand
        {
            /// <inheritdoc cref="Reason" />
            public bool IsConnected => ChangeType != UpdateType.PlayerDisconnect;

            public enum UpdateType : byte
            {
                /// <summary>A 玩家（我知道的）disconnected.</summary>
                PlayerDisconnect = 0,
                /// <summary>This 是新加入者，加入了 AFTER me.</summary>
                NewJoiner,
                /// <summary>This 是现有玩家，已加入游戏 BEFORE me.</summary>
                ExistingPlayer,
                /// <summary>A 玩家更改了 username.</summary>
                UsernameChange,
            }

            public UpdateType ChangeType;

            /// <remarks>Client 无法推断 NetworkId，因为此 RPC 是从 server.</remarks> 发送的
            public int NetworkId;

            public ClientRegisterUsernameRpc Username;

            /// <summary>
            ///     <see cref="IsConnected" /> 时无效。存储最后一次断开连接的原因/导致此 client，允许
            ///     其他玩家显示原因。
            /// </summary>
            public NetworkStreamDisconnectReason Reason;
        }

        /// <summary>Stores 最后收到的此 player.</summary> 的 RPC
        public ChangedRpc State;

        public bool IsCreated => State.NetworkId != default;
    }
}
