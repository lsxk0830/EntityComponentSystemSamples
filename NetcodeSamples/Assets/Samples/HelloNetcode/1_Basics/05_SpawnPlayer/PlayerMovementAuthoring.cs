using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    [GhostComponent(PrefabType=GhostPrefabType.AllPredicted)]
    public struct PlayerMovement : IComponentData
    {
        // 该值连接到 prediction 循环内部计算的跳转逻辑
        // 因此需要是一个 ghost 字段，以便它正确存储在 ghost 历史记录中，并且
        // 每次 prediction 运行时，无论刻度是什么，您都会获得正确的值
        // 是 predicted
        [GhostField]
        public int JumpVelocity;
    }

    [DisallowMultipleComponent]
    public class PlayerMovementAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(PlayerMovement), "JumpVelocity")]
        public int JumpVelocity;

        class Baker : Baker<PlayerMovementAuthoring>
        {
            public override void Bake(PlayerMovementAuthoring authoring)
            {
                PlayerMovement component = default(PlayerMovement);
                component.JumpVelocity = authoring.JumpVelocity;
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}
