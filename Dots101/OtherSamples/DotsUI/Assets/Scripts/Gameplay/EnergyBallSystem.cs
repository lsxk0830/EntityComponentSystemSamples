using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.DotsUISample
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct EnergyBallSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingletonRW<Player>();
            var playerEntity = SystemAPI.GetSingletonEntity<Player>();
            var playerPosition = SystemAPI.GetComponentRO<LocalTransform>(playerEntity).ValueRO.Position;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (transform, energy) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<Energy>>())
            {
                // 如果能量球已经收集完毕，让它绕玩家一圈飞行
                if (energy.ValueRO.Collected)
                {
                    var indexSeparation = energy.ValueRO.Index * math.PI / 2;
                    transform.ValueRW.Position = playerPosition + new float3(
                        (float)(math.cos(SystemAPI.Time.ElapsedTime * 5f + indexSeparation) * 1.5f),
                        1.5f,
                        (float)(math.sin(SystemAPI.Time.ElapsedTime * 5f + indexSeparation) * 1.5f));

                    continue;
                }

                float distance = math.distance(transform.ValueRO.Position.xz, playerPosition.xz);

                // 如果玩家距离能量球较远，请将其上下移动
                if (distance > 4f)
                {
                    transform.ValueRW.Position.y = (float)(math.sin(SystemAPI.Time.ElapsedTime) * 0.5f + 2f);
                }
                else if (distance < 5f)
                {
                    // 如果玩家靠近能量球，将其移向玩家
                    var t = SystemAPI.Time.DeltaTime * 5f;
                    transform.ValueRW.Position.xz = math.lerp(transform.ValueRO.Position.xz, playerPosition.xz, t);
                    transform.ValueRW.Scale = math.lerp(transform.ValueRO.Scale, 0.5f, t);

                    // 如果玩家足够近，收集能量球
                    if (distance < 1f)
                    {
                        energy.ValueRW.Collected = true;
                        transform.ValueRW.Scale = 0.5f;

                        player.ValueRW.EnergyCount++;
                        var eventEntity = ecb.CreateEntity();
                        ecb.AddComponent<Event>(eventEntity);
                        ecb.AddComponent<PickupEvent>(eventEntity);
                    }
                }
            }
        }
    }
}