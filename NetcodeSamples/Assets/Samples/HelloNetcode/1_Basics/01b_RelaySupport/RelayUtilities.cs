using System.Collections.Generic;
using System.Linq;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 围绕不安全函数的必要包装器，用于将原始数据转换为各种中继结构。
    /// </summary>
    public static class RelayUtilities
    {
        public static RelayServerEndpoint GetEndpointForConnectionType(List<RelayServerEndpoint> endpoints, string connectionType)
        {
            return endpoints.FirstOrDefault(endpoint => endpoint.ConnectionType == connectionType);
        }
    }
}
