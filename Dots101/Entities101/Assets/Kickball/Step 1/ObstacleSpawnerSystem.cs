using Tutorials.Kickball.Execute;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tutorials.Kickball.Step1
{
    // TransformSystemGroup 中的 systems 根据 entity 的 LocalTransform component 计算渲染矩阵。
    // UpdateBefore 属性使得此 system 在 TransformSystemGroup 之前更新，因此
    // 我们生成的障碍物将在同一帧而不是下一帧中计算其渲染矩阵。
    // （在这种情况下，如果没有此属性，大多数玩家不会注意到差异：障碍物只会
    // 一帧后出现。）
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ObstacleSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // RequireForUpdate<T> 导致 system 跳过更新
            // 只要 world 中不存在 component T 的实例。

            // 通常，system 将在加载主 scene 之前开始更新。通过使用 RequireForUpdate，
            // 我们可以使 system 跳过更新，直到从 scene 加载某些 components。

            // 这个 system 需要访问单例 component Config，其中
            // 在加载 scene 之前不会存在。
            state.RequireForUpdate<Config>();

            // 本示例中的 Execute* components 用于控制 systems run 中的 scenes。
            // 通过将 ExecuteAuthoring component 添加到子 scene 中的 GameObject 和
            // 检查 ObstacleSpawner 复选框，该类型的实例将在
            // scene，因此当 scene 加载时，此 system 将开始更新。
            state.RequireForUpdate<ObstacleSpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 我们只想产生一次障碍。禁用 system 将停止后续更新。
            state.Enabled = false;

            // GetSingleton 和 SetSingleton 方便访问
            // “单例”component（只有一个 entity 具有的 component 类型）。
            // 如果 0 个 entities 或 2 个或更多 entities 具有配置 component，则此 GetSingleton() 调用将抛出。
            var config = SystemAPI.GetSingleton<Config>();

            // 为了简单性和一致性，我们将使用固定的随机种子值。
            var rand = new Random(123);
            var scale = config.ObstacleRadius * 2;

            // 在网格中生成障碍物。
            for (int column = 0; column < config.NumColumns; column++)
            {
                for (int row = 0; row < config.NumRows; row++)
                {
                    // 实例化 entity 副本：使用所有相同的 component 类型创建新的 entity
                    // 和 component 值作为 ObstaclePrefab entity。
                    var obstacle = state.EntityManager.Instantiate(config.ObstaclePrefab);

                    // 通过设置 LocalTransform component 来定位新障碍物。
                    state.EntityManager.SetComponentData(obstacle, new LocalTransform
                    {
                        Position = new float3
                        {
                            x = (column * config.ObstacleGridCellSize) + rand.NextFloat(config.ObstacleOffset),
                            y = 0,
                            z = (row * config.ObstacleGridCellSize) + rand.NextFloat(config.ObstacleOffset)
                        },
                        Scale = scale,
                        Rotation = quaternion.identity
                    });
                }
            }
        }
    }
}
