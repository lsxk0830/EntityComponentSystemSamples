using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.NetCode.Samples.Common;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 大厅数据中使用的字符串键，用于传达当前加入代码和主机使用的 ID
    /// 游戏主持人。在主机迁移事件期间，将使用此功能，以便每个人都连接到正确的中继
    /// 分配。
    /// </summary>
    static class LobbyKeys
    {
        public const string RelayJoinCode = "relay.joinCode";
        public const string RelayHost = "relay.host";
    }

    public class HostMigrationController : MonoBehaviour
    {
        public Lobby CurrentLobby => m_CurrentLobby;
        public string RelayJoinCode { get; set; }

        /// <summary>
        /// 当主机的第一次加入因主机尚未准备好而延迟时设置（中继加入代码未更新）
        /// 在这种情况下，在初始主机加入完成之前，我们不会对主机迁移事件做出反应。
        /// </summary>
        public bool WaitForInitialJoin { get; set; }

        /// <summary>
        /// 默认情况下，使用数据报传输层安全 (dtls) 连接类型进行网络传输
        /// </summary>
#if !UNITY_WEBGL
        public string ConnectionType { get; } = "dtls";
#else
        public string ConnectionType { get; } = "wss";
#endif

        public int MaxPlayers
        {
            get
            {
                if (m_CurrentLobby != null) return m_CurrentLobby.MaxPlayers;
                return k_DefaultMaxLobbyPlayers;
            }
        }

        Lobby m_CurrentLobby;
        Coroutine m_Heartbeat;
        HostMigrationFrontend m_HostMigrationFrontend;

        public int InitialDataSize { get; set; } = 100_000;

        // 执行大厅心跳的时间间隔。
        // 此操作必须至少每 30 秒执行一次。
        // Note: 此呼叫受服务速率限制（最大 1 rq/s）。
        const int k_HeartbeatIntervalSeconds = 10;

        // 为此游戏/示例创建的大厅中允许的最大玩家数量。这也设置在大堂
        // 项目 ID 的云配置选项卡（请参阅 https://cloud.unity.com 上的项目）。Relay 的最大支持
        // 玩家是：https://docs.unity.com/ugs/manual/relay/manual/limitations
        const int k_DefaultMaxLobbyPlayers = 50;

        // 主机迁移后建立中继连接的超时时间
        const double k_TimeoutForRelayConnectionSeconds = 60;

        // 重试服务 API 调用时，使用调用之间的此延迟
        const int k_ServiceRetryDelayMS = 1000;

        // 服务 API 调用的重试次数限制
        const int k_ServiceRetryCount = 10;

        MigrationDataInfo m_MigrationConfig;
        EntityQuery m_HostMigrationStatsQuery;
        NativeList<byte> m_MigrationDataBlob;
        Task<LobbyUploadMigrationDataResults> m_UploadTask;

        string m_PrevHostId;

        public string CurrentHostId => m_CurrentLobby == null ? "" : m_CurrentLobby.HostId;
        public bool IsHost => CurrentHostId == CurrentPlayerId;
        public string CurrentLobbyId => m_CurrentLobby == null ? "" : m_CurrentLobby.Id;
        public string CurrentPlayerId => UnityServices.State == ServicesInitializationState.Initialized ?
            AuthenticationService.Instance.PlayerId : "";

        public bool FailNextHostMigration { get; set; }

#if !UNITY_SERVER
        double m_LastUpdateTime = 0.01;

        void Start()
        {
            if (ClientServerBootstrap.AutoConnectPort != 0)
                Debug.LogError("Host migration sample can't run via auto connect (play scene directly) but most be loaded via the frontend");
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
                Debug.LogError($"[HostMigration] Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
            m_MigrationDataBlob = new NativeList<byte>(InitialDataSize, Allocator.Persistent);
            m_HostMigrationFrontend = FindFirstObjectByType<HostMigrationFrontend>();
            DontDestroyOnLoad(this);
        }

        internal void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Frontend.SceneName)
            {
                Debug.Log("[HostMigration] Re-entering frontend scene, leaving lobby");
                m_HostMigrationFrontend = FindFirstObjectByType<HostMigrationFrontend>();
                StopHeartbeat();
                LeaveLobby();
                m_LastUpdateTime = 0;
                m_HostMigrationStatsQuery = default;
            }
        }

        void OnKickedFromLobby()
        {
            Debug.Log("[HostMigration] Left lobby");
            if (FindFirstObjectByType<HostMigrationFrontend>() == null)
            {
                Debug.Log($"[HostMigration] Re-entering frontend scene as we've left the lobby now");
                var frontendHud = FindFirstObjectByType<FrontendHUD>();
                frontendHud?.ReturnToFrontend();
            }
        }

        async void OnLobbyChanged(ILobbyChanges changes)
        {
            if (!changes.LobbyDeleted)
            {
                changes.ApplyToLobby(m_CurrentLobby);
                await CheckHostMigration(changes);
                m_PrevHostId = CurrentHostId;
                if (!IsHost)
                    await CheckLobbyDataForNewRelayHost(changes);
            }
        }

        void OnApplicationQuit()
        {
            LeaveLobby();
        }

        void OnDestroy()
        {
            m_MigrationDataBlob.Dispose();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopHeartbeat();
            LeaveLobby();
        }

        /// <summary>
        /// 只需离开大厅，保持其运行，因为我们需要其他 clients 进行主机迁移
        /// </summary>
        void LeaveLobby()
        {
            if (!string.IsNullOrEmpty(CurrentLobbyId) && !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
                LobbyService.Instance.RemovePlayerAsync(m_CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
        }

        void StartHeartbeat()
        {
            StopHeartbeat();
            Debug.Log($"[HostMigration] Starting heartbeat coroutine.");
            m_Heartbeat = StartCoroutine(HeartbeatLobbyCoroutine());
        }

        void StopHeartbeat()
        {
            if (m_Heartbeat != null)
            {
                Debug.Log($"[HostMigration] Stopping heartbeat coroutine.");
                StopCoroutine(m_Heartbeat);
                m_Heartbeat = null;
            }
        }

        void Update()
        {
            if (ClientServerBootstrap.ServerWorld == null)
                m_HostMigrationStatsQuery = default;

            if (IsHost && ClientServerBootstrap.ServerWorld != null && m_HostMigrationStatsQuery == default)
            {
                var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<HostMigrationStats>();
                var serverWorld = ClientServerBootstrap.ServerWorld;
                m_HostMigrationStatsQuery = builder.Build(serverWorld.EntityManager);
            }

            if (IsHost && ClientServerBootstrap.ServerWorld != null && !string.IsNullOrEmpty(CurrentLobbyId)
                && m_HostMigrationStatsQuery != default && m_HostMigrationStatsQuery.TryGetSingleton<HostMigrationStats>(out var stats) && stats.LastDataUpdateTime > m_LastUpdateTime)
            {
                m_LastUpdateTime = stats.LastDataUpdateTime;
                HostMigrationData.Get(ClientServerBootstrap.ServerWorld, ref m_MigrationDataBlob);
                if (m_MigrationDataBlob.Length > m_MigrationConfig.MaxSize)
                {
                    Debug.LogError($"[HostMigration] Migration data is too large {m_MigrationDataBlob.Length} bytes, maximum is {m_MigrationConfig.MaxSize} bytes. Host migration will not be possible with this data size.");
                    return;
                }

                _ = UploadMigrationData();
            }
        }

        /// <summary>
        /// 将主机迁移数据上传至主机迁移服务。如果需要，上传位置 URL 是
        /// 神清气爽。这将被定期调用，因此如果由于服务速率限制而失败也没关系。
        /// </summary>
        async Task UploadMigrationData()
        {
            try
            {
                if (m_UploadTask != null && !m_UploadTask.IsCompleted)
                {
                    Debug.Log($"[HostMigration] Previous upload still in progress.");
                    return;
                }

                // 从过期时间减去一分钟以确保我们及时更新迁移 URL
                if (m_MigrationConfig.Expires.AddMinutes(-1) < DateTime.UtcNow)
                {
                    Debug.Log($"[HostMigration] Refreshing migration config as it has expired.");
                    m_MigrationConfig = await LobbyService.Instance.GetMigrationDataInfoAsync(CurrentLobbyId);
                    Debug.Log($"[HostMigration] Migration Data Information: Expires:{m_MigrationConfig.Expires} (Now:{DateTime.UtcNow}) MaxSize:{m_MigrationConfig.MaxSize} ReadUrl:{m_MigrationConfig.Read} WriteUrl:{m_MigrationConfig.Write}");
                }

                var uploadData = m_MigrationDataBlob.AsArray().ToArray();
                //var startTime = Time.realtimeSinceStartup;
                m_UploadTask = LobbyService.Instance.UploadMigrationDataAsync(m_MigrationConfig, uploadData, new LobbyUploadMigrationDataOptions());
                await m_UploadTask;
                //Debug.Log($"[HostMigration][DEBUG] Uploaded migration data, size={uploadData.Length} time={Time.realtimeSinceStartup - startTime}");
            }
            catch (LobbyServiceException ex)
            {
                if (ex.Reason == LobbyExceptionReason.RateLimited)
                {
                    Debug.LogWarning($"[HostMigration] Hit lobby rate limit while trying to upload migration data, will try again");
                    return;
                }
                Debug.LogError($"[HostMigration] Lobby exception thrown while trying to upload migration data: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        async Task<byte[]> DownloadMigrationDataWithRetry()
        {
            var migrationData = await DownloadMigrationData();
            var retryCount = 0;
            while (migrationData?.Data == null || migrationData.Data.Length == 0)
            {
                if (retryCount++ == k_ServiceRetryCount)
                {
                    Debug.LogError($"[HostMigration] Received 0 bytes migration data after {retryCount} attempts. Failed to download migration data.");
                    break;
                }
                Debug.LogWarning($"[HostMigration] Received 0 bytes migration data. Will retry download (retry count = {retryCount}).");
                await Task.Delay(k_ServiceRetryDelayMS);
                migrationData = await DownloadMigrationData();
            }
            if (migrationData != null && migrationData.Data != null)
                return migrationData.Data;

            return Array.Empty<byte>();
        }

        async Task<LobbyMigrationData> DownloadMigrationData()
        {
            try
            {
                return await LobbyService.Instance.DownloadMigrationDataAsync(m_MigrationConfig, new LobbyDownloadMigrationDataOptions());
            }
            catch (LobbyServiceException ex)
            {
                Debug.Log($"[HostMigration] Failed to download migration data: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 检查大厅更改事件以了解主机迁移事件。
        ///   - 如果这是我们已经连接到的主机的主机迁移事件，则可以忽略
        ///   - 重置 client 驱动，即使连接 entity 被破坏仍然可以有连接
        ///     呈现给网络驱动程序本身中的继电器 server。
        ///   - 如果这是我们的主机迁移活动，我们需要接管托管职责
        ///     - 启动大厅心跳（需要定期进行 ping 操作，否则大厅认为我们不活动）
        ///     - 下载主机迁移数据并将其部署到新的 server world（执行主机迁移）
        ///     - 当使用新的分配连接到中继 server 时，使用新的加入代码更新大厅
        ///       所以其他 clients 现在可以连接到我
        ///   - 如果是主机迁移事件，并且我们将继续作为 client，我们需要等待新的加入
        ///     新主机已报告新中继分配的代码（所以基本上什么都不做）
        /// </summary>
        async Task CheckHostMigration(ILobbyChanges changes)
        {
            // 这些更改是否包括 hostId 修改？
            if (changes.HostId.Changed)
            {
                var newHostId = changes.HostId.Value;
                if (newHostId == m_PrevHostId)
                {
                    Debug.Log($"[HostMigration] Discarding host change event for the current host {m_PrevHostId}.");
                    return;
                }
                Debug.Log($"[HostMigration] Host change event. Previously '{m_PrevHostId}', now '{newHostId}'.");

                // 在这里切断 client 的中继连接，以确保它以后不会妨碍
                // 重新连接 client 时
                ResetClientNetworkDriver();

                // 我们是新主人吗？
                if (newHostId == CurrentPlayerId)
                {
                    Debug.Log($"[HostMigration] We are the new elected host!");

                    if (ClientServerBootstrap.ClientWorld == null)
                    {
                        Debug.Log($"No client world found during host migration event processing. Will initialize a new world.");

                        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
                        m_HostMigrationFrontend.LoadScenes(client);
                    }

                    if (FailNextHostMigration)
                    {
                        Debug.Log("[HostMigration] Manual host migration failure has been triggered. Aborting migration.");
                        return;
                    }

                    // 接管心跳职责
                    StartHeartbeat();

                    // 下载数据并进行大厅更新需要一些时间，更新 UI 发生的情况（这将在 client world 中发生，因为 server world 仅在最后创建）
                    var clientRelayEntity = HostMigrationHUD.SetWaitForRelayConnection(new WaitForRelayConnection() { WaitForHostSetup = true, IsHostMigration = true, StartTime = Time.realtimeSinceStartup});

                    Debug.Log($"[HostMigration] Fetching migration data information");
                    m_MigrationConfig = await LobbyService.Instance.GetMigrationDataInfoAsync(CurrentLobbyId);
                    Debug.Log($"[HostMigration] Migration Data Information: Expires:{m_MigrationConfig.Expires} (Now:{DateTime.Now}) MaxSize:{m_MigrationConfig.MaxSize} ReadUrl:{m_MigrationConfig.Read} WriteUrl:{m_MigrationConfig.Write}");

                    var data = await DownloadMigrationDataWithRetry();
                    if (!ValidateWorldsForMigration()) return;
                    Debug.Log($"[HostMigration] Received {data.Length} bytes host migration data. Switching self to host role.");
                    var success = await ListenAndConnectWithRelayAsHost(data);
                    if (!success)
                    {
                        if (m_CurrentLobby != null && !string.IsNullOrEmpty(CurrentLobbyId))
                            await LobbyService.Instance.RemovePlayerAsync(CurrentLobbyId, AuthenticationService.Instance.PlayerId);
                        return;
                    }

                    if (ClientServerBootstrap.ServerWorld == null)
                    {
                        Debug.LogError("[HostMigration] Failed to create server world during host migration event. Migration cannot be completed.");
                        return;
                    }

                    // TODO: 等待冷却、多个请求排队等

                    var waitEntityQuery = ClientServerBootstrap.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WaitForRelayConnection>());
                    if (!waitEntityQuery.TryGetSingletonEntity<WaitForRelayConnection>(out var serverRelayEntity))
                        serverRelayEntity = ClientServerBootstrap.ServerWorld.EntityManager.CreateEntity(ComponentType.ReadOnly<WaitForRelayConnection>());
                    waitEntityQuery.Dispose();
                    ClientServerBootstrap.ServerWorld.EntityManager.AddComponentData(serverRelayEntity, new WaitForRelayConnection() { IsHostMigration = true, StartTime = Time.realtimeSinceStartup});
                    ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(clientRelayEntity); // 清理，因为不再需要它

                    // 连接 server 迁移统计信息 HUD
                    var statsText = FindFirstObjectByType<HostMigrationHUD>().StatsText;
                    ClientServerBootstrap.ServerWorld.GetExistingSystemManaged<ServerHostMigrationHUDSystem>().StatsText = statsText;

                    // 禁用 client 状态 HUD
                    ClientServerBootstrap.ClientWorld.GetOrCreateSystemManaged<ClientHostMigrationHUDSystem>().Enabled = false;

                    await UpdateJoinCodeWhenReady();
                }
                else
                {
                    // 不是主人；如果我们是，请停止发送大厅心跳
                    StopHeartbeat();

                    // 在一次强制迁移中，该宿主仍然活着并且状况良好，但是
                    // 应该停止成为 server
                    if (ClientServerBootstrap.ServerWorld != null)
                    {
                        Debug.Log("[HostMigration] Disposing of Server world.");
                        ClientServerBootstrap.ServerWorld.Dispose();
                    }

                    // TODO: 此时应该始终有一个 client world，但似乎确实发生了这种情况并且需要调试
                    if (ClientServerBootstrap.ClientWorld != null)
                    {
                        HostMigrationHUD.SetWaitForRelayConnection(new WaitForRelayConnection() { WaitForJoinCode = true, OldJoinCode = RelayJoinCode, IsHostMigration = true, StartTime = Time.realtimeSinceStartup});

                        // 连接迁移统计信息 HUD
                        var statsText = FindFirstObjectByType<HostMigrationHUD>().StatsText;
                        var clientMigrationSystem = ClientServerBootstrap.ClientWorld.GetExistingSystemManaged<ClientHostMigrationHUDSystem>();
                        clientMigrationSystem.StatsText = statsText;
                    }
                    else
                    {
                        Debug.LogWarning("[HostMigration] No client world found during migration event.");
                    }

                    // CheckLobbyDataForNewRelayHost() will be called next to see if the present changes include a new
                    // 加入代码。但更有可能的是，一个单独的变更事件很快就会到来。
                    Debug.Log("[HostMigration] Host migration triggered, waiting for updates from new host before connecting");
                }
            }
        }

        /// <summary>
        /// 使用新的中继分配 ID 和加入代码更新大厅。
        /// 这向其他玩家发出信号，表明他们现在可以连接。我们需要等到连接到中继
        /// server 已建立，否则 clients 可能会在我们准备好接受传入之前尝试加入
        /// connections (such cases would result in errors on the client side).
        /// </summary>
        async Task UpdateJoinCodeWhenReady()
        {
            var serverWorld = ClientServerBootstrap.ServerWorld;
            var drvQuery = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            var networkStreamDriver = drvQuery.GetSingletonRW<NetworkStreamDriver>();
            drvQuery.Dispose();

            // 查找哪个驱动程序正在使用继电器
            int relayDriverNr = 0;
            for (var i = networkStreamDriver.ValueRO.DriverStore.FirstDriver;
                 i < networkStreamDriver.ValueRO.DriverStore.LastDriver;
                 ++i)
            {
                if (networkStreamDriver.ValueRO.DriverStore.GetDriverRO(i).GetRelayConnectionStatus() != RelayConnectionStatus.NotUsingRelay)
                {
                    relayDriverNr = i;
                    break;
                }
            }
            Debug.Log("[HostMigration] Waiting for relay connection before join code update");

            // 等待连接建立
            var relayNetworkDriver = networkStreamDriver.ValueRO.DriverStore.GetDriverRO(relayDriverNr);
            var startTime = Time.realtimeSinceStartup;
            serverWorld.EntityManager.CompleteAllTrackedJobs();
            var status = relayNetworkDriver.GetRelayConnectionStatus();
            while (status != RelayConnectionStatus.Established)
            {
                await Task.Delay(100);
                // Server world 在等待更新加入代码时已被破坏（很可能返回主菜单）
                if (serverWorld == null || !serverWorld.IsCreated)
                {
                    // 立即离开，因为我们是房东，需要其他人接管
                    LeaveLobby();
                    return;
                }
                serverWorld.EntityManager.CompleteAllTrackedJobs();
                status = relayNetworkDriver.GetRelayConnectionStatus();
                if (Time.realtimeSinceStartup - startTime > k_TimeoutForRelayConnectionSeconds)
                {
                    Debug.LogError($"[HostMigration] Timeout while waiting for relay connection before announcing join code ({status})");
                    return;
                }
            }

            var connectionTime = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[HostMigration] Relay connection established ({connectionTime:F2} s). Updating relay join code.");

            // 宣布加入代码，因为中继现在应该为 clients 做好准备
            UpdateLobbyOptions updateLobbyOptions = new UpdateLobbyOptions()
            {
                Data = new Dictionary<string, DataObject>(){
                    {
                        LobbyKeys.RelayHost,
                        new DataObject(DataObject.VisibilityOptions.Member, CurrentPlayerId)
                    },
                    {
                        LobbyKeys.RelayJoinCode,
                        new DataObject(DataObject.VisibilityOptions.Member, RelayJoinCode)
                    },
                },
            };
            m_CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobby.Id, updateLobbyOptions);
            Debug.Log($"[HostMigration] Updated relay join code in lobby");
        }

        /// <summary>
        /// 当主机断开连接时，中继连接不会立即删除，这可以通过以下方式强制
        /// 处置 NetworkDriver（通过重置驱动程序存储）。
        /// </summary>
        void ResetClientNetworkDriver()
        {
            var client = ClientServerBootstrap.ClientWorld;
            if (client == null) return;
            using var clientNetDebugQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetDebug>());
            var clientNetDebug = clientNetDebugQuery.GetSingleton<NetDebug>();
            var clientDriverStore = new NetworkDriverStore();
            DefaultDriverBuilder.DefaultDriverConstructor.CreateClientDriver(client, ref clientDriverStore, clientNetDebug);
            using var clientDriverQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            var clientDriver = clientDriverQuery.GetSingleton<NetworkStreamDriver>();
            clientDriver.ResetDriverStore(client.Unmanaged, ref clientDriverStore);
        }

        /// <summary>
        /// 检查新主机是否更新了大厅数据上的中继连接信息。
        /// 如果报告新的有效加入代码，我们将使用中继连接到新主机。
        /// </summary>
        async Task CheckLobbyDataForNewRelayHost(ILobbyChanges changes)
        {
            if (!changes.Data.Changed)
                return;

            Debug.Log($"[HostMigration] Data change event received.");
            var lobbyData = changes.Data.Value;
            if (lobbyData.ContainsKey(LobbyKeys.RelayJoinCode))
            {
                if (WaitForInitialJoin)
                {
                    Debug.Log($"[HostMigration] Ignoring host migration event as we've been waiting for the join code of the initial host.");
                    return;
                }

                if (m_PrevHostId == changes.HostId.Value)
                {
                    Debug.Log($"[HostMigration] Discarding host change event for the current host {m_PrevHostId}.");
                    return;
                }

                string relayHost;
                if (lobbyData.ContainsKey(LobbyKeys.RelayHost) && !lobbyData[LobbyKeys.RelayJoinCode].Removed)
                {
                    relayHost = lobbyData[LobbyKeys.RelayHost].Value.Value;
                }
                else if (m_CurrentLobby.Data.ContainsKey(LobbyKeys.RelayHost))
                {
                    relayHost = m_CurrentLobby.Data[LobbyKeys.RelayHost].Value;
                }
                else
                {
                    Debug.LogWarning($"[HostMigration] Ignoring unattributed relay join code; current host is {CurrentHostId}.");
                    return;
                }

                // 主机迁移后，大厅会在新大厅主机更新之前立即更新
                // 中继加入代码。在此更新中，（现已过时）来自先前大厅的中继信息
                // 主机需要被忽略。
                if (relayHost != CurrentHostId)
                {
                    Debug.Log($"[HostMigration] Ignoring stale relay join code from host {relayHost}; current host is {CurrentHostId}.");
                    return;
                }

                var newRelayJoinCode = lobbyData[LobbyKeys.RelayJoinCode].Value.Value;
                if (newRelayJoinCode == RelayJoinCode)
                {
                    Debug.Log($"[HostMigration] Discarding join code event for client (already using this join code '{RelayJoinCode}').");
                    return;
                }

                Debug.Log($"[HostMigration] New relay join code received {newRelayJoinCode}");
                await ConnectWithRelayAsClient(newRelayJoinCode);
            }
        }

        /// <summary>
        /// 在 client 成为主机之前，对当前 world 状态进行健全性检查
        /// 主机迁移事件。
        /// - 不能已有 server world，因为我们将创建一个新的
        /// - 不支持薄 clients
        /// - Client world 必须存在，它将从中继连接设置切换（旧主机）
        ///   至 IPC 至本地 server world
        /// </summary>
        static bool ValidateWorldsForMigration()
        {
            World clientWorld = default;
            foreach (var world in World.All)
            {
                if (world.IsServer())
                {
                    Debug.LogError("Server already present during host migration start. Aborting host migration.");
                    return false;
                }
                if (world.IsThinClient())
                {
                    Debug.LogError("Cannot migrate thin client to server! Aborting host migration.");
                    return false;
                }
                if (world.IsClient())
                {
                    if (clientWorld != default)
                    {
                        Debug.LogError("More than one client world present, this is not allowed. Aborting host migration.");
                        return false;
                    }
                    clientWorld = world;
                }
            }
            if (clientWorld == default)
            {
                Debug.LogError("No client world found during host migration. Aborting host migration.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// clients 的主机迁移例程仍为 clients。
        ///   - 没有创建新的 client world，但当前的 world 保持不变
        ///   - 使用新的继电器分配/加入代码重置 client 驱动程序存储
        ///   - 连接到新主机
        /// </summary>
        async Task ConnectWithRelayAsClient(string joinCode)
        {
            var clientMigrationSystem = ClientServerBootstrap.ClientWorld.GetExistingSystemManaged<ClientHostMigrationHUDSystem>();
            clientMigrationSystem.RelayJoinCode = joinCode;
            RelayJoinCode = joinCode;
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            await UpdatePlayerAllocationId(allocation.AllocationId);
            var relayData = allocation.ToRelayServerData(ConnectionType);
            var driverConstructor = new RelayDriverConstructor(new RelayServerData(), relayData);
            HostMigrationHelper.ConfigureClientAndConnect(ClientServerBootstrap.ClientWorld, driverConstructor, relayData.Endpoint);
        }

        /// <summary>
        /// 新主机的主机迁移例程。
        ///   - 创建新的中继分配和加入代码
        ///   - 创建新的 server world 并在其中部署迁移数据。
        /// </summary>
        async Task<bool> ListenAndConnectWithRelayAsHost(byte[] migrationData)
        {
            if (migrationData.Length == 0)
            {
                Debug.LogError($"No migration data given during host migration event.");
                return false;
            }

            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            Debug.Log($"[HostMigration] Created new relay allocation {allocation.AllocationId}");
            await UpdatePlayerAllocationId(allocation.AllocationId);

            var hostRelayData = allocation.ToRelayServerData(ConnectionType);
            var driverConstructor = new RelayDriverConstructor(hostRelayData, new RelayServerData());

            m_MigrationDataBlob.ResizeUninitialized(migrationData.Length);
            var arrayData = m_MigrationDataBlob.AsArray();
            var slice = new NativeSlice<byte>(arrayData, 0, migrationData.Length);
            slice.CopyFrom(migrationData);

            if (!HostMigrationHelper.MigrateDataToNewServerWorld(driverConstructor, ref arrayData))
            {
                Debug.LogError($"[HostMigration] HostMigration.MigrateDataToNewServerWorld failed. Aborting host migration.");
                return false;
            }

            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[HostMigration] Obtained joincode {joinCode} for allocation {allocation.AllocationId}");
            RelayJoinCode = joinCode;
            return true;
        }

        /// <summary>
        /// 为大厅中的本地玩家更新中继分配 ID。每次都需要执行此操作
        /// 分配发生变化，因此大厅和中继可以可靠地识别此端点（例如，如果玩家
        /// 中继超时，大厅也会知道）。
        /// </summary>
        /// <param name="allocationId"></param>
        public async Task UpdatePlayerAllocationId(Guid allocationId)
        {
            var updatePlayerOptions = new UpdatePlayerOptions() { AllocationId = allocationId.ToString() };
            m_CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobbyId, CurrentPlayerId, updatePlayerOptions);
            await LobbyService.Instance.ReconnectToLobbyAsync(CurrentLobbyId);
        }

        /// <summary>
        /// 按照 <see cref="k_HeartbeatIntervalSeconds"/> 指定的时间间隔向当前大厅发送心跳。
        /// 只有 server 需要发送心跳（保持大厅活动）。
        /// </summary>
        /// <returns></returns>
        IEnumerator HeartbeatLobbyCoroutine()
        {
            while (true)
            {
                if (m_CurrentLobby == null)
                    yield return null;

                try
                {
                    LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobbyId);
                }
                catch (LobbyServiceException ex)
                {
                    if (ex.Reason == LobbyExceptionReason.RateLimited)
                        Debug.LogWarning($"[HostMigration] Hit lobby heartbeat rate limit, will try again in {k_HeartbeatIntervalSeconds} seconds.");
                    else
                        Debug.LogError($"[HostMigration] Lobby exception while sending heartbeat: {ex.Message}.");
                }
                yield return new WaitForSecondsRealtime(k_HeartbeatIntervalSeconds);
            }
        }

        /// <summary>
        /// 初始大厅创建。在整个主机迁移过程中将保留相同的大厅，因此只需要
        /// 创建一次。
        /// </summary>
        public async Task CreateLobbyAsync(string joinCode, string allocationId)
        {
            // TODO: 处理可能达到速率限制的情况，等待成功

            RelayJoinCode = joinCode;
            var playerId = AuthenticationService.Instance.PlayerId;

            CreateLobbyOptions options = new CreateLobbyOptions();
            options.Data = new Dictionary<string, DataObject>()
            {
                {LobbyKeys.RelayHost, new DataObject(DataObject.VisibilityOptions.Member, playerId)},
                {LobbyKeys.RelayJoinCode, new DataObject(DataObject.VisibilityOptions.Member, joinCode)}
            };
            options.Player = new Player(id: playerId, allocationId: allocationId);

            m_CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(m_HostMigrationFrontend.LobbyName.text, k_DefaultMaxLobbyPlayers, options);
            Debug.Log($"[HostMigration] Created lobby {m_CurrentLobby.Id} with name '{m_HostMigrationFrontend.LobbyName.text}'");
            m_PrevHostId = CurrentHostId;

            m_MigrationConfig = await LobbyService.Instance.GetMigrationDataInfoAsync(m_CurrentLobby.Id);
            Debug.Log($"[HostMigration] Migration Data Information: Expires:{m_MigrationConfig.Expires} (Now:{DateTime.Now}) MaxSize:{m_MigrationConfig.MaxSize} ReadUrl:{m_MigrationConfig.Read} WriteUrl:{m_MigrationConfig.Write}");

            // 主机负责对大厅进行心跳以保持其活力
            StartHeartbeat();
        }

        /// <summary>
        /// 大厅活动的初始订阅。我们需要它来获取有关主机迁移事件的通知。这
        /// 只需要发生一次，因为大厅在主机迁移过程中保持完整。
        /// </summary>
        public async Task SubscribeToLobbyEvents()
        {
            if (string.IsNullOrEmpty(CurrentLobbyId))
                return;
            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += OnLobbyChanged;
            callbacks.KickedFromLobby += OnKickedFromLobby;
            try {
                await LobbyService.Instance.SubscribeToLobbyEventsAsync(m_CurrentLobby.Id, callbacks);
                Debug.Log($"[HostMigration] Subscribed to lobby events lobbyId:{m_CurrentLobby.Id}");
            }
            catch (LobbyServiceException ex)
            {
                switch (ex.Reason) {
                    case LobbyExceptionReason.AlreadySubscribedToLobby: Debug.LogWarning($"Already subscribed to lobby[{m_CurrentLobby.Id}]. We did not need to try and subscribe again. Exception Message: {ex.Message}"); break;
                    case LobbyExceptionReason.SubscriptionToLobbyLostWhileBusy: Debug.LogError($"Subscription to lobby events was lost while it was busy trying to subscribe. Exception Message: {ex.Message}"); throw;
                    case LobbyExceptionReason.LobbyEventServiceConnectionError: Debug.LogError($"Failed to connect to lobby events. Exception Message: {ex.Message}"); throw;
                    default: throw;
                }
            }
        }

        /// <summary>
        /// 特定大厅名称的初始加入处理。clients 期间只需要加入大厅一次
        /// 主机迁移时的游戏会话将继续使用相同的大厅。如果确切的大厅，这将会失败
        /// 未找到名称。
        /// </summary>
        public async Task JoinLobbyByNameAsync(string lobbyName)
        {
            var queryLobbiesOptions = new QueryLobbiesOptions();
            queryLobbiesOptions.Filters = new List<QueryFilter>() { new QueryFilter(QueryFilter.FieldOptions.Name, lobbyName, QueryFilter.OpOptions.EQ) };
            try
            {
                QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync();
                if (lobbies.Results.Count == 0)
                {
                    Debug.LogError($"[HostMigration] Lobby not found: '{lobbyName}'");
                    return;
                }

                var foundLobby = lobbies.Results.FirstOrDefault(x => x.Name.Equals(lobbyName));
                if (foundLobby != null)
                {
                    Debug.Log($"[HostMigration] Joining lobby name:{lobbyName} id:{foundLobby.Id} HostId:{foundLobby.HostId}");
                }
                else
                {
                    Debug.LogError($"[HostMigration] Lobby not found: '{lobbyName}'.");
                    m_HostMigrationFrontend.ClientConnectionStatus = $"Lobby '{lobbyName}' not found. Found {lobbies.Results.Count} other lobbies.";
                    foreach (var lobby in lobbies.Results)
                        Debug.LogWarning($"[HostMigration] Name:{lobby.Name} ID:{lobby.Id} HostID:{lobby.HostId}");
                    return;
                }

                // 主机 ID 需要在加入之前设置，因为在加入操作期间您可以获取该主机的主机迁移事件
                // （大厅所有者）但我们已经连接到他，因此需要忽略此事件
                m_PrevHostId = foundLobby.HostId;
                m_CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(foundLobby.Id);
            }
            catch (LobbyServiceException ex)
            {
                if (ex.Reason == LobbyExceptionReason.LobbyFull)
                {
                    Debug.LogError($"[HostMigration] Failed to join lobby because it is full");
                    m_HostMigrationFrontend.ClientConnectionStatus = "Lobby is full!";
                }
                else if (ex.Reason == LobbyExceptionReason.RateLimited)
                {
                    Debug.LogWarning($"[HostMigration] Hit lobby query rate limit while trying to join lobby '{lobbyName}', try again.");
                    return;
                }
                // TODO: 这主要是调试信息，以防意外发生此异常（稍后删除）
                if (m_CurrentLobby == null)
                    Debug.Log("DEBUG: No lobby instance found.");
                var joinedLobbies = await LobbyService.Instance.GetJoinedLobbiesAsync();
                if (joinedLobbies.Count > 0)
                {
                    foreach (var lobby in joinedLobbies)
                        Debug.Log($"DEBUG: Already joined lobby: {lobby}");
                    Debug.Log($"DEBUG: Getting first lobby: {joinedLobbies[0]}");
                    m_CurrentLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobbies[0]);
                }
                else
                {
                    return;
                }
            }
            Debug.Log($"[HostMigration] Joined lobby ID:{m_CurrentLobby.Id} HostID:{CurrentHostId}");
        }
#endif
    }
}
