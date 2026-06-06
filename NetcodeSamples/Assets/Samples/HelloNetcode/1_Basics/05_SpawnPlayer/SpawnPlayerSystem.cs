using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.Transforms;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 标志 component，表示是否已生成玩家角色控制器 (CC)
    /// 对于给定的连接。
    /// </summary>
    public struct PlayerSpawned : IComponentData { }

    public struct PlayerReconnected : IComponentData { }

    /// <summary>
    ///     方便：这允许我们轻松获取与关联的连接 entity
    ///     该玩家角色控制器 entity。
    /// </summary>
    public struct ConnectionOwner : IComponentData
    {
        public Entity Entity;
    }

    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SpawnPlayerSystem : ISystem
    {
        private EntityQuery m_NewPlayersQuery;
        private EntityQuery m_ExistingPlayersQuery;
        private EntityQuery m_ReconnectedPlayersQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 必须等待生成器 entity scene 流入，
            // 在此示例中，这很可能是瞬时的（但可以肯定）。
            state.RequireForUpdate<Spawner>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<EnableSpawnPlayer, EnableRemotePredictedPlayer>().Build());
            state.RequireForUpdate<NetworkStreamInGame>();
            // 不要将 run 新玩家流程用于重新连接的连接，它们将在下面的重新连接 query 中处理
            m_NewPlayersQuery = SystemAPI.QueryBuilder().WithAll<NetworkId>().WithNone<PlayerSpawned>().WithNone<NetworkStreamIsReconnected>().Build();
            m_ReconnectedPlayersQuery = SystemAPI.QueryBuilder().
                WithAll<NetworkId, NetworkStreamIsReconnected, PlayerSpawned>().
                WithNone<PlayerReconnected>().Build();
            m_ExistingPlayersQuery = SystemAPI.QueryBuilder().WithAll<GhostOwner>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_NewPlayersQuery.IsEmptyIgnoreFilter && m_ReconnectedPlayersQuery.IsEmptyIgnoreFilter)
                return;

            var prefab = SystemAPI.GetSingleton<Spawner>().Player;
            state.EntityManager.GetName(prefab, out var prefabName);
            if (prefabName.IsEmpty) prefabName = prefab.ToFixedString();

            // 在新的 server 上应用主机迁移时，请勿尝试生成新玩家
            if (SystemAPI.HasSingleton<HostMigrationInProgress>())
                return;

            var reconnectedPlayers = m_ReconnectedPlayersQuery.ToEntityArray(Allocator.Temp);
            var reconnectedIds = m_ReconnectedPlayersQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            for (var i = 0; i < reconnectedPlayers.Length; i++)
            {
                var networkId = reconnectedIds[i];
                var connectionEntity = reconnectedPlayers[i];
                var players = m_ExistingPlayersQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
                var playerEntities = m_ExistingPlayersQuery.ToEntityArray(Allocator.Temp);
                Debug.Log($"[SpawnPlayerSystem][{state.WorldUnmanaged.Name}] Reconnecting host migrated player for {networkId.ToFixedString()}.");
                for (var j = 0; j < players.Length; j++)
                {
                    var ghostOwner = players[j];
                    if (ghostOwner.NetworkId == networkId.Value)
                    {
                        state.EntityManager.AddComponentData(playerEntities[j], new ConnectionOwner {Entity = connectionEntity});
                        state.EntityManager.GetBuffer<LinkedEntityGroup>(connectionEntity).Add(new LinkedEntityGroup {Value = playerEntities[j]});
                        state.EntityManager.SetComponentData(connectionEntity, new CommandTarget {targetEntity = playerEntities[j]});
                    }
                }
                // 确保我们不会再次处理此连接
                state.EntityManager.AddComponent<PlayerReconnected>(connectionEntity);
            }

            // 迭代Netcode引发的所有连接事件，
            // 如果他们是新加入者，则为他们生成一个玩家角色控制器：
            var connectionEntities = m_NewPlayersQuery.ToEntityArray(Allocator.Temp);
            var networkIds = m_NewPlayersQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            for (var i = 0; i < connectionEntities.Length; i++)
            {
                var networkId = networkIds[i];
                var connectionEntity = connectionEntities[i];
                var player = state.EntityManager.Instantiate(prefab);
                Debug.Log($"[SpawnPlayerSystem][{state.WorldUnmanaged.Name}] Spawning player CC '{player.ToFixedString()}' (from prefab '{prefabName}') for {networkId.ToFixedString()}.");

                // 偏移生成位置，以便 ghosts 不会生成在彼此之上。
                // 在真实的游戏中，您需要设置生成位置/区域。
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(prefab);
                localTransform.Position.x += networkId.Value * 2;
                state.EntityManager.SetComponentData(player, localTransform);

                // 网络 ID 所有者必须在生成的 ghost 上设置。
                // 这样做使 client 有权为（i.e.控制）此 ghost 筹集投入。
                state.EntityManager.SetComponentData(player, new GhostOwner {NetworkId = networkId.Value});

                // 这是为了支持瘦 client 播放器。
                // 您通常不需要执行此操作，因为通常更容易启用 AutoCommandTarget
                // （通过 GhostAuthoringComponent）。
                // 有关更多详细信息，请参阅 ThinClients 示例。
                state.EntityManager.SetComponentData(connectionEntity, new CommandTarget {targetEntity = player});

                // 将播放器添加到连接上链接的 entity 组中，因此它被销毁
                // 断开连接时自动（i.e。它与连接 entity 一起被破坏，
                // 当连接 entity 被破坏时）。
                state.EntityManager.GetBuffer<LinkedEntityGroup>(connectionEntity).Add(new LinkedEntityGroup {Value = player});

                // 这是一个方便：它允许我们轻松获取与关联的连接 entity
                // 该玩家角色控制器 entity。
                state.EntityManager.AddComponentData(player, new ConnectionOwner {Entity = connectionEntity});

                // 标记此连接已为其生成了一个玩家，因此我们不会再次处理它：
                state.EntityManager.AddComponent<PlayerSpawned>(connectionEntity);
            }
        }
    }
}
