using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 存储来自 <see cref="ShootingSystem"/> 的原始最后命中数据，以处理为
    /// <see cref="ClientHitMarker"/> 或 <see cref="ServerHitMarker"/> 由 <see cref="ApplyHitMarkSystem"/>。
    /// </summary>
    public struct Hit : IComponentData
    {
        public Entity Victim;
        public NetworkTick Tick;
        public float3 HitPoint;
    }

    public class HitAuthoring : MonoBehaviour
    {
        class Baker : Baker<HitAuthoring>
        {
            public override void Bake(HitAuthoring authoring)
            {
                Hit component = default(Hit);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}

