// 该代码用于 5g2。独特的 Collider Blob 共享演示并继承自 SpawnRandomObjectsSystemBase
// SpawnRandomObjectsSystemBase 中的 OnUpdate 方法将生成一个爆炸组，其定义在
// ConfigureInstance(). The prefab being spawned is ExplosionSpawner.
using System.Collections.Generic;
using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

public struct PeriodicallySpawnExplosionComponent : IComponentData, ISpawnSettings, IPeriodicSpawnSettings
{
    public Entity Prefab { get; set; }
    public float3 Position { get; set; }
    public quaternion Rotation { get; set; }
    public float3 Range { get; set; }
    public int Count { get; set; }
    public int RandomSeedOffset { get; set; }

    public int SpawnRate { get; set; }
    public int DeathRate { get; set; }
    public int Id;
}

public class PeriodicallySpawnExplosionAuthoring : MonoBehaviour
{
    public float3 Range;
    public int SpawnRate;
    public GameObject Prefab;
    public int Count = 1;
}

class PeriodicallySpawnExplosionAuthoringBaking : Baker<PeriodicallySpawnExplosionAuthoring>
{
    public override void Bake(PeriodicallySpawnExplosionAuthoring authoring)
    {
        var transform = GetComponent<Transform>();
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new PeriodicallySpawnExplosionComponent
        {
            Count = authoring.Count,
            DeathRate = 10,
            Position = transform.position,
            Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
            Range = authoring.Range,
            Rotation = quaternion.identity,
            SpawnRate = authoring.SpawnRate,
            Id = 0,
        });
    }
}

// ConfigureInstance 中的数据集输入到 PeriodicalySpawnRandomObjectsSystem 的 OnUpdate 方法中。这个 system
// 更新 ExplosionSpawner prefab（烟花火箭预爆炸）。
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
partial class PeriodicallySpawnExplosionsSystem : PeriodicalySpawnRandomObjectsSystem<PeriodicallySpawnExplosionComponent>
{
    internal override void ConfigureInstance(Entity instance, ref PeriodicallySpawnExplosionComponent spawnSettings)
    {
        Assert.IsTrue(EntityManager.HasComponent<SpawnExplosionSettings>(instance));

        var explosionComponent = EntityManager.GetComponentData<SpawnExplosionSettings>(instance);

        var localTransform = EntityManager.GetComponentData<LocalTransform>(instance);

        // 想要负 ID 与 CollisionFilter GroupIndex 一起使用
        spawnSettings.Id--;

        // 设置新爆炸组的 ID，使该组获得唯一的 collider
        explosionComponent.Id = spawnSettings.Id;

        explosionComponent.Position = localTransform.Position;

        EntityManager.SetComponentData(instance, explosionComponent);
    }
}
