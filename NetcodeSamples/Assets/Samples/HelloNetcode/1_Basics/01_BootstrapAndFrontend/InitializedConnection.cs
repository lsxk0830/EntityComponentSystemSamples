using Unity.Entities;

namespace Samples.HelloNetcode
{
    // 这个 component 用于将连接标记为已初始化，以避免
    // 它们被多次处理。
    public struct InitializedConnection : IComponentData { }
}
