// 此演示的目的是展示如何在运行时创建网格 colliders。该演示使用 trigger 事件作为一种方式
// 在运行时进行交互以发生 collider 创建事件。
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics
{
    public class CreateTileGridSpawner : MonoBehaviour
    {
        public GameObject GridPrefab;
        public GameObject WallPrefab;

        class CreateTileGridBaker : Baker<CreateTileGridSpawner>
        {
            public override void Bake(CreateTileGridSpawner authoring)
            {
                DependsOn(authoring.GridPrefab);
                if (authoring.GridPrefab == null || authoring.WallPrefab == null) return;
                var prefabGridEntity = GetEntity(authoring.GridPrefab, TransformUsageFlags.Dynamic);
                var prefabWallEntity = GetEntity(authoring.WallPrefab, TransformUsageFlags.Dynamic);

                var createComponent = new CreateTileGridSpawnerComponent
                {
                    GridEntity = prefabGridEntity,
                    WallEntity = prefabWallEntity,
                    SpawningPosition = authoring.transform.position
                };
                var gridSpawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(gridSpawnerEntity, createComponent);
            }
        }
    }

    public struct CreateTileGridSpawnerComponent : IComponentData
    {
        public Entity GridEntity;
        public Entity WallEntity;
        public float3 SpawningPosition;
    }

    public struct WallsTagComponent : IComponentData
    {
    }
}
