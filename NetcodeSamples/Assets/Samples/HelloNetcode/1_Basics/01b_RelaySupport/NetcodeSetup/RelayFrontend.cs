using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// HUD 实现。实现按钮的行为，托管 server，加入 client，并开始游戏。
    ///
    /// 一旦用户按下，文本字段将输出 server 和 client 向继电器 server 注册的状态
    /// 相应的按钮。
    ///
    /// 引导程序 world 构造为 run 和 jobs，用于设置主机和 client 配置用于继电器 server。
    /// 完成此操作后，即可启动游戏，并可从构建的 world 中检索配置。
    /// </summary>
    public class RelayFrontend :
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

        public InputField JoinCode;
        public Text HostConnectionLabel;
        public Text ClientConnectionLabel;
        public Toggle UseRelayForLocalConnection;

        string m_OldAddressValue;
        bool m_UseRelayForLocalClient;
        bool m_IsHosting;
        ConnectionState m_State;
        HostServer m_HostServerSystem;
        ConnectingPlayer m_HostClientSystem;

        enum ConnectionState
        {
            Unknown,
            SetupHost,
            SetupClient,
            JoinGame,
            JoinLocalGame,
        }

#if !UNITY_SERVER
        public override void Start()
        {
            base.Start();
            SceneName = "RelayFrontend";
#if UNITY_WEBGL
            ClientServerButton.interactable = false;
#endif
        }

        protected override void OnStart() { }

        public void OnUseRelayForLocalClient(Toggle value)
        {
            m_UseRelayForLocalClient = value.isOn;
        }

        public void OnRelayEnable(Toggle value)
        {
            if (value.isOn)
            {
                Port.gameObject.SetActive(false);
                UseRelayForLocalConnection.interactable = true;
                m_OldAddressValue = JoinCode.text;
                JoinCode.text = string.Empty;
                JoinCode.placeholder.GetComponent<Text>().text = "Enter Join Code...";
                ClientServerButton.interactable = true;
            }
            else
            {
                Port.gameObject.SetActive(true);
                UseRelayForLocalConnection.interactable = false;
                JoinCode.text = m_OldAddressValue;
                if (string.IsNullOrEmpty(JoinCode.text))
                    JoinCode.text = "127.0.0.1";
                JoinCode.placeholder.GetComponent<Text>().text = "Enter Address...";
#if UNITY_WEBGL
                ClientServerButton.interactable = false;
#endif
            }
        }

        public void Update()
        {
            // 当中继在托管按钮上切换时，当用户在其中输入内容时应禁用
            // 加入代码文本字段，因为预计您接下来要加入中继会话
            if (EnableRelay.isOn)
            {
                if (!string.IsNullOrEmpty(JoinCode.text))
                    ClientServerButton.interactable = false;
                else
                    ClientServerButton.interactable = true;
            }
            // 如果中继已关闭，则重置托管按钮，除非您使用的是 webgl，否则它应该
            // 保持关闭状态，因为它仅支持中继托管
            else if (!ClientServerButton.interactable)
            {
#if !UNITY_WEBGL
                ClientServerButton.interactable = true;
#endif
            }

            switch (m_State)
            {
                case ConnectionState.SetupHost:
                {
                    m_IsHosting = true;
                    HostServer();
                    m_State = ConnectionState.SetupClient;
                    goto case ConnectionState.SetupClient;
                }
                case ConnectionState.SetupClient:
                {
                    var isServerHostedLocally = m_HostServerSystem?.RelayServerData.Endpoint.IsValid;
                    var enteredJoinCode = !string.IsNullOrEmpty(JoinCode.text);
                    if (isServerHostedLocally.GetValueOrDefault())
                    {
                        if (m_UseRelayForLocalClient)
                        {
                            SetupClient();
                            m_HostClientSystem.GetJoinCodeFromHost();
                        }
                        m_State = ConnectionState.JoinLocalGame;
                        goto case ConnectionState.JoinLocalGame;
                    }

                    if (enteredJoinCode)
                    {
                        JoinAsClient();
                        m_State = ConnectionState.JoinGame;
                        goto case ConnectionState.JoinGame;
                    }

                    if (!m_IsHosting)
                    {
                        ClientConnectionLabel.text = "Join Code field is empty!";
                        m_State = ConnectionState.Unknown;
                    }
                    break;
                }
                case ConnectionState.JoinGame:
                {
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        ConnectToRelayServer();
                        m_State = ConnectionState.Unknown;
                    }
                    break;
                }
                case ConnectionState.JoinLocalGame:
                {
                    var hasClientConnectedToRelayService = m_HostClientSystem?.RelayClientData.Endpoint.IsValid;
                    if (!m_UseRelayForLocalClient || hasClientConnectedToRelayService.GetValueOrDefault())
                    {
                        SetupRelayHostedServerAndConnect();
                        m_State = ConnectionState.Unknown;
                    }
                    break;
                }
                case ConnectionState.Unknown:
                {
                    m_IsHosting = false;
                    break;
                }
                default: return;
            }
        }

        public void StartHostServer()
        {
            if (EnableRelay.isOn)
                m_State = ConnectionState.SetupHost;
            else
                StartIpPortClientServer();
        }

        public void StartSetupClient()
        {
            if (EnableRelay.isOn)
                m_State = ConnectionState.SetupClient;
            else
                ConnectToIpPortServer();
        }

        void HostServer()
        {
            var world = World.All[0];
            m_HostServerSystem = world.GetOrCreateSystemManaged<HostServer>();
            var enableRelayServerEntity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<EnableRelayServer>());
            world.EntityManager.AddComponent<EnableRelayServer>(enableRelayServerEntity);

            m_HostServerSystem.UIBehaviour = this;
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(m_HostServerSystem);
        }

        void SetupClient()
        {
            var world = World.All[0];
            m_HostClientSystem = world.GetOrCreateSystemManaged<ConnectingPlayer>();
            m_HostClientSystem.UIBehaviour = this;
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(m_HostClientSystem);
        }

        void JoinAsClient()
        {
            SetupClient();
            var world = World.All[0];
            var enableRelayServerEntity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<EnableRelayServer>());
            world.EntityManager.AddComponent<EnableRelayServer>(enableRelayServerEntity);
            m_HostClientSystem.JoinUsingCode(JoinCode.text);
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
        void SetupRelayHostedServerAndConnect()
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                UnityEngine.Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return;
            }

            var world = World.All[0];
            var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>()?.RelayClientData;
            var relayServerData = world.GetExistingSystemManaged<HostServer>().RelayServerData;
            var joinCode = world.GetExistingSystemManaged<HostServer>().JoinCode;

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData.GetValueOrDefault());
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            LoadScenes(server);
            SceneManager.LoadScene("RelayHUD", LoadSceneMode.Additive);

            var joinCodeEntity = server.EntityManager.CreateEntity(ComponentType.ReadOnly<JoinCode>());
            server.EntityManager.SetComponentData(joinCodeEntity, new JoinCode { Value = joinCode });

            using var serverDriverQuery = server.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
            var serverDriver = serverDriverQuery.GetSingletonRW<NetworkStreamDriver>();
            serverDriver.ValueRW.RequireConnectionApproval = m_RequireConnectionApproval;
            serverDriver.ValueRW.Listen(NetworkEndpoint.AnyIpv4);
            var ipcLocalEndPoint = serverDriver.ValueRW.DriverStore.GetDriverInstanceRO(1).driver.GetLocalEndpoint();

            using var clientDriverQuery = client.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
            var clientDriver = clientDriverQuery.GetSingletonRW<NetworkStreamDriver>();
            if (relayClientData.HasValue)
            {
                clientDriver.ValueRW.Connect(client.EntityManager, relayClientData.Value.Endpoint);
            }
            else
            {
                clientDriver.ValueRW.Connect(client.EntityManager, ipcLocalEndPoint);
            }
        }

        void ConnectToRelayServer()
        {
            var world = World.All[0];
            var relayClientData = world.GetExistingSystemManaged<ConnectingPlayer>().RelayClientData;

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), relayClientData);
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            LoadScenes(client);

            var networkStreamEntity = client.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkStreamRequestConnect>());
            client.EntityManager.SetName(networkStreamEntity, "NetworkStreamRequestConnect");
            // 对于 IPC 这将不起作用并在传输层中给出错误。对于此示例，我们强制 client 通过中继服务进行连接。
            // 对于本地托管的 server，client 需要连接到 NetworkEndpoint.AnyIpv4，在所有其他情况下需要连接到 relayClientData.Endpoint。
            client.EntityManager.SetComponentData(networkStreamEntity, new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });
        }
#endif
    }
}
