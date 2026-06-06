using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace HelloCube.RandomSpawn
{
    public partial struct SpawnSystem : ISystem
    {
        uint m_SeedOffset;
        float m_SpawnTimer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ExecuteRandomSpawn>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            const int count = 200;
            const float spawnWait = 0.05f; // 0.05 秒

            m_SpawnTimer -= SystemAPI.Time.DeltaTime;
            if (m_SpawnTimer > 0)
            {
                return;
            }

            m_SpawnTimer = spawnWait;

            // 从前一帧中生成的 entities 中删除 NewSpawn 标签 component。
            var newSpawnQuery = SystemAPI.QueryBuilder().WithAll<NewSpawn>().Build();
            state.EntityManager.RemoveComponent<NewSpawn>(newSpawnQuery);

            // 生成盒子
            var prefab = SystemAPI.GetSingleton<Config>().Prefab;
            state.EntityManager.Instantiate(prefab, count, Allocator.Temp);

            // 每个生成的盒子都需要一个独特的种子，所以
            // seedOffset 必须按每帧的框数递增。
            m_SeedOffset += count;

            new RandomPositionJob { SeedOffset = m_SeedOffset }.ScheduleParallel();
        }
    }

    [WithAll(typeof(NewSpawn))]
    [BurstCompile]
    partial struct RandomPositionJob : IJobEntity
    {
        public uint SeedOffset;

        void Execute([EntityIndexInQuery] int index, ref LocalTransform transform)
        {
            // 具有相似种子的随机实例会产生相似的结果，因此要获得正确的结果
            // 这里的随机性，我们使用 CreateFromIndex，它对种子进行哈希处理。
            var random = Random.CreateFromIndex(SeedOffset + (uint)index);
            var xz = random.NextFloat2Direction() * 50;
            transform.Position = new float3(xz[0], 50, xz[1]);
        }
    }
}
