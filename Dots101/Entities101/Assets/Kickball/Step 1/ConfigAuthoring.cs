using Unity.Entities;
using UnityEngine;

namespace Tutorials.Kickball.Step1
{
    // 配置 component 将用作单例（意味着只有一个 entity 将具有此 component）。
    // 它存储了一堆游戏参数以及我们将在运行时实例化的 entity prefab。

    public class ConfigAuthoring : MonoBehaviour
    {
        // 其中大部分字段在步骤 1 中未使用，但将在后续步骤中使用。
        public int ObstaclesNumRows;
        public int ObstaclesNumColumns;
        public float ObstacleGridCellSize;
        public float ObstacleRadius;
        public float ObstacleOffset;
        public float PlayerOffset;
        public float PlayerSpeed;
        public float BallStartVelocity;
        public float BallVelocityDecay;
        public float BallKickingRange;
        public float BallKickForce;
        public GameObject ObstaclePrefab;
        public GameObject PlayerPrefab;
        public GameObject BallPrefab;

        class Baker : Baker<ConfigAuthoring>
        {
            public override void Bake(ConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                // 每个 authoring 字段对应一个同名的 component 字段。
                AddComponent(entity, new Config
                {
                    NumRows = authoring.ObstaclesNumRows,
                    NumColumns = authoring.ObstaclesNumColumns,
                    ObstacleGridCellSize = authoring.ObstacleGridCellSize,
                    ObstacleRadius = authoring.ObstacleRadius,
                    ObstacleOffset = authoring.ObstacleOffset,
                    PlayerOffset = authoring.PlayerOffset,
                    PlayerSpeed = authoring.PlayerSpeed,
                    BallStartVelocity = authoring.BallStartVelocity,
                    BallVelocityDecay = authoring.BallVelocityDecay,
                    BallKickingRangeSQ = authoring.BallKickingRange * authoring.BallKickingRange,
                    BallKickForce = authoring.BallKickForce,
                    // GetEntity() bakes a GameObject prefab into its entity equivalent.
                    ObstaclePrefab = GetEntity(authoring.ObstaclePrefab, TransformUsageFlags.Dynamic),
                    PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                    BallPrefab = GetEntity(authoring.BallPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }

    public struct Config : IComponentData
    {
        public int NumRows; // 障碍物和玩家在网格中生成，每个单元格一个障碍物和玩家
        public int NumColumns;
        public float ObstacleGridCellSize;
        public float ObstacleRadius;
        public float ObstacleOffset;
        public float PlayerOffset;
        public float PlayerSpeed; // 米每秒
        public float BallStartVelocity;
        public float BallVelocityDecay;
        public float BallKickingRangeSQ; // 球员必须离球多近才能踢球的平方距离
        public float BallKickForce;
        public Entity ObstaclePrefab;
        public Entity PlayerPrefab;
        public Entity BallPrefab;
    }
}
