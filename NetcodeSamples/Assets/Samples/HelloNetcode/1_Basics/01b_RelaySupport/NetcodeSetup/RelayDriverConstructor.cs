using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 使用继电器 server 设置注册 client 和 server。
    ///
    /// 从引导程序 world 检索设置。当按下“开始游戏”时，该驱动程序构造函数将显示 run
    /// 仅应在 server 和 client 配置正确初始化后按下。
    /// </summary>
    public class RelayDriverConstructor : INetworkStreamDriverConstructor
    {
        RelayServerData m_RelayClientData;
        RelayServerData m_RelayServerData;

        public RelayDriverConstructor(RelayServerData serverData, RelayServerData clientData)
        {
            m_RelayServerData = serverData;
            m_RelayClientData = clientData;
        }

        /// <summary>
        /// 此方法将确保我们根据继电器设置注册不同的驱动程序类型
        /// 设置。
        /// <para>
        /// 模式|  Relay 设置
        /// Client/Server |  有效->使用中继连接本地 server
        ///                  无效->使用 IPC 连接本地 server
        /// Client |  始终使用继电器。期望数据有效。
        /// <para>
        /// <para>
        /// 对于 WebGL，Editor 中的 client 始终首选 websocket，以密切模拟玩家行为。
        /// </para>
        /// </summary>
        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = DefaultDriverBuilder.GetNetworkClientSettings();
            //如果中继数据无效，则通过本地 ipc 连接
            if(ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.ClientAndServer &&
               !m_RelayClientData.Endpoint.IsValid)
            {
                DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug, settings);
            }
            else
            {
                settings.WithRelayParameters(ref m_RelayClientData);
#if !UNITY_WEBGL
                DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
#else
                DefaultDriverBuilder.RegisterClientWebSocketDriver(world, ref driverStore, netDebug, settings);
#endif
            }
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            //第一个驱动程序是 IPC，用于内部 client/server（如有必要）。
            var ipcSettings = DefaultDriverBuilder.GetNetworkServerSettings();
            DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driverStore, netDebug, ipcSettings);
            var relaySettings = DefaultDriverBuilder.GetNetworkServerSettings();
            //另一个驱动程序（仍然是同一端口）将使用中继来监听外部连接
            relaySettings.WithRelayParameters(ref m_RelayServerData);
#if !UNITY_WEBGL
            DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, relaySettings);
#else
            DefaultDriverBuilder.RegisterServerWebSocketDriver(world, ref driverStore, netDebug, relaySettings);
#endif
        }
    }
}
