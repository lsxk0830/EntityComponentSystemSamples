using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    // 将任何已建立的网络连接放置在游戏中，以便 ghost snapshot 同步可以开始
    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class GoInGameSystem : SystemBase
    {
        private EntityQuery m_NewConnections;

        protected override void OnCreate()
        {
            RequireForUpdate<EnableGoInGame>();
            m_NewConnections = SystemAPI.QueryBuilder().WithAll<NetworkId>().WithNone<NetworkStreamInGame>().Build();
            RequireForUpdate(m_NewConnections);
        }

        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            FixedString32Bytes worldName = World.Name;

            // 连接建立后立即进入游戏（连接网络 ID 已设置）
            foreach (var (id, ent) in SystemAPI.Query<NetworkId>().WithNone<NetworkStreamInGame>().WithEntityAccess())
            {
                UnityEngine.Debug.Log($"[{worldName}] Go in game connection {id.Value}");
                commandBuffer.AddComponent<NetworkStreamInGame>(ent);
            }

            commandBuffer.Playback(EntityManager);
        }
    }
}
