using System;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
    public class HostMigrationFrontend :
#if UNITY_SERVER
        MonoBehaviour
#else
        Frontend
#endif
    {
        public string HostConnectionStatus
        {
            get => HostConnectionLabel.text;
            set => HostConnectionLabel.text = value;
        }
        public string ClientConnectionStatus
        {
            get => ClientConnectionLabel.text;
            set => ClientConnectionLabel.text = value;
        }

        public InputField LobbyName;
        public Text HostConnectionLabel;
        public Text ClientConnectionLabel;

        // 执行初始连接时等待主机加入代码的时间（以秒为单位）
        const int k_InitialHostJoinWaitTimeout = 30;
        ConnectionState m_State;

        public GameObject hostMigrationControllerPrefab;
        public HostMigrationController hostMigrationController;

#if !UNITY_SERVER

        public override void OnEnableHostMigration(Toggle value)
        {
            if (value.isOn)
            {
                LobbyName.gameObject.SetActive(true);
                Address.gameObject.SetActive(false);
                Port.gameObject.SetActive(false);
                ClientServerButton.interactable = true;
            }
            else
            {
                LobbyName.gameObject.SetActive(false);
                Address.gameObject.SetActive(true);
                Port.gameObject.SetActive(true);
#if UNITY_WEBGL
                ClientServerButton.interactable = false;
#endif
            }
        }

        public override void Start()
        {
            base.Start();
            SceneName = "HostMigrationFrontend";
        }

        protected override void OnStart()
        {
            hostMigrationController = FindFirstObjectByType<HostMigrationController>();
            if (hostMigrationController == null)
                hostMigrationController = Instantiate(hostMigrationControllerPrefab).GetComponent<HostMigrationController>();
            SceneManager.sceneLoaded += hostMigrationController.OnSceneLoaded;
        }

        /// <summary>
        /// 游戏会话主机的初始设置。
        /// </summary>
        public async void SetupRelayAndLobbyAsHost()
        {
            if (!EnableHostMigration.isOn)
            {
                var sceneName = GetAndSaveSceneSelection();
                StartIpPortClientServer();
                return;
            }
            HostConnectionStatus = "Initializing services";
            await InitializeServices();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                HostConnectionStatus = "Logging in anonymously";
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log($"[HostMigration] Signed in as {AuthenticationService.Instance.PlayerId}");

            HostConnectionStatus = "Waiting for allocation";
            var allocation = await RelayService.Instance.CreateAllocationAsync(hostMigrationController.MaxPlayers - 1);
            HostConnectionStatus = "Waiting for join code";
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[HostMigration] Created allocation {allocation.AllocationId} with join code {joinCode}");

            SetupRelayHostedServerAndConnect(allocation.ToRelayServerData(hostMigrationController.ConnectionType));
            Debug.Log($"[HostMigration] Creating lobby with name {LobbyName.text}");
            await hostMigrationController.CreateLobbyAsync(joinCode, allocation.AllocationId.ToString());
            await hostMigrationController.SubscribeToLobbyEvents();
        }

        /// <summary>
        /// 初始服务初始化。这只需要完成一次。在独立播放器中运行时
        /// 我们需要使用不同的玩家资料，否则大厅会将此玩家视为相同的身份
        /// 作为编辑者或其他玩家（会将所有人从大厅中删除，例如当一个玩家实例
        /// 断开连接/离开）。除非传递 -userprofile 参数，否则将生成随机配置文件名称
        /// 在这种情况下将使用给定名称的进程。
        /// </summary>
        async Task InitializeServices()
        {
            var userprofile = CommandLineUtils.GetCommandLineValueFromKey("userprofile");
            if (!string.IsNullOrEmpty(userprofile))
            {
                Debug.Log($"[HostMigration] Using user profile {userprofile}");
                var options = new InitializationOptions();
                options.SetProfile(userprofile);
                await UnityServices.InitializeAsync(options);
            }
            else if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
            {
                var random = new System.Random((int)System.Diagnostics.Stopwatch.GetTimestamp());
                for (int i = 0; i < 10; ++i)
                    userprofile += (char)random.Next(65, 90);
                Debug.Log($"[HostMigration] Using random user profile {userprofile}");
                var options = new InitializationOptions();
                options.SetProfile(userprofile);
                await UnityServices.InitializeAsync(options);
            }
            else
            {
                await UnityServices.InitializeAsync();
            }
        }

        /// <summary>
        /// clients 加入游戏会话的初始设置。
        /// </summary>
        public async void JoinLobbyAndConnectWithRelayAsClient()
        {
            if (!EnableHostMigration.isOn)
            {
                ConnectToIpPortServer();

                return;
            }
            ClientConnectionStatus = "Initializing services";
            await InitializeServices();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ClientConnectionStatus = "Logging in anonymously";
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log($"[HostMigration] Signed in as {AuthenticationService.Instance.PlayerId}");

            ClientConnectionStatus = "Joining lobby";
            await hostMigrationController.JoinLobbyByNameAsync(LobbyName.text);
            await hostMigrationController.SubscribeToLobbyEvents();

            if (hostMigrationController.CurrentLobby != null && hostMigrationController.CurrentLobby.Players != null)
            {
                if (!hostMigrationController.CurrentLobby.Data.ContainsKey(LobbyKeys.RelayHost) ||
                    !hostMigrationController.CurrentLobby.Data.ContainsKey(LobbyKeys.RelayJoinCode))
                {
                    Debug.LogError($"[HostMigration] Lobby data missing entries for '{LobbyKeys.RelayHost}' and/or '{LobbyKeys.RelayJoinCode}'");
                    return;
                }

                // 如果大厅数据具有无效的加入代码（可能是前一个主机），那么它就没有
                // 已由当前主机更新。迁移可能正在发生，我们应该等待
                // 主机迁移期间 clients 通常执行的正确加入代码
                if (hostMigrationController.CurrentLobby.Data[LobbyKeys.RelayHost].Value !=
                    hostMigrationController.CurrentLobby.HostId)
                {
                    Debug.Log("[HostMigration] Relay host does not match lobby host. Will wait for join code");
                    ClientConnectionStatus = "Waiting for join code";
                    hostMigrationController.WaitForInitialJoin = true;
                    var timeout = Time.realtimeSinceStartup + k_InitialHostJoinWaitTimeout;
                    while (hostMigrationController.CurrentLobby.Data[LobbyKeys.RelayHost].Value != hostMigrationController.CurrentLobby.HostId)
                    {
                        if (Time.realtimeSinceStartup > timeout)
                        {
                            Debug.LogError("[HostMigration] Relay host does not match lobby host, timeout while waiting for updated join code.");
                            hostMigrationController.WaitForInitialJoin = false;
                            return;
                        }
                        await Task.Delay(100);
                    }
                    hostMigrationController.WaitForInitialJoin = false;
                    if (hostMigrationController.CurrentLobby.Data[LobbyKeys.RelayHost].Value == AuthenticationService.Instance.PlayerId)
                    {
                        Debug.Log("Elected as host while trying to join as client. Will abort the client join flow as the host flow from host migration election will take over.");
                        return;
                    }
                }
                var relayJoinCode = hostMigrationController.CurrentLobby.Data[LobbyKeys.RelayJoinCode].Value;
                Debug.Log($"[HostMigration] Using relay join code {relayJoinCode} from lobby data");
                hostMigrationController.RelayJoinCode = relayJoinCode;
                ClientConnectionStatus = "Joining relay allocation";
                var allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                ClientConnectionStatus = "Binding to relay server";
                ConnectToServerWithRelay(allocation.ToRelayServerData(hostMigrationController.ConnectionType));
                await hostMigrationController.UpdatePlayerAllocationId(allocation.AllocationId);
            }
        }

        /// <summary>
        /// 从已完成的 systems 收集中继 server 端点。设置带有中继支持的 server 并连接 client
        /// 通过中继 server 到托管 server。
        /// client 和 server world 都是手动创建的，允许我们覆盖 <see cref="DriverConstructor"/>。
        ///
        /// 两个单例 entities 是通过监听和连接请求构造的。这些将异步执行。
        /// 连接到继电器 server 不会立即绑定。Request 结构将确保我们
        /// 不断轮询，直到建立连接。
        /// </summary>
        void SetupRelayHostedServerAndConnect(RelayServerData relayServerData)
        {
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            try
            {
                var driverConstructor = new RelayDriverConstructor(relayServerData, new RelayServerData());
                NetworkStreamReceiveSystem.DriverConstructor = driverConstructor;
                var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
                var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

                server.EntityManager.CreateEntity(ComponentType.ReadOnly<EnableHostMigration>());

                LoadScenes(server);

                using var driverQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                var serverDriver = driverQuery.GetSingletonRW<NetworkStreamDriver>();
                serverDriver.ValueRW.RequireConnectionApproval = m_RequireConnectionApproval;
                if (!serverDriver.ValueRW
                        .Listen(NetworkEndpoint.AnyIpv4))
                {
                    Debug.LogError($"NetworkStreamDriver.Listen() failed");
                    return;
                }

                // 更新 UI 与继电器的连接状态
                var relayEntity = ClientServerBootstrap.ServerWorld.EntityManager.CreateEntity(ComponentType.ReadOnly<WaitForRelayConnection>());
                ClientServerBootstrap.ServerWorld.EntityManager.SetComponentData(relayEntity, new WaitForRelayConnection() { StartTime = Time.realtimeSinceStartup });

                var ipcPort = serverDriver.ValueRW.GetLocalEndPoint(serverDriver.ValueRW.DriverStore.FirstDriver).Port;
                var networkStreamEntity = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
                client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
                client.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestConnect { Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(ipcPort) });
            }
            finally
            {
                NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
            }
        }

        /// <summary>
        /// 连接到中继 server 和给定的 server 端点。
        /// </summary>
        void ConnectToServerWithRelay(RelayServerData relayServerData)
        {
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            try
            {
                NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), relayServerData);
                var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

                LoadScenes(client);

                // 更新 UI 与继电器的连接状态
                var relayEntity = client.EntityManager.CreateEntity(ComponentType.ReadOnly<WaitForRelayConnection>());
                client.EntityManager.SetComponentData(relayEntity, new WaitForRelayConnection() { StartTime = Time.realtimeSinceStartup });

                var networkStreamEntity = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
                client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");

                // 对于 IPC 这将不起作用并在传输层中给出错误。对于此示例，我们强制 client 通过中继服务进行连接。
                // 对于本地托管的 server，client 需要连接到 NetworkEndpoint.AnyIpv4，在所有其他情况下需要连接到 relayClientData.Endpoint。
                client.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestConnect { Endpoint = relayServerData.Endpoint });
            }
            finally
            {
                NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
            }
        }

#endif
    }
}
