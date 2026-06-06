using Unity.Assertions;
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics.Stateful
{
    public class StatefulTriggerEventBufferAuthoring : MonoBehaviour
    {
        class Baker : Baker<StatefulTriggerEventBufferAuthoring>
        {
            public override void Bake(StatefulTriggerEventBufferAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }

    // Trigger 可以存储在 DynamicBuffer 内的事件
    public struct StatefulTriggerEvent : IBufferElementData, IStatefulSimulationEvent<StatefulTriggerEvent>
    {
        public Entity EntityA { get; set; }
        public Entity EntityB { get; set; }
        public int BodyIndexA { get; set; }
        public int BodyIndexB { get; set; }
        public ColliderKey ColliderKeyA { get; set; }
        public ColliderKey ColliderKeyB { get; set; }
        public StatefulEventState State { get; set; }

        public StatefulTriggerEvent(TriggerEvent triggerEvent)
        {
            EntityA = triggerEvent.EntityA;
            EntityB = triggerEvent.EntityB;
            BodyIndexA = triggerEvent.BodyIndexA;
            BodyIndexB = triggerEvent.BodyIndexB;
            ColliderKeyA = triggerEvent.ColliderKeyA;
            ColliderKeyB = triggerEvent.ColliderKeyB;
            State = default;
        }

        // 返回 EntityPair 中的其他 entity（如果提供了一个）
        public Entity GetOtherEntity(Entity entity)
        {
            Assert.IsTrue((entity == EntityA) || (entity == EntityB));
            return (entity == EntityA) ? EntityB : EntityA;
        }

        public int CompareTo(StatefulTriggerEvent other) => ISimulationEventUtilities.CompareEvents(this, other);
    }

    // 如果将此 component 添加到 entity，则 trigger 事件不会添加到动态缓冲区
    // entity 的 StatefulTriggerEventBufferSystem。此 component 默认添加到
    // CharacterController entity，这样 CharacterControllerSystem 就可以将 trigger 事件添加到
    // CharacterController 独立运行，不受 StatefulTriggerEventBufferSystem 干扰。
    public struct StatefulTriggerEventExclude : IComponentData {}
}
