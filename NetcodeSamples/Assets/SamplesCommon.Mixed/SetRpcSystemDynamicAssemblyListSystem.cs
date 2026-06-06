using System;
using Unity.Entities;

namespace Unity.NetCode.Samples.Common
{
    /// <summary>
    ///     我们使用动态程序集列表，因此我们可以使用程序集的子集构建 server
    ///     （仅包括其中一个示例，而不是全部）。
    ///     如果项目中只有一个游戏，通常不需要启用 DynamicAssemblyList。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    public partial struct SetRpcSystemDynamicAssemblyListSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<RpcCollection>().ValueRW.DynamicAssemblyList = true;
            state.Enabled = false;
        }
    }
}
