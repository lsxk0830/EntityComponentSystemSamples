using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 负责接触继电器 server 和设置<see cref="RelayServerData"/>和<see cref="JoinCode"/>。
    /// 步骤包括：
    /// 1. 初始化服务
    /// 2. 登录
    /// 3.分配允许加入的玩家数量。
    /// 4. 检索加入代码
    /// 5.获取继电器 server 信息。I.e。IP-地址等
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HostServer : SystemBase
    {
        public RelayFrontend UIBehaviour;
        const int RelayMaxConnections = 5;
        public string JoinCode;

        public RelayServerData RelayServerData;
        HostStatus m_HostStatus;
        Task<List<Region>> m_RegionsTask;
        Task<Allocation> m_AllocationTask;
        Task<string> m_JoinCodeTask;
        Task m_InitializeTask;
        Task m_SignInTask;

        [Flags]
        enum HostStatus
        {
            Unknown,
            InitializeServices,
            Initializing,
            SigningIn,
            FailedToHost,
            Ready,
            Allocating,
            GettingJoinCode,
            GetRelayData,
        }

        protected override void OnCreate()
        {
            RequireForUpdate<EnableRelayServer>();
            m_HostStatus = HostStatus.InitializeServices;
        }

        protected override void OnUpdate()
        {
            switch (m_HostStatus)
            {
                case HostStatus.FailedToHost:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Failed check console";
#endif
                    m_HostStatus = HostStatus.Unknown;
                    return;
                }
                case HostStatus.Ready:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Success, players may now connect";
#endif
                    m_HostStatus = HostStatus.Unknown;
                    return;
                }
                case HostStatus.InitializeServices:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Initializing services";
#endif
                    m_InitializeTask = UnityServices.InitializeAsync();
                    m_HostStatus = HostStatus.Initializing;
                    return;
                }
                case HostStatus.Initializing:
                {
                    m_HostStatus = WaitForInitialization(m_InitializeTask, out m_SignInTask);
                    return;
                }
                case HostStatus.SigningIn:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Logging in anonymously";
#endif
                    m_HostStatus = WaitForSignIn(m_SignInTask, out m_AllocationTask);
                    return;
                }
                case HostStatus.Allocating:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Waiting for allocation";
#endif
                    m_HostStatus = WaitForAllocations(m_AllocationTask, out m_JoinCodeTask);
                    return;
                }
                case HostStatus.GettingJoinCode:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Waiting for join code";
#endif
                    m_HostStatus = WaitForJoin(m_JoinCodeTask, out JoinCode);
                    return;
                }
                case HostStatus.GetRelayData:
                {
#if !UNITY_SERVER
                    UIBehaviour.HostConnectionStatus = "Getting relay data";
#endif
                    m_HostStatus = BindToHost(m_AllocationTask, out RelayServerData);
                    return;
                }
                case HostStatus.Unknown:
                default:
                    break;
            }
        }

        static HostStatus WaitForSignIn(Task signInTask, out Task<Allocation> allocationTask)
        {
            allocationTask = default;
            if (!signInTask.IsCompleted)
            {
                return HostStatus.SigningIn;
            }

            if (signInTask.IsFaulted)
            {
                Debug.LogError("Signing in failed");
                Debug.LogException(signInTask.Exception);
                return HostStatus.FailedToHost;
            }

            // 请求分配 Relay 服务
            // 最多 5 个对等连接，最多 6 名玩家。
            allocationTask = RelayService.Instance.CreateAllocationAsync(RelayMaxConnections);
            return HostStatus.Allocating;
        }

        static HostStatus WaitForInitialization(Task initializeTask, out Task nextTask)
        {
            if (!initializeTask.IsCompleted)
            {
                nextTask = default;
                return HostStatus.Initializing;
            }

            if (initializeTask.IsFaulted)
            {
                Debug.LogError("UnityServices Initialization failed");
                Debug.LogException(initializeTask.Exception);
                nextTask = default;
                return HostStatus.FailedToHost;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                nextTask = Task.CompletedTask;
                return HostStatus.SigningIn;
            }
            else
            {
                nextTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                return HostStatus.SigningIn;
            }
        }

        // 绑定并监听 Relay server
        static HostStatus BindToHost(Task<Allocation> allocationTask, out RelayServerData relayServerData)
        {
            var allocation = allocationTask.Result;
#if !UNITY_WEBGL
            var connectionType = "dtls";
#else
            var connectionType = "wss";
#endif
            try
            {
                // 根据所需的 connectionType 格式化 server 数据
                relayServerData = HostRelayData(allocation, connectionType);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                relayServerData = default;
                return HostStatus.FailedToHost;
            }
            return HostStatus.Ready;
        }

        // 获取加入代码，然后您可以与 clients 分享，以便他们可以加入
        static HostStatus WaitForJoin(Task<string> joinCodeTask, out string joinCode)
        {
            joinCode = null;
            if (!joinCodeTask.IsCompleted)
            {
                return HostStatus.GettingJoinCode;
            }

            if (joinCodeTask.IsFaulted)
            {
                Debug.LogError("Create join code request failed");
                Debug.LogException(joinCodeTask.Exception);
                return HostStatus.FailedToHost;
            }

            joinCode = joinCodeTask.Result;
            return HostStatus.GetRelayData;
        }

        static HostStatus WaitForAllocations(Task<Allocation> allocationTask, out Task<string> joinCodeTask)
        {
            if (!allocationTask.IsCompleted)
            {
                joinCodeTask = null;
                return HostStatus.Allocating;
            }

            if (allocationTask.IsFaulted)
            {
                Debug.LogError("Create allocation request failed");
                Debug.LogException(allocationTask.Exception);
                joinCodeTask = null;
                return HostStatus.FailedToHost;
            }

            // 向 Relay 服务请求加入代码
            var allocation = allocationTask.Result;
            joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return HostStatus.GettingJoinCode;
        }

        // connectionType 也支持 udp，但不推荐这样做
        static RelayServerData HostRelayData(Allocation allocation, string connectionType = "dtls")
        {
            // 根据所需的 connectionType 选择端点
            var endpoint = RelayUtilities.GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
            if (endpoint == null)
            {
                throw new InvalidOperationException($"endpoint for connectionType {connectionType} not found");
            }
            // 准备 Relay server 数据并计算随机数值
            // 主机将其 connectionData 两次传递到此函数中
            var isWebSocket = connectionType == "wss" || connectionType == "ws";
            var relayServerData = new RelayServerData(endpoint.Host, (ushort)endpoint.Port,
                allocation.AllocationIdBytes, allocation.ConnectionData, allocation.ConnectionData,
                allocation.Key, endpoint.Secure, isWebSocket);

            return relayServerData;
        }
    }
}
