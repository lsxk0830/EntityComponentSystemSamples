using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;

public class TriggerVolumePortalAuthoring : MonoBehaviour
{
    public PhysicsBodyAuthoring CompanionPortal;

    class Baker : Baker<TriggerVolumePortalAuthoring>
    {
        public override void Bake(TriggerVolumePortalAuthoring authoring)
        {
            var companion = GetEntity(authoring.CompanionPortal, TransformUsageFlags.Dynamic);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TriggerVolumePortal
            {
                Companion = companion,
                TransferCount = 0
            });
        }
    }
}

public struct TriggerVolumePortal : IComponentData
{
    public Entity Companion;

    // 当 entity 传送到其同伴时，
    // 我们增加同伴的 TransferCount 以便
    // entity 不会立即传送
    // 回到原来的门户
    public int TransferCount;
}
