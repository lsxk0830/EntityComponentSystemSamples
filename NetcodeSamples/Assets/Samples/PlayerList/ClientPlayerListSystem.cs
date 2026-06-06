using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     收到<see cref="PlayerListEntry" /> RPC 的，通知本 client 其他 clients 的 PRESENCE。
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct ClientPlayerListSystem : ISystem, ISystemStartStop
    {
        EntityArchetype m_UsernameRpcArchetype;
        EntityQuery m_InvalidUsernameResponseRpc;
        EntityQuery m_DesiredUsernameChangedQuery;

        public void OnCreate(ref SystemState state)
        {
            OnCreateBurstCompatible(ref state);

            ref var desiredUsernameStore = ref SystemAPI.GetSingletonRW<DesiredUsername>().ValueRW;
            if (desiredUsernameStore.Value.IsEmpty) desiredUsernameStore.Value = UsernameSanitizer.GetDefaultUsername(state.World);

            m_InvalidUsernameResponseRpc = state.GetEntityQuery(ComponentType.ReadOnly<PlayerListEntry.InvalidUsernameResponseRpc>());

            m_DesiredUsernameChangedQuery = state.GetEntityQuery(ComponentType.ReadWrite<DesiredUsername>());
            m_DesiredUsernameChangedQuery.AddChangedVersionFilter(ComponentType.ReadWrite<DesiredUsername>());
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            var desiredUsername = SystemAPI.GetSingletonRW<DesiredUsername>().ValueRW;
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var localPlayerNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;
            var players = SystemAPI.GetSingletonBuffer<PlayerListBufferEntry>();
            ref var entry = ref GetOrCreateEntry(players, localPlayerNetworkId);

            netDebug.DebugLog($"Client {localPlayerNetworkId} has connected, so sending their DesiredUsername '{desiredUsername.Value}' to the server!");
            SendUsernameRpc(ref state, ref desiredUsername, ref entry, "SetUsernameRpc");
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            // 言下之意就是我们断绝了联系。
            SystemAPI.GetSingletonBuffer<PlayerListBufferEntry>().Clear();
        }

        [BurstCompile]
        void OnCreateBurstCompatible(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<DesiredUsername>())
            {
                state.EntityManager.CreateSingleton<DesiredUsername>();
            }

            state.RequireForUpdate<EnablePlayerListsFeature>();
            state.RequireForUpdate<NetworkId>();

            var componentTypes = new NativeArray<ComponentType>(2, Allocator.Temp);
            componentTypes[0] = ComponentType.ReadWrite<PlayerListEntry.ClientRegisterUsernameRpc>();
            componentTypes[1] = ComponentType.ReadWrite<SendRpcCommandRequest>();
            m_UsernameRpcArchetype = state.EntityManager.CreateArchetype(componentTypes);
            componentTypes.Dispose();

            state.EntityManager.CreateSingletonBuffer<PlayerListBufferEntry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var localPlayerNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;
            var players = SystemAPI.GetSingletonBuffer<PlayerListBufferEntry>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // 处理用户名 RPC：
            if(!m_DesiredUsernameChangedQuery.IsEmpty)
            {
                var desiredUsername = SystemAPI.GetSingletonRW<DesiredUsername>().ValueRW;
                ref var localPlayerEntry = ref GetOrCreateEntry(players, localPlayerNetworkId);
                if (localPlayerEntry.State.Username.Value != desiredUsername.Value)
                {
                    netDebug.DebugLog($"Client {localPlayerNetworkId} has changed their DesiredUsername '{desiredUsername.Value}', so notifying server of change.");
                    SendUsernameRpc(ref state, ref desiredUsername, ref localPlayerEntry, "NotifyUsernameChangedRpc");
                }
            }

            new HandleReceivedStateChangedRpcJob
            {
                ecb = ecb,
                players = players,
                netDebug = netDebug,
                localPlayerNetworkId = localPlayerNetworkId
            }.Schedule();

            // 处理无效的用户名响应：
            if(!m_InvalidUsernameResponseRpc.IsEmptyIgnoreFilter)
            {
                using var rpcs = m_InvalidUsernameResponseRpc.ToComponentDataArray<PlayerListEntry.InvalidUsernameResponseRpc>(Allocator.Temp);
                foreach (var rpc in rpcs)
                {
                    ref var entry = ref GetOrCreateEntry(players, localPlayerNetworkId);
                    var desiredUsernameStore = SystemAPI.GetSingletonRW<DesiredUsername>().ValueRW;

                    // Note 如果用户已经更改了用户名 AGAIN，则应忽略此无效响应（因为我们已经发送了另一个用户名更改请求 RPC）。
                    if (desiredUsernameStore.Value == rpc.RequestedUsername)
                    {
                        desiredUsernameStore.Value = entry.State.Username.Value;
                        netDebug.LogError($"Local player received InvalidUsernameResponseRpc for '{rpc.RequestedUsername}'. Using '{entry.State.Username.Value}'!");
                    }
                    else netDebug.LogError($"Local player received InvalidUsernameResponseRpc for '{rpc.RequestedUsername}', but user attempting '{desiredUsernameStore.Value}'!");
                }
                state.EntityManager.DestroyEntity(m_InvalidUsernameResponseRpc);
            }
        }

        void SendUsernameRpc(ref SystemState state, ref DesiredUsername desiredUsername, ref PlayerListEntry localPlayerEntry, in FixedString64Bytes rpcName)
        {
            localPlayerEntry.State.Username.Value = desiredUsername.Value = UsernameSanitizer.SanitizeUsername(desiredUsername.Value, localPlayerEntry.State.NetworkId, out _);

            var rpcEntity = state.EntityManager.CreateEntity(m_UsernameRpcArchetype);
            state.EntityManager.SetName(rpcEntity, rpcName);
            state.EntityManager.SetComponentData(rpcEntity, new PlayerListEntry.ClientRegisterUsernameRpc
            {
                Value = desiredUsername.Value
            });
        }

        [BurstCompile]
        [WithAll(typeof(ReceiveRpcCommandRequest))]
        public partial struct HandleReceivedStateChangedRpcJob : IJobEntity
        {
            public EntityCommandBuffer ecb;
            public DynamicBuffer<PlayerListBufferEntry> players;
            public NetDebug netDebug;
            public int localPlayerNetworkId;

            public void Execute(Entity rpcEntity, in PlayerListEntry.ChangedRpc rpc)
            {
                ecb.DestroyEntity(rpcEntity);

                ref var entry = ref GetOrCreateEntry(players, rpc.NetworkId);
                netDebug.DebugLog($"Client {localPlayerNetworkId} received PlayerListEntry.StateChangedRpc: {PlayerListDebugUtils.ToFixedString(entry.State)} >>> {PlayerListDebugUtils.ToFixedString(rpc)}!");
                entry.State = rpc;
            }
        }

        /// <summary>
        ///     因为我们将条目存储在列表中，所以获取条目涉及：
        ///     1、保证阵列容量。
        ///     2. 返回条目的引用。
        ///     Note 默认条目有效。
        /// </summary>
        static unsafe ref PlayerListEntry GetOrCreateEntry(DynamicBuffer<PlayerListBufferEntry> players, int networkId)
        {
            var delta = networkId - players.Length;
            if (delta > 0)
                players.AddRange(new NativeArray<PlayerListBufferEntry>(delta, Allocator.Temp));

            return ref UnsafeUtility.ArrayElementAsRef<PlayerListEntry>(players.GetUnsafePtr(), networkId - 1);
        }
    }
}
