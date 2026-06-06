// 该代码用于 5g2。独特的 Collider Blob 共享演示并继承自 SpawnRandomObjectsSystemBase
// SpawnRandomObjectsSystemBase 中的 OnUpdate 方法将生成一个爆炸组，其定义在
// ConfigureInstance().
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using UnityEngine;
using Collider = Unity.Physics.Collider;

struct SpawnExplosionSettings : ISpawnSettings, IComponentData
{
    #region ISpawnSettings
    public Entity Prefab { get; set; }
    public float3 Position { get; set; }
    public quaternion Rotation { get; set; }
    public float3 Range { get; set; }
    public int Count { get; set; }
    public int RandomSeedOffset { get; set; }
    #endregion

    public int Id;
    public int Countdown;
    public float Force;
    public Entity Source;
}

public class SpawnExplosionAuthoring : MonoBehaviour
{
    [Header("Debris")]
    public GameObject Prefab;
    public int Count;

    [Header("Explosion")]
    public int Countdown;
    public float Force;

    internal static int Id = -1;
}

class SpawnExplosionAuthoringBaker : Baker<SpawnExplosionAuthoring>
{
    public override void Bake(SpawnExplosionAuthoring authoring)
    {
        var transform = GetComponent<Transform>();
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new SpawnExplosionSettings
        {
            Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
            Position = transform.position,
            Rotation = quaternion.identity,
            Count = authoring.Count,

            Id = SpawnExplosionAuthoring.Id--,
            Countdown = authoring.Countdown,
            Force = authoring.Force,
            Source = Entity.Null,
        });
    }
}

// ConfigureInstance 中的数据集输入到 SpawnRandomObjectsSystemBase 的 OnUpdate 方法中。OnUpdate 将
// 循环爆炸组实例（火箭）的数量，此 system 指定要实例化的 prefab
// 烟花的编号是 ExplosionDebris prefab。
partial class SpawnExplosionSystem : SpawnRandomObjectsSystemBase<SpawnExplosionSettings>
{
    // 用于将 colliders 分组，并为每个组创建单个 collider
    internal int GroupId;
    internal PhysicsCollider GroupCollider;

    protected override void OnCreate()
    {
        GroupId = 1;
    }

    protected override void OnDestroy() {}

    /// <summary>
    /// 第一次调用该方法时，ExplosionDebris 实例的 collider 被设为唯一。
    /// 在后续调用中，GroupId 将已经与 spawnSettings.Id 匹配，并且 collider 数据将与
    /// 在第一次调用中更新为 collider 数据集。因此，各爆炸组（火箭）的碎片
    /// 在同一个火箭中共享，但对于每个火箭实例都是唯一的。
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="spawnSettings"></param>
    internal override void ConfigureInstance(Entity instance, ref SpawnExplosionSettings spawnSettings)
    {
        // 每个爆炸组创建单个 collider
        if (GroupId != spawnSettings.Id)
        {
            GroupId = spawnSettings.Id;
            spawnSettings.Source = instance;

            var collider = EntityManager.GetComponentData<PhysicsCollider>(instance);
            var oldFilter = collider.Value.Value.GetCollisionFilter();

            // 每组只需要其中一个，因为里面的所有碎片
            // 一组将共享一个 collider
            // 这将使爆炸发生后一段时间内的碎片发生碰撞
            EntityManager.AddComponentData(instance, new ChangeFilterCountdown
            {
                Countdown = spawnSettings.Countdown * 2,
                Filter = oldFilter
            });

            // 为每个生成的爆炸组制作一个唯一的 collider
            collider.MakeUnique(instance, EntityManager);

            // 将 GroupIndex 设置为 GroupId，为负数
            // 这确保了一组内的碎片不会发生碰撞
            collider.Value.Value.SetCollisionFilter(new CollisionFilter
            {
                BelongsTo = oldFilter.BelongsTo,
                CollidesWith = oldFilter.CollidesWith,
                GroupIndex = GroupId
            });

            GroupCollider = collider;
        }

        // 将更新后的 collider 数据应用于爆炸组中的所有碎片
        EntityManager.SetComponentData(instance, GroupCollider);

        EntityManager.AddComponentData(instance, new ExplosionCountdown
        {
            Source = spawnSettings.Source,
            Countdown = spawnSettings.Countdown,
            Center = spawnSettings.Position,
            Force = spawnSettings.Force
        });
    }
}
