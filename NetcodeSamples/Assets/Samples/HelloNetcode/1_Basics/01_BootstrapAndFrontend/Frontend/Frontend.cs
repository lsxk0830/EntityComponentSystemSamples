using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.NetCode;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.HostMigration;
using Unity.Networking.Transport;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Samples.HelloNetcode
{
    public class Frontend : MonoBehaviour
    {
        const ushort k_NetworkPort = 7979;
        const string k_LastPlayedSampleSceneKey = "Frontend.LastPlayedSampleScene";
        const string k_LastSamplePickerDropdownValueKey = "Frontend.LastSamplePickerDropdownValue";
        const int k_MaxSessionPlayers = 50;

        public InputField Address;
        public InputField Port;
        public InputField SessionID;
        public Dropdown Sample;
        public Dropdown SamplePicker;
        public Button ClientServerButton;
        public Toggle UseSessions;
        public Toggle EnableRelay;
        public Toggle EnableHostMigration;

        /// <summary>
        /// 存储本地 world 的旧名称（由初始引导程序创建）。
        /// 当从游戏返回菜单时创建本地 world 时，稍后会重复使用它。
        /// </summary>
        internal static string OldFrontendWorldName = string.Empty;

        /// <summary>
        /// 存储此前端的名称 scene，稍后从游戏 scene 返回到游戏时会使用该名称
        /// 主菜单 scene（前端）。其他前端可以覆盖它。
        /// </summary>
        public static string SceneName;

        /// <summary>
        /// 当加载 ConnectionApproval scene 时，<see cref="GetAndSaveSceneSelection"/> 将其设置为 true。能
        /// 然后在调用 <see cref="NetworkDriver.Listen"/> 之前在网络驱动程序上进行设置以根据需要启用该功能。
        /// </summary>
        protected bool m_RequireConnectionApproval;

        ISession m_Session;

        static string LastPlayedSampleScene
        {
            get => PlayerPrefs.GetString(k_LastPlayedSampleSceneKey, null);
            set => PlayerPrefs.SetString(k_LastPlayedSampleSceneKey, value);
        }

        static int LastSamplePickerDropdownValue
        {
            get => math.clamp(PlayerPrefs.GetInt(k_LastSamplePickerDropdownValueKey, 0), 0, 1);
            set => PlayerPrefs.SetInt(k_LastSamplePickerDropdownValueKey, value);
        }

        public void OnUseSessions(Toggle value)
        {
            if (!value.isOn)
            {
                Address.gameObject.SetActive(true);
                SessionID.gameObject.SetActive(false);
                EnableRelay.isOn = false;
                EnableRelay.interactable = false;
                EnableHostMigration.isOn = false;
                EnableHostMigration.interactable = false;
            }
            else
            {
                Address.gameObject.SetActive(false);
                SessionID.gameObject.SetActive(true);
                EnableRelay.interactable = true;
                EnableHostMigration.interactable = true;
            }
        }

        public virtual void OnEnableHostMigration(Toggle value)
        {
            EnableRelay.isOn = value.isOn;
            EnableRelay.interactable = !value.isOn;
        }

        public virtual void Start()
        {
            SceneName = "Frontend";
            SamplePicker.value = LastSamplePickerDropdownValue;
            PopulateSampleDropdown(SamplePicker.value);
            ClientServerButton.gameObject.SetActive(ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.ClientAndServer);
            OnStart();
        }

        protected virtual void OnStart()
        {
            if (string.IsNullOrEmpty(SessionID.text))
                SessionID.text = $"{Dns.GetHostName()}";
        }

        [RuntimeInitializeOnLoadMethod]
        static void AddQuitHandler()
        {
            Application.quitting += OnQuit;
        }

        async Task InitializeServices()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Frontend] Initialized services");
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception while initializing services: {e.Message}");
                if (e.InnerException != null)
                    Debug.LogError(e.InnerException.Message);
            }
        }

        async Task StartClientServer()
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return;
            }

            await InitializeServices();
            if (!AuthenticationService.Instance.IsSignedIn)
                return;

            var port = ParsePortOrDefault(Port.text);
            try
            {
                var options = new SessionOptions()
                {
                    MaxPlayers = k_MaxSessionPlayers
                };
                if (EnableHostMigration.isOn)
                    options = options.WithRelayNetwork().WithHostMigration().WithNetworkHandler(new CustomNetcodeNetworkHandler());
                else if (EnableRelay.isOn)
                    options = options.WithRelayNetwork().WithNetworkHandler(new CustomNetcodeNetworkHandler());
                else
                    options = options.WithDirectNetwork(port: port).WithNetworkHandler(new CustomNetcodeNetworkHandler());

                m_Session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(SessionID.text, options);
                m_Session.SessionMigrated += OnSessionMigrated;
                m_Session.SessionHostChanged += OnHostChanged;
                if (EnableHostMigration.isOn)
                    ClientServerBootstrap.ServerWorld.EntityManager.CreateSingleton<EnableHostMigration>();
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception while creating session: {e.Message}");
                return;
            }

            LoadScenes(ClientServerBootstrap.ServerWorld);
            await SetHudSessionName();
        }

        protected void StartIpPortClientServer()
        {
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            LoadScenes(server);

            var port = ParsePortOrDefault(Port.text);
            NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(port);
            using var serverDriverQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            ref var serverDriver = ref serverDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            serverDriver.RequireConnectionApproval = m_RequireConnectionApproval;
            serverDriver.Listen(ep);
            ep = NetworkEndpoint.LoopbackIpv4.WithPort(port);
            using var clientDriverQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            clientDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
        }

        async Task SetHudSessionName()
        {
            var frontendHud = FindFirstObjectByType<FrontendHUD>();
            // HUD scene 可能尚未加载，因此我们需要对其进行轮询
            while (frontendHud == null)
            {
                await Task.Delay(100);
                frontendHud = FindFirstObjectByType<FrontendHUD>();
            }
            frontendHud.LobbyName.text = m_Session.Id;
        }

        /// <summary>
        /// 当会话主机更改时，通知 HUD 打印中继/迁移状态（等待加入代码）。
        /// </summary>
        void OnHostChanged(string obj)
        {
// 作为专用 server 运行时，跳过 UI/HUD 交互
#if !UNITY_SERVER
            HostMigrationHUD.SetWaitForRelayConnection(new WaitForRelayConnection() { WaitForJoinCode = true, OldJoinCode = m_Session.Code, IsHostMigration = true, StartTime = Time.realtimeSinceStartup});
#endif
        }

        /// <summary>
        /// 主机迁移完成后，清理并重置迁移 HUD 元素。
        /// </summary>
        void OnSessionMigrated()
        {
#if !UNITY_SERVER
            if (m_Session.IsHost)
            {
                // 连接 server 迁移统计信息 HUD
                var statsText = FindFirstObjectByType<HostMigrationHUD>().StatsText;
                ClientServerBootstrap.ServerWorld.GetExistingSystemManaged<ServerHostMigrationHUDSystem>().StatsText = statsText;

                // 禁用 client 状态 HUD
                ClientServerBootstrap.ClientWorld.GetOrCreateSystemManaged<ClientHostMigrationHUDSystem>().Enabled = false;
            }
            else
            {
                using var waitForRelayQuery = ClientServerBootstrap.ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WaitForRelayConnection>());
                ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(waitForRelayQuery);
            }
