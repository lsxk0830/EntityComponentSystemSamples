using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    [UpdateAfter(typeof(ShootingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ApplyHitMarkSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<Hit>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            // 回滚时不执行 hit-scan，仅在模拟最新 tick 时执行
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            var isServer = state.WorldUnmanaged.IsServer();
            foreach (var (hit, serverHitMarker, clientHitMarker) in SystemAPI.Query<RefRO<Hit>, RefRW<ServerHitMarker>, RefRW<ClientHitMarker>>().WithAll<Simulate>())
            {
                if (hit.ValueRO.Victim == Entity.Null)
                {
                    continue;
                }

                if (isServer)
                {
                    serverHitMarker.ValueRW.Victim = hit.ValueRO.Victim;
                    serverHitMarker.ValueRW.HitPoint = hit.ValueRO.HitPoint;
                    serverHitMarker.ValueRW.ServerHitTick = hit.ValueRO.Tick;
                }
                else
                {
                    clientHitMarker.ValueRW.Victim = hit.ValueRO.Victim;
                    clientHitMarker.ValueRW.HitPoint = hit.ValueRO.HitPoint;
                    clientHitMarker.ValueRW.ClientHitTick = hit.ValueRO.Tick;
                }
            }
        }
    }
}
