using System;
using AutoAuthoring;
using Unity.Entities;
using Unity.Mathematics;

namespace Baking.AutoAuthoring
{
    public class SpawnerAuthoring : AutoAuthoring<Spawner>
    {
        // Defining OnEnable() makes the inspector show the enabled component checkbox.
        // 禁用的 components 不会被烘焙。
        void OnEnable() {}
    }

    [Serializable]
    public struct Spawner : IComponentData
    {
        public Entity Prefab;
        public float3 Offset;
        public int InstanceCount;
    }
}