#endif
        }

        static void OnQuit()
        {
            Application.quitting -= OnQuit;
            if (MultiplayerService.Instance != null)
            {
                foreach (var session in MultiplayerService.Instance.Sessions)
                    session.Value.LeaveAsync();
            }
        }

        public void StartClientServerButton()
        {
            if (UseSessions.isOn)
            {
#pragma warning disable CS4014
                StartClientServer();
#pragma warning restore CS4014
            }
            else
            {
                StartIpPortClientServer();
            }
        }

        protected string GetAndSaveSceneSelection()
        {
            // 检查示例选择器中是否选择了 Samples(0) 或 HelloNetcodeSamples(1)
            LastSamplePickerDropdownValue = SamplePicker.value;
            string sceneName = Sample.options[Sample.value].text;
            m_RequireConnectionApproval = sceneName.Contains("ConnectionApproval", StringComparison.OrdinalIgnoreCase);
            if (m_RequireConnectionApproval)
                Debug.Log($"[Frontend] Enabling connection approval");
            LastPlayedSampleScene = sceneName;
            PlayerPrefs.Save();
            return sceneName;
        }

        public async void ConnectToServer()
        {
            if (!UseSessions.isOn)
            {
                Debug.Log($"[ConnectToServer] Called on '{Address.text}:{Port.text}'.");
                ConnectToIpPortServer();
                return;
            }

            await InitializeServices();

            Debug.Log($"[ConnectToServer] Called on session ID '{SessionID.text}'.");
            try
            {
                var options = new JoinSessionOptions();
                // 加入会话时将自动使用网络类型（中继/直接）
                if (EnableHostMigration.isOn)
                    options = options.WithHostMigration().WithNetworkHandler(new CustomNetcodeNetworkHandler());
                else
                    options = options.WithNetworkHandler(new CustomNetcodeNetworkHandler());

                m_Session = await MultiplayerService.Instance.JoinSessionByIdAsync(SessionID.text, options);
                m_Session.SessionMigrated += OnSessionMigrated;
                m_Session.SessionHostChanged += OnHostChanged;
#if !UNITY_SERVER
                if (EnableHostMigration.isOn)
                {
                    var clientHudSystem = ClientServerBootstrap.ClientWorld.GetExistingSystemManaged<ClientHostMigrationHUDSystem>();
                    clientHudSystem.RelayJoinCode = m_Session.Code;
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception while joining session: {e.Message}");
                return;
            }

            LoadScenes(ClientServerBootstrap.ClientWorld);
            await SetHudSessionName();
        }

        protected void ConnectToIpPortServer()
        {
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            LoadScenes(client);
            var ep = NetworkEndpoint.Parse(Address.text, ParsePortOrDefault(Port.text));
            using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
        }

        public void LoadScenes(World world)
        {
            SceneManager.LoadScene("FrontendHUD");
            if (EnableHostMigration != null && EnableHostMigration.isOn)
                SceneManager.LoadScene("HostMigrationHUD", LoadSceneMode.Additive);

            // 销毁本地模拟 world 以避免游戏 scene 加载到其中
            // 这会阻止渲染（不太支持从多个 world 进行渲染）
            // 以及其他问题。
            DestroyLocalSimulationWorld();

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;
            var sceneName = GetAndSaveSceneSelection();
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 根据是否在第一个中选择示例/hellonetcode 选择来填充 scene 下拉列表
        /// 下拉菜单。始终跳过前端 scene 因为它是显示此菜单的，并且没有任何意义
        /// load (as well as HUD scenes since they are additively loaded on top of sample scenes)
        /// </summary>
        public void PopulateSampleDropdown(int value)
        {
            var scenes = SceneManager.sceneCountInBuildSettings;
            Sample.ClearOptions();
            if (value == 0)
            {
                var lastPlayed = LastPlayedSampleScene;
                for (var i = 0; i < scenes; ++i)
                {
                    var scenePathByBuildIndex = SceneUtility.GetScenePathByBuildIndex(i);

                    var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePathByBuildIndex);
                    var isHelloNetcodeScene = scenePathByBuildIndex.Contains("HelloNetcode");
                    var isDisableBootstrapScene = scenePathByBuildIndex.Contains("DisableBootstrap");
                    if (!sceneName.StartsWith("Frontend") && !sceneName.EndsWith("HUD") && !isHelloNetcodeScene && !isDisableBootstrapScene)
                    {
                        Sample.options.Add(new Dropdown.OptionData { text = sceneName });
                        if (string.Equals(sceneName, lastPlayed, StringComparison.OrdinalIgnoreCase))
                            Sample.value = Sample.options.Count;
                    }
                }
            }
            else if (value == 1)
            {
                string lastPlayed = LastPlayedSampleScene;
                for (var i = 0; i < scenes; ++i)
                {
                    var scenePathByBuildIndex = SceneUtility.GetScenePathByBuildIndex(i);

                    var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePathByBuildIndex);
                    var isHelloNetcodeScene = scenePathByBuildIndex.Contains("HelloNetcode");
                    var isDisableBootstrapScene = scenePathByBuildIndex.Contains("DisableBootstrap");
                    if (!sceneName.StartsWith("Frontend") && !sceneName.EndsWith("HUD") && isHelloNetcodeScene && !isDisableBootstrapScene)
                    {
                        Sample.options.Add(new Dropdown.OptionData { text = sceneName });
                        if (string.Equals(sceneName, lastPlayed, StringComparison.OrdinalIgnoreCase))
                            Sample.value = Sample.options.Count;
                    }
                }
            }
            else
            {
                Debug.LogError("Invalid dropdown value");
            }

            Sample.RefreshShownValue();
        }

        protected void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    OldFrontendWorldName = world.Name;
                    world.Dispose();
                    break;
                }
            }
        }

        // 尝试解析端口，如果成功则返回 true，否则返回 false
        // 端口将设置为解析的任何内容，否则为 k_NetworkPort 的默认端口
        private UInt16 ParsePortOrDefault(string s)
        {
            if (!UInt16.TryParse(s, out var port))
            {
                Debug.LogWarning($"Unable to parse port, using default port {k_NetworkPort}");
                return k_NetworkPort;
            }

            return port;
        }
    }
}
