using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Conversion
{
    public class RotationAuthoring : MonoBehaviour
    {
        public Vector3 LocalAngularVelocity = Vector3.zero; // 以度/秒为单位

        class Baker : Baker<RotationAuthoring>
        {
            public override void Bake(RotationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Rotation
                {
                    // 我们可以在这里转换为弧度/秒。
                    LocalAngularVelocity = math.radians(authoring.LocalAngularVelocity),
                });
            }
        }
    }

    public struct Rotation : IComponentData
    {
        public float3 LocalAngularVelocity; // 以弧度/秒为单位
    }
}
