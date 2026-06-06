using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HelloCube.FixedTimestep
{
    // 该 system 与 DefaultRateSpawner 几乎相同；关键的区别在于它更新在
    // FixedStepSimulationSystemGroup 而不是默认的 SimulationSystemGroup。
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct FixedRateSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExecuteFixedTimestep>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float spawnTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var spawner in
                     SystemAPI.Query<RefRO<FixedRateSpawner>>())
            {
                var projectileEntity = state.EntityManager.Instantiate(spawner.ValueRO.Prefab);
                var spawnPos = spawner.ValueRO.SpawnPos;
                spawnPos.y += 0.3f * math.sin(5.0f * spawnTime);

                SystemAPI.SetComponent(projectileEntity, LocalTransform.FromPosition(spawnPos));
                SystemAPI.SetComponent(projectileEntity, new Projectile { SpawnTime = spawnTime, SpawnPos = spawnPos, });
            }
        }
    }
}
