using Unity.Entities;
using Unity.NetCode.Samples;

namespace Samples.HelloNetcode
{
    // System 将 PlayerMovement component 注册到 ClientOnlyComponent 集合。
    // 注册仅在运行时执行，而不是在创建时执行（在 OnCreate 内）
    // 因为应该检查 EnableClientOnlyState 条件。
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct RegisterClientOnlyComponents : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnableClientOnlyState>();
            state.RequireForUpdate<ClientOnlyCollection>();
        }
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<ClientOnlyCollection>().ValueRW.RegisterClientOnlyComponentType(
                ComponentType.ReadWrite<PlayerMovement>());
            state.Enabled = false;
        }
    }
}
