using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace HelloCube
{
    // authoring component 只是普通的 MonoBehavior。
    public class RotationSpeedAuthoring : MonoBehaviour
    {
        public float DegreesPerSecond = 360.0f;

        // 在 baking 中，此 Baker 将为 entity subscene 中的每个 RotationSpeedAuthoring 实例 run 一次。
        // （嵌套 authoring component 的 Baker 类只是一个可选的样式问题。）
        class Baker : Baker<RotationSpeedAuthoring>
        {
            public override void Bake(RotationSpeedAuthoring authoring)
            {
                // entity 将被移动
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RotationSpeed { RadiansPerSecond = math.radians(authoring.DegreesPerSecond) });
            }
        }
    }

    public struct RotationSpeed : IComponentData
    {
        public float RadiansPerSecond;
    }
}
