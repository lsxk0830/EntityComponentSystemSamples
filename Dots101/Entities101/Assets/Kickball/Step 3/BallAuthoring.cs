using Tutorials.Kickball.Step2;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tutorials.Kickball.Step3
{
    public class BallAuthoring : MonoBehaviour
    {
        class Baker : Baker<BallAuthoring>
        {
            public override void Bake(BallAuthoring authoring)
            {
               var entity = GetEntity(TransformUsageFlags.Dynamic);

                // 单个 authoring component 可以将多个 components 添加到 entity。
                AddComponent<Ball>(entity);
                AddComponent<Velocity>(entity);

                // 在步骤 5 中使用
                AddComponent<Carry>(entity);
                SetComponentEnabled<Carry>(entity, false);
            }
        }
    }

    // 球 entities 的标签 component。
    public struct Ball : IComponentData
    {
    }

    // 球 entities 的 2d 速度矢量。
    public struct Velocity : IComponentData
    {
        public float2 Value;
    }
}
