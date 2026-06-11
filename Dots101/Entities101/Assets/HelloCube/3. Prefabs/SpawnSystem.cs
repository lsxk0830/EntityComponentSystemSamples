using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HelloCube.Prefabs
{
    public partial struct SpawnSystem : ISystem
    {
        uint m_UpdateCounter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Spawner>();
            state.RequireForUpdate<ExecutePrefabs>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 创建一个与所有具有 RotationSpeed component 的 query 匹配的 query。
            // （query 在源生成中缓存，因此不会产生每次更新时重新创建它的成本。）
            var spinningCubesQuery = SystemAPI.QueryBuilder().WithAll<RotationSpeed>().Build();

            // 仅当当前不存在立方体时才生成立方体。
            if (spinningCubesQuery.IsEmpty)
            {
                var prefab = SystemAPI.GetSingleton<Spawner>().Prefab;

                // 实例化 entity 会创建具有相同 component 类型和值的副本 entities。
                var instances = state.EntityManager.Instantiate(prefab, 500, Allocator.Temp);

                // 与新的 Random() 不同，CreateFromIndex() 对随机种子进行哈希处理，这样相似的种子就不会产生相似的结果。
                var random = Random.CreateFromIndex(m_UpdateCounter++);

                foreach (var entity in instances)
                {
                    // 使用新位置更新 entity 的 LocalTransform component。
                    var transform = SystemAPI.GetComponentRW<LocalTransform>(entity);
                    transform.ValueRW.Position = (random.NextFloat3() - new float3(0.5f, 0, 0.5f)) * 20;
                }
            }
        }
    }
}
