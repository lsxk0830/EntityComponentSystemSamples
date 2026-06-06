using System;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 负责使用从 <see cref="HostServer"/> 检索的加入代码加入中继 server。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ConnectingPlayer : SystemBase
    {
        Task<JoinAllocation> m_JoinTask;
        Task m_SetupTask;
        ClientStatus m_ClientStatus;
        string m_RelayJoinCode;
        NetworkEndpoint m_Endpoint;
        NetworkConnection m_ClientConnection;
        public RelayServerData RelayClientData;
        public RelayFrontend UIBehaviour;

        [Flags]
        enum ClientStatus
        {
            Unknown,
            FailedToConnect,
            Ready,
            GetJoinCodeFromHost,
            WaitForJoin,
            WaitForInit,
            WaitForSignIn,
        }

        protected override void OnCreate()
        {
            RequireForUpdate<EnableRelayServer>();
            m_ClientStatus = ClientStatus.Unknown;
        }

        public void GetJoinCodeFromHost()
        {
            m_ClientStatus = ClientStatus.GetJoinCodeFromHost;
        }

        public void JoinUsingCode(string joinCode)
        {
#if !UNITY_SERVER
            UIBehaviour.ClientConnectionStatus = "Waiting for relay response";
#endif
            m_RelayJoinCode = joinCode;
            m_SetupTask = UnityServices.InitializeAsync();
            m_ClientStatus = ClientStatus.WaitForInit;
        }

        protected override void OnUpdate()
        {
            switch (m_ClientStatus)
            {
                case ClientStatus.Ready:
                {
#if !UNITY_SERVER
                    UIBehaviour.ClientConnectionStatus = "Success";
#endif
                    m_ClientStatus = ClientStatus.Unknown;
                    return;
                }
                case ClientStatus.FailedToConnect:
                {
#if !UNITY_SERVER
                    UIBehaviour.ClientConnectionStatus = "Failed, check console";
#endif
                    m_ClientStatus = ClientStatus.Unknown;
                    return;
                }
                case ClientStatus.GetJoinCodeFromHost:
                {
#if !UNITY_SERVER
                    UIBehaviour.ClientConnectionStatus = "Waiting for join code from host server";
#endif
                    var hostServer = World.GetExistingSystemManaged<HostServer>();
                    m_ClientStatus = JoinUsingJoinCode(hostServer.JoinCode, out m_JoinTask);
                    return;
                }
                case ClientStatus.WaitForJoin:
                {
#if !UNITY_SERVER
                    UIBehaviour.ClientConnectionStatus = "Binding to relay server";
#endif
                    m_ClientStatus = WaitForJoin(m_JoinTask, out RelayClientData);
                    return;
                }
                case ClientStatus.WaitForInit:
                {
                    if (m_SetupTask.IsCompleted)
                    {
                        if (!AuthenticationService.Instance.IsSignedIn)
                        {
                            m_SetupTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                        }
                        m_ClientStatus = ClientStatus.WaitForSignIn;
                    }
                    return;
                }
                case ClientStatus.WaitForSignIn:
                {
                    if (m_SetupTask.IsCompleted)
                        m_ClientStatus = JoinUsingJoinCode(m_RelayJoinCode, out m_JoinTask);
                    return;
                }
                case ClientStatus.Unknown:
                default:
                    break;
            }
        }

        static ClientStatus WaitForJoin(Task<JoinAllocation> joinTask, out RelayServerData relayClientData)
        {
            if (!joinTask.IsCompleted)
            {
                relayClientData = default;
                return ClientStatus.WaitForJoin;
            }

            if (joinTask.IsFaulted)
            {
                relayClientData = default;
                Debug.LogError("Join Relay request failed");
                Debug.LogException(joinTask.Exception);
                return ClientStatus.FailedToConnect;
            }

            return BindToRelay(joinTask, out relayClientData);
        }

        static ClientStatus BindToRelay(Task<JoinAllocation> joinTask, out RelayServerData relayClientData)
        {
            // 从连接响应中收集并转换 Relay 数据
            var allocation = joinTask.Result;
#if !UNITY_WEBGL
            var connectionType = "dtls";
#else
            var connectionType = "wss";
#endif
            // 根据所需的 connectionType 格式化 server 数据
            try
            {
                relayClientData = PlayerRelayData(allocation, connectionType);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                relayClientData = default;
                return ClientStatus.FailedToConnect;
            }

            return ClientStatus.Ready;
        }

        static ClientStatus JoinUsingJoinCode(string hostServerJoinCode, out Task<JoinAllocation> joinTask)
        {
            if (hostServerJoinCode == null)
            {
                joinTask = null;
                return ClientStatus.GetJoinCodeFromHost;
            }

            // 发送加入请求到 Relay 服务
            joinTask = RelayService.Instance.JoinAllocationAsync(hostServerJoinCode);
            return ClientStatus.WaitForJoin;
        }

        static RelayServerData PlayerRelayData(JoinAllocation allocation, string connectionType = "dtls")
        {
            // 根据所需的 connectionType 选择端点
            var endpoint = RelayUtilities.GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
            if (endpoint == null)
            {
                throw new Exception($"endpoint for connectionType {connectionType} not found");
            }

            // 准备 Relay server 数据并计算随机数值
            // 加入主机的玩家会传递自己的 connectionData 以及主机的 connectionData
            var relayServerData = new RelayServerData(endpoint.Host, (ushort)endpoint.Port,
                allocation.AllocationIdBytes, allocation.ConnectionData, allocation.HostConnectionData, allocation.Key,
                endpoint.Secure, connectionType == "wss");

            return relayServerData;
        }
    }
}
