using Unity.Entities;
using UnityEngine;

namespace Blender
{
    public class BuoyancyZoneAuthoring : MonoBehaviour
    {
        public float WaterLevel; // 水面高度（world 空间中）
        public float BuoyancyForce;
        public float Drag; // 水拖

        public class Baker : Baker<BuoyancyZoneAuthoring>
        {
            public override void Bake(BuoyancyZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BuoyancyZone
                {
                    Buoyancy = new Buoyancy
                    {
                        WaterLevel = authoring.WaterLevel,
                        BuoyancyForce = authoring.BuoyancyForce,
                        Drag = authoring.Drag,
                    }
                });
            }
        }
    }

    public struct BuoyancyZone : IComponentData
    {
        public Buoyancy Buoyancy;
    }
}