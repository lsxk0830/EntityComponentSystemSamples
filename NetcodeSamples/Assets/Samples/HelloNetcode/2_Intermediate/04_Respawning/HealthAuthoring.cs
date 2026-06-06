using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <remarks>
    /// 使用 short 是为了我们不必处理浮点不精确，
    /// 并确保我们可以走向消极，如果有治疗或类似的积极 HP 操作，这可能会发挥作用。
    /// </remarks>
    public struct Health : IComponentData
    {
        [GhostField(Smoothing = SmoothingAction.Clamp)] public short MaximumHitPoints;
        [GhostField(Smoothing = SmoothingAction.Clamp)] public short CurrentHitPoints;
    }

    public class HealthAuthoring : MonoBehaviour
    {
        public short MaximumHitPoints = 100;

        class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Health
                {
                    MaximumHitPoints = authoring.MaximumHitPoints,
                    CurrentHitPoints = authoring.MaximumHitPoints,
                });
            }
        }
    }
}

