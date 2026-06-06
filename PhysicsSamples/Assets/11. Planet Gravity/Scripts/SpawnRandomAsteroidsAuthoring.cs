using System;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

class SpawnRandomAsteroidsAuthoring : SpawnRandomObjectsAuthoringBase<AsteroidSpawnSettings>
{
    public float massFactor = 1;

    internal override void Configure(ref AsteroidSpawnSettings spawnSettings) => spawnSettings.MassFactor = massFactor;
}

class SpawnRandomAsteroidsAuthoringBaker : SpawnRandomObjectsAuthoringBaseBaker<SpawnRandomAsteroidsAuthoring, AsteroidSpawnSettings>
{
    internal override void Configure(SpawnRandomAsteroidsAuthoring authoring, ref AsteroidSpawnSettings spawnSettings) => spawnSettings.MassFactor = authoring.massFactor;
}

struct AsteroidSpawnSettings : IComponentData, ISpawnSettings
{
    public Entity Prefab { get; set; }
    public float3 Position { get; set; }
    public quaternion Rotation { get; set; }
    public float3 Range { get; set; }
    public int Count { get; set; }
    public int RandomSeedOffset { get; set; }
    public float MassFactor;
}

partial class SpawnRandomAsteroidsSystem : SpawnRandomObjectsSystemBase<AsteroidSpawnSettings>
{
    Random m_RandomMass;

    internal override int GetRandomSeed(AsteroidSpawnSettings spawnSettings)
    {
        var seed = base.GetRandomSeed(spawnSettings);
        // 历史记录：这曾经是“^ spawnSettings.Prefab.GetHashCode()，但 prefab 的哈希在代码更改中并不稳定。
        // 现在它是硬编码的。如果同一 scene 中的两个生成器仅在它们生成的 prefab 上有所不同，则设置它们的 RandomSeedOffset 字段
        // 不同的值来区分它们。
        seed = (seed * 397) ^ 220;
        seed = (seed * 397) ^ (int)(spawnSettings.MassFactor * 1000);
        return seed;
    }

    internal override void OnBeforeInstantiatePrefab(ref AsteroidSpawnSettings spawnSettings)
    {
        m_RandomMass = new Random();
        m_RandomMass.InitState(10);
    }

    internal override void ConfigureInstance(Entity instance, ref AsteroidSpawnSettings spawnSettings)
    {
        var mass = EntityManager.GetComponentData<PhysicsMass>(instance);
        var halfMassFactor = spawnSettings.MassFactor * 0.5f;
        mass.InverseMass = m_RandomMass.NextFloat(mass.InverseMass * math.rcp(halfMassFactor), mass.InverseMass * halfMassFactor);
        EntityManager.SetComponentData(instance, mass);
    }
}
