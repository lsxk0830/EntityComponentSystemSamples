using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;

namespace Streaming.SceneManagement.CompleteSample
{
    public class TileInfoAuthoring : MonoBehaviour
    {
        public int randomSeed;
        public float tileSize; // 正方形边长的大小
        public int2 minBoundary;
        public int2 maxBoundary;
        public List<TileTemplate> tileTemplates;

#if UNITY_EDITOR
        public class Baker : Baker<TileInfoAuthoring>
        {
            public override void Bake(TileInfoAuthoring authoring)
            {
                // 制作副本以过滤包含 null scenes 的条目
                List<TileTemplate> tileTemplates =
                    new List<TileTemplate>(authoring.tileTemplates.Count);
                List<EntitySceneReference> sceneReferences =
                    new List<EntitySceneReference>(authoring.tileTemplates.Count);

                // 获取 scene 参考
                foreach (var tileTemplate in authoring.tileTemplates)
                {
                    if (tileTemplate != null)
                    {
                        // 我们希望创建对 scene 的依赖关系，以防 scene 被删除
                        // 这需要在 authoring.scene!= null 检查之外，以防资产文件被删除然后恢复。
                        DependsOn(tileTemplate.tileScene);

                        if (tileTemplate.tileScene != null)
                        {
                            tileTemplates.Add(tileTemplate);
                            sceneReferences.Add(new EntitySceneReference(tileTemplate.tileScene));
                        }
                    }
                }

                int2 min = math.min(authoring.minBoundary, authoring.maxBoundary);
                int2 max = math.max(authoring.minBoundary, authoring.maxBoundary);

                var random = new Unity.Mathematics.Random((uint)authoring.randomSeed);
                float2 tileSize = new float2(authoring.tileSize, authoring.tileSize);

                // 从图案中选择 world 的瓷砖
                for (int x = min.x; x <= max.x; ++x)
                {
                    for (int y = min.y; y <= max.y; ++y)
                    {
                        int selectedTile = random.NextInt(0, tileTemplates.Count);

                        var tileEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, $"Tile {x}_{y}");

                        // 存储信息以将 scene 实例化到正确的图块位置
                        var loadingDistance = tileTemplates[selectedTile].loadingDistance;
                        var unloadingDistance = tileTemplates[selectedTile].unloadingDistance;
                        AddComponent(tileEntity, new TileInfo
                        {
                            Scene = sceneReferences[selectedTile],
                            Position = tileSize * new float2(x, y),
                            Rotation = random.NextInt(4) * (math.PI / 2f),  // 0、90、180 或 270 度
                            LoadingDistanceSq = loadingDistance * loadingDistance,
                            UnloadingDistanceSq = unloadingDistance * unloadingDistance
                        });

                        // 此 component 将存储到相关 entities 的距离
                        AddComponent<DistanceToRelevant>(tileEntity);
                    }
                }
            }
        }
#endif

        [Serializable]
        public class TileTemplate
        {
#if UNITY_EDITOR
            public UnityEditor.SceneAsset tileScene; // 实例化 scene
#endif
            public float loadingDistance; // 考虑加载 scene 的接近距离
            public float unloadingDistance; // 考虑卸载 scene 的邻近距离
        }
    }

    public struct TileInfo : IComponentData
    {
        public EntitySceneReference Scene; // scene 实例
        public float2 Position;
        public float Rotation;
        public float LoadingDistanceSq;
        public float UnloadingDistanceSq;
    }

    public struct DistanceToRelevant : IComponentData
    {
        public float DistanceSq;
    }
}
