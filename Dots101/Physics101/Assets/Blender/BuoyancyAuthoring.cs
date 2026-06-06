using Unity.Entities;
using UnityEngine;

namespace Blender
{
    public class BuoyancyAuthoring : MonoBehaviour
    {
        public float WaterLevel; // 水面高度（world 空间中）
        public float BuoyancyForce;
        public float Drag; // 水拖

        public class Baker : Baker<BuoyancyAuthoring>
        {
            public override void Bake(BuoyancyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Buoyancy
                {
                    WaterLevel = authoring.WaterLevel,
                    BuoyancyForce = authoring.BuoyancyForce,
                    Drag = authoring.Drag,
                });
                SetComponentEnabled<Buoyancy>(entity, false);
            }
        }
    }

    public struct Buoyancy : IComponentData, IEnableableComponent
    {
        public float WaterLevel;
        public float BuoyancyForce;
        public float Drag;
    }
}