using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// Component 添加到攻击者，复制 server 确认的射击结果（因此，每帧最多一次命中）。
    /// </summary>
    public struct ServerHitMarker : IComponentData
    {
        [GhostField] public Entity Victim;
        [GhostField] public float3 HitPoint;
        [GhostField] public NetworkTick ServerHitTick;
        public NetworkTick AppliedClientTick;
    }

    /// <summary>
    /// 与<see cref="ServerHitMarker"/>类似，但存储 client predicted 射击结果。
    /// 因此，不通过 <see cref="GhostFieldAttribute"/> 复制。
    /// </summary>
    public struct ClientHitMarker : IComponentData
    {
        public Entity Victim;
        public float3 HitPoint;
        public NetworkTick ClientHitTick;
        public NetworkTick AppliedClientTick;
    }

    public class HitMarkerAuthoring : MonoBehaviour
    {
        public class Baker : Baker<HitMarkerAuthoring>
        {
            public override void Bake(HitMarkerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ServerHitMarker>(entity);
                AddComponent<ClientHitMarker>(entity);
            }
        }
    }
}
