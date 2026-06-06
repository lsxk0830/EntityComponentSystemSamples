//#定义 ENABLE_NETCODE_SAMPLE_SECURE
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
#if ENABLE_NETCODE_SAMPLE_SECURE
    /// <summary>
    /// 使用 TLS 配置注册 client 和 server。
    /// 配置是从 <see cref="SecureParameters"/> 检索的。
    /// </summary>
    public struct SecureDriverConstructor : INetworkStreamDriverConstructor
    {
        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            DefaultDriverBuilder.RegisterClientDriver(
                world, ref driverStore, netDebug,
                caCertificate: ref SecureParameters.GameClientCA,
                serverName: ref SecureParameters.ServerCommonName);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            DefaultDriverBuilder.RegisterServerDriver(
                world, ref driverStore, netDebug,
                certificate: ref SecureParameters.GameServerCertificate,
                privateKey: ref SecureParameters.GameServerPrivate);
        }
    }
#endif
}
