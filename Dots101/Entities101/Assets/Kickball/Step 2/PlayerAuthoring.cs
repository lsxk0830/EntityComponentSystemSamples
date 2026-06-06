using Unity.Entities;
using UnityEngine;

namespace Tutorials.Kickball.Step2
{
    // 与步骤 1 中的 ObstacleAuthoring.cs 相同的模式。
    public class PlayerAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<Player>(entity);

                // 在步骤 5 中使用
                AddComponent<Carry>(entity);
                SetComponentEnabled<Carry>(entity, false);
            }
        }
    }

    public struct Player : IComponentData
    {
    }

    // 在步骤 5 中使用
    public struct Carry : IComponentData, IEnableableComponent
    {
        // 在球上，这表示持球的球员；对于球员来说，这表示球正在被携带
        public Entity Target;
    }
}
