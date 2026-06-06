using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GravityWell
{
    public partial struct BallSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;  // 我们希望这个 system 仅更新一次
            var config = SystemAPI.GetSingleton<Config>();

            // 产生球
            state.EntityManager.Instantiate(config.BallPrefab, config.BallCount, Allocator.Temp);

            // 将球分散在网格中，这样它们就不会在彼此的顶部生成
            const float spacing = 3;
            const float maxRowSize = 100;
            float minX = -maxRowSize / 2.0f;
            float x = minX;
            float y = 0;
            foreach (var ballTransform in
                     SystemAPI.Query<RefRW<LocalTransform>>()
                         .WithAll<Ball>())
            {
                ballTransform.ValueRW.Position = new float3(x, y, 0);
                x += spacing;
                if (x > maxRowSize) // 限制网格中每行的球数
                {
                    x = minX;
                    y += spacing;
                }
            }
        }
    }
}