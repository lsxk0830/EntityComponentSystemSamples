using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Asteroids.Client
{
    /// <summary>
    /// Asteroids 示例中 clients 的自定义主机迁移逻辑。立即再次将 client 放入游戏
    /// 主机迁移后。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct HostMigrationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<LevelComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var evt in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                var reconnected = SystemAPI.GetComponentLookup<NetworkStreamIsReconnected>();
                if (evt.State == ConnectionState.State.Connected && reconnected.HasComponent(evt.ConnectionEntity))
                {
                    state.EntityManager.AddComponent<NetworkStreamInGame>(evt.ConnectionEntity);
                    // 完成后删除重新连接标签
                    state.EntityManager.RemoveComponent<NetworkStreamIsReconnected>(evt.ConnectionEntity);
                }
            }
        }
    }
}
