using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
    public class HostMigrationHUD : MonoBehaviour
    {
        public Text StatsText;

#if !UNITY_SERVER
        void Start()
        {
            if (ClientServerBootstrap.ServerWorld != null)
            {
                var hudSystem = ClientServerBootstrap.ServerWorld.GetOrCreateSystemManaged<ServerHostMigrationHUDSystem>();
                hudSystem.StatsText = StatsText;
            }
            else if (ClientServerBootstrap.ClientWorld != null)
            {
                var hudSystem = ClientServerBootstrap.ClientWorld.GetOrCreateSystemManaged<ClientHostMigrationHUDSystem>();
                hudSystem.StatsText = StatsText;
            }
        }

        /// <summary>
        /// 设置 component 用于跟踪主机迁移过程中的中继连接状态，然后 HUD UI 打印信息
        /// </summary>
        public static Entity SetWaitForRelayConnection(WaitForRelayConnection waitComponent)
        {
            using var relayEntityQuery = ClientServerBootstrap.ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WaitForRelayConnection>());
            var relayEntity = Entity.Null;
            // 如果我们收到主机迁移事件，但主机发生故障并选择了另一台主机，则可能已经存在 WaitForRelayConnection
            if (!relayEntityQuery.IsEmptyIgnoreFilter)
                relayEntity = relayEntityQuery.ToEntityArray(Allocator.Temp)[0];
            else
                relayEntity = ClientServerBootstrap.ClientWorld.EntityManager.CreateEntity(ComponentType.ReadOnly<WaitForRelayConnection>());
            ClientServerBootstrap.ClientWorld.EntityManager.AddComponentData(relayEntity, waitComponent);
            return relayEntity;
        }
    }

    public struct WaitForRelayConnection : IComponentData
    {
        public bool WaitForJoinCode;
        public bool WaitForHostSetup;
        public bool IsHostMigration;
        public float StartTime;
        public FixedString32Bytes OldJoinCode;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientHostMigrationHUDSystem : SystemBase
    {
        public Text StatsText;
        EntityQuery m_RelayQuery;
        public FixedString32Bytes RelayJoinCode;

        protected override void OnCreate()
        {
            m_RelayQuery = GetEntityQuery(ComponentType.ReadOnly<WaitForRelayConnection>());
        }

        protected override void OnUpdate()
        {
            if (StatsText != null)
            {
                var prefix = "<color=#008000ff>[Client]</color>";
                if (!m_RelayQuery.IsEmptyIgnoreFilter)
                {
                    var waitData = m_RelayQuery.GetSingleton<WaitForRelayConnection>();
                    if (waitData.IsHostMigration)
                        StatsText.text = $"{prefix} Host migration in progress: ";
                    else
                        StatsText.text = $"{prefix} ";
                    var connectionTime = UnityEngine.Time.realtimeSinceStartup - waitData.StartTime;

                    // 如果我们正在等待中继但还没有加入代码，我们仍在等待大厅将其发送给我们
                    if (waitData.WaitForJoinCode)
                    {
                        StatsText.text += $"Waiting for new join code ({connectionTime:F2} s)";
                        if (waitData.OldJoinCode == RelayJoinCode)
                            return;
                        waitData.WaitForJoinCode = false;
                        waitData.StartTime = UnityEngine.Time.realtimeSinceStartup;
                        World.EntityManager.SetComponentData(m_RelayQuery.GetSingletonEntity(), waitData);
                        Debug.Log($"{prefix}[HostMigration] New join code received ({connectionTime:F2} s)");
                        return;
                    }

                    if (waitData.WaitForHostSetup)
                    {
                        StatsText.text += $"Waiting for host migration data ({connectionTime:F2} s)";
                        return;
                    }

                    // TODO: 当主机迁移发生时，这似乎捕获了旧的中继连接，因此立即看到已建立
                    var relayEntity = m_RelayQuery.GetSingletonEntity();
                    CheckRelayStatus(StatsText, World, relayEntity, prefix, connectionTime);
                }
                else
                {
                    StatsText.text = $"{prefix} Ready.";
                }
            }
        }

        internal static void CheckRelayStatus(Text statusText, World world, Entity relayEntity, string prefix, double connectionTime)
        {
            var relayConnectionStatus = GetRelayConnectionStatus(world);
            switch (relayConnectionStatus)
            {
                case RelayConnectionStatus.Established:
                    statusText.text += "Relay connection established";
                    Debug.Log($"{prefix}[HostMigration] Relay connection established ({connectionTime:F2} s)");
                    world.EntityManager.DestroyEntity(relayEntity);
                    break;
                case RelayConnectionStatus.NotEstablished:
                    statusText.text += $"Connecting to relay server ({connectionTime:F2} s)";
                    break;
                case RelayConnectionStatus.AllocationInvalid:
                    statusText.text += "Relay connection failed; allocation is invalid";
                    break;
                // 在 client 从旧主机迁移到新主机的过程中，会有一段时间它没有连接到任何东西并等待新的加入代码
                case RelayConnectionStatus.NotUsingRelay:
                    break;
                default:
                    statusText.text += "Unexpected Relay connection status";
                    break;
            }
        }

        static RelayConnectionStatus GetRelayConnectionStatus(World world)
        {
            using var drvQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            var networkStreamDriver =drvQuery.GetSingleton<NetworkStreamDriver>();

            // 获取带有 UDPNetworkInterface 并启用继电器的 server 驱动程序
            RelayConnectionStatus status = RelayConnectionStatus.NotUsingRelay;
            for (var i = networkStreamDriver.DriverStore.FirstDriver;
                 status == RelayConnectionStatus.NotUsingRelay && i < networkStreamDriver.DriverStore.LastDriver;
                 ++i)
            {
                var networkDriver = networkStreamDriver.DriverStore.GetDriverRO(i);
                world.EntityManager.CompleteAllTrackedJobs();
                status = networkDriver.GetRelayConnectionStatus();
            }
            return status;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerHostMigrationHUDSystem : SystemBase
    {
        public Text StatsText;
        EntityQuery m_RelayQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<HostMigrationStats>();
            m_RelayQuery = GetEntityQuery(ComponentType.ReadOnly<WaitForRelayConnection>());
        }

        protected override void OnUpdate()
        {
            var stats = SystemAPI.GetSingleton<HostMigrationStats>();
            if (StatsText != null)
            {
                var prefix = "<color=#ff0000ff>[Server]</color>";
                if (!m_RelayQuery.IsEmptyIgnoreFilter)
                {
                    var waitData = m_RelayQuery.GetSingleton<WaitForRelayConnection>();
                    if (waitData.IsHostMigration)
                        StatsText.text = $"{prefix} Host migration in progress: ";
                    else
                        StatsText.text = $"{prefix} Starting: ";

                    var connectionTime = UnityEngine.Time.realtimeSinceStartup - waitData.StartTime;
                    var relayEntity = m_RelayQuery.GetSingletonEntity();
                    ClientHostMigrationHUDSystem.CheckRelayStatus(StatsText, World, relayEntity, prefix, connectionTime);
                }
                else
                {
                    StatsText.text = $"{prefix} Ghost Count: {stats.GhostCount} Prefab Count: {stats.PrefabCount} Update Size: {stats.UpdateSize} Total Update Size: {stats.TotalUpdateSize}";
                }
            }
        }
#endif
    }
}
