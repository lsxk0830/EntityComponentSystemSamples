using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace KickBall
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ObstacleSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EntityPrefabs>();
            state.RequireForUpdate<ObstacleConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            var prefabs = SystemAPI.GetSingleton<EntityPrefabs>();
            var obstacleConfig = SystemAPI.GetSingleton<ObstacleConfig>();

            // 为了简单性和一致性，我们将使用固定的随机种子值。
            var rand = new Random(123);

            var prefabTransform = state.EntityManager.GetComponentData<LocalTransform>(prefabs.Obstacle);

            // 在网格中生成障碍物。
            for (int column = 0; column < obstacleConfig.NumColumns; column++)
            {
                for (int row = 0; row < obstacleConfig.NumRows; row++)
                {
                    var obstacle = state.EntityManager.Instantiate(prefabs.Obstacle);

                    prefabTransform.Position = new float3
                    {
                        x = (column * obstacleConfig.GridCellSize) + rand.NextFloat(obstacleConfig.Offset),
                        y = 0,
                        z = (row * obstacleConfig.GridCellSize) + rand.NextFloat(obstacleConfig.Offset)
                    };

                    state.EntityManager.SetComponentData(obstacle, prefabTransform);
                }
            }
        }
    }
}
