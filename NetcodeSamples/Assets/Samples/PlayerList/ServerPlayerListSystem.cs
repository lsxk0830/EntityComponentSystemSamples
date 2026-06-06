using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     管理 <see cref="PlayerListEntry" /> component 和 RPCs，
    ///     允许 clients 查看其他 clients 的名称和连接状态。
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ServerPlayerListSystem : ISystem
    {
        EntityArchetype m_InvalidUsernameRpcArchetype;
        EntityArchetype m_RpcArchetype;
        EntityQuery m_PlayerListQuery;
        ComponentLookup<PlayerListEntry> m_PlayerListEntryFromEntity;
        ComponentLookup<NetworkId> m_NetworkIdFromEntity;
        EntityQuery m_ClientRegisterUsernameRpcQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_PlayerListQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerListEntry>());

            var archetypeTypes = new NativeArray<ComponentType>(2, Allocator.Temp);
            archetypeTypes[0] = ComponentType.ReadOnly<PlayerListEntry.ChangedRpc>();
            archetypeTypes[1] = ComponentType.ReadOnly<SendRpcCommandRequest>();
            m_RpcArchetype = state.EntityManager.CreateArchetype(archetypeTypes);
            archetypeTypes[0] = ComponentType.ReadOnly<PlayerListEntry.InvalidUsernameResponseRpc>();
            m_InvalidUsernameRpcArchetype = state.EntityManager.CreateArchetype(archetypeTypes);
            archetypeTypes.Dispose();

            m_PlayerListEntryFromEntity = state.GetComponentLookup<PlayerListEntry>(true);
            m_NetworkIdFromEntity = state.GetComponentLookup<NetworkId>(true);

            m_ClientRegisterUsernameRpcQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerListEntry.ClientRegisterUsernameRpc>());

            state.RequireForUpdate<EnablePlayerListsFeature>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rpcArchetype = m_RpcArchetype;
            var invalidUsernameRpcArchetype = m_InvalidUsernameRpcArchetype;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var netDbg = SystemAPI.GetSingleton<NetDebug>();

            var connectionEventsForTick = SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick;
            if (connectionEventsForTick.Length > 0)
            {
                state.Dependency = new NotifyPlayersOfDisconnectsJob
                {
                    netDbg = netDbg,
                    ecb = ecb,
                    rpcArchetype = rpcArchetype,
                    connectionEventsForTick = connectionEventsForTick,
                    playerListEntryLookup = SystemAPI.GetComponentLookup<PlayerListEntry>(),
                }.Schedule(state.Dependency);
            }

            if (!m_ClientRegisterUsernameRpcQuery.IsEmptyIgnoreFilter)
            {
                // 我们只添加新玩家 IF 他们向我们发送用户名。
                // 这确保他们从一开始就始终拥有有效的用户名。
                m_PlayerListEntryFromEntity.Update(ref state);
                m_NetworkIdFromEntity.Update(ref state);
                var playerListEntries = m_PlayerListQuery.ToComponentDataListAsync<PlayerListEntry>(state.WorldUpdateAllocator, state.Dependency, out var gatherPlayerListsHandle);
                state.Dependency = new HandleNewJoinersJob
                {
                    ecb = ecb,
                    netDbg = netDbg,
                    rpcArchetype = rpcArchetype,
                    invalidUsernameRpcArchetype = invalidUsernameRpcArchetype,
                    playerListEntries = m_PlayerListEntryFromEntity,
                    networkIds = m_NetworkIdFromEntity,
                    existingPlayerListEntries = playerListEntries,
                }.Schedule(gatherPlayerListsHandle);
            }
        }

        [BurstCompile]
        public partial struct HandleNewJoinersJob : IJobEntity
        {
            public EntityCommandBuffer ecb;
            public EntityArchetype rpcArchetype;
            public EntityArchetype invalidUsernameRpcArchetype;
            public NetDebug netDbg;
            [ReadOnly]
            public ComponentLookup<PlayerListEntry> playerListEntries;
            [ReadOnly]
            public ComponentLookup<NetworkId> networkIds;
            [ReadOnly]
            public NativeList<PlayerListEntry> existingPlayerListEntries;

            public void Execute(Entity rpcEntity, ref PlayerListEntry.ClientRegisterUsernameRpc rpc, in ReceiveRpcCommandRequest req)
            {
                if (!networkIds.TryGetComponent(req.SourceConnection, out var networkId))
                {
                    netDbg.DebugLog("Server received a ClientRegisterUsernameRpc from a client who has since disconnected. Ignoring!");
                    return;
                }

                // 在这里自动修补而不是踢玩家，因为玩家不会选择他们的默认名称。
                var originalUsername = rpc.Value;
                rpc.Value = UsernameSanitizer.SanitizeUsername(rpc.Value, networkId.Value, out var usernameWasSanitized);

                if (usernameWasSanitized)
                    netDbg.LogError($"Server received a PlayerListEntry.ClientRegisterUsernameRpc with an invalid username '{originalUsername}', sanitized to '{rpc.Value}'!");

                // Note ClientRegisterUsernameRpc 可以表示：
                if (!playerListEntries.TryGetComponent(req.SourceConnection, out var entry))
                {
                    // NEW JOINER：
                    entry.State = new PlayerListEntry.ChangedRpc
                    {
                        ChangeType = PlayerListEntry.ChangedRpc.UpdateType.NewJoiner,
                        Reason = default,
                        NetworkId = networkId.Value,
                        Username = rpc
                    };

                    ecb.AddComponent(req.SourceConnection, entry);
                    NotifyJoinerOfAllExistingPlayers(ref existingPlayerListEntries, ref ecb, in rpcArchetype, in req.SourceConnection);
                }
                else
                {
                    // AN EXISTING PLAYER 使用新用户名：
                    if (entry.State.Username.Value == rpc.Value)
                    {
                        netDbg.LogWarning($"Server received a PlayerListEntry.ChangedRpc from existing player {entry.State.NetworkId} but username '{rpc.Value}' is identical to cached value. Ignoring.");
                        ecb.DestroyEntity(rpcEntity);
                        return;
                    }

                    netDbg.DebugLog($"Server received a PlayerListEntry.ChangedRpc from an already connected player {entry.State.NetworkId}! Broadcasting the rename ('{entry.State.Username.Value}' >>> '{rpc.Value}').");
                    entry.State.ChangeType = PlayerListEntry.ChangedRpc.UpdateType.UsernameChange;
                    entry.State.Username = rpc;

                    // 更新 Servers 缓存条目。
                    ecb.SetComponent(req.SourceConnection, entry);
                }

                // 通过重新利用 RPC 来广播用户名通知。
                // 我们只需要在与其他 clients 相关时进行广播：
                // 否则我们只会发回给发件人。
                ecb.RemoveComponent<PlayerListEntry.ClientRegisterUsernameRpc>(rpcEntity);
                ecb.RemoveComponent<ReceiveRpcCommandRequest>(rpcEntity);

                ecb.AddComponent(rpcEntity, entry.State);
                ecb.AddComponent<SendRpcCommandRequest>(rpcEntity);

                // 通知发送者他们的原始输入变成了这个新的、经过净化的输入，以便他们可以接受 servers 值。
                if (usernameWasSanitized)
                {
                    var clientInvalidUsernameRpc = ecb.CreateEntity(invalidUsernameRpcArchetype);
                    ecb.AddComponent(clientInvalidUsernameRpc, new PlayerListEntry.InvalidUsernameResponseRpc
                    {
                        RequestedUsername = originalUsername
                    });
                    ecb.SetComponent(clientInvalidUsernameRpc, new SendRpcCommandRequest
                    {
                        TargetConnection = req.SourceConnection
                    });
                }
            }
        }

        [BurstCompile]
        public partial struct NotifyPlayersOfDisconnectsJob : IJob
        {
            public NetDebug netDbg;
            public EntityCommandBuffer ecb;
            public EntityArchetype rpcArchetype;
            public NativeArray<NetCodeConnectionEvent>.ReadOnly connectionEventsForTick;
            public ComponentLookup<PlayerListEntry> playerListEntryLookup;
            public void Execute()
            {
                foreach (var evt in connectionEventsForTick)
                {
                    if (evt.State == ConnectionState.State.Disconnected)
                    {
                        // 如果尚未添加 PlayerListEntry，请忽略。
                        if (!playerListEntryLookup.TryGetRefRW(evt.ConnectionEntity, out var entryRef)) { continue; }
                        ref var entry = ref entryRef.ValueRW;

                        netDbg.DebugLog($"Server: Established player {evt.ConnectionId} disconnected with reason {evt.DisconnectReason}! Notifying other players.");

                        entry.State.Reason = evt.DisconnectReason;
                        entry.State.ChangeType = PlayerListEntry.ChangedRpc.UpdateType.PlayerDisconnect;

                        // 广播状态通知：
                        var rpcEntity = ecb.CreateEntity(rpcArchetype);
                        ecb.SetComponent(rpcEntity, entry.State);

                        // 清理清理 component（同时也会将 trigger entity 删除）。
                        ecb.RemoveComponent<PlayerListEntry>(evt.ConnectionEntity);
                    }
                }
            }
        }

        static void NotifyJoinerOfAllExistingPlayers(ref NativeList<PlayerListEntry> existingPlayers, ref EntityCommandBuffer ecb, in EntityArchetype newJoinerArchetype, in Entity targetConnection)
        {
            for (var i = 0; i < existingPlayers.Length; i++)
            {
                var notifyOthersRpc = ecb.CreateEntity(newJoinerArchetype);
                var changedRpc = existingPlayers[i].State;
                changedRpc.ChangeType = PlayerListEntry.ChangedRpc.UpdateType.ExistingPlayer;
                ecb.SetComponent(notifyOthersRpc, changedRpc);
                ecb.SetComponent(notifyOthersRpc, new SendRpcCommandRequest
                {
                    TargetConnection = targetConnection
                });
            }
        }
    }
}
