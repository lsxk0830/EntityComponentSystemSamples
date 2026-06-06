using Unity.Assertions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Stateful
{
    public class StatefulCollisionEventBufferAuthoring : MonoBehaviour
    {
        [Tooltip("If selected, the details will be calculated in collision event dynamic buffer of this entity")]
        public bool CalculateDetails = false;

        class StatefulCollisionEventBufferBaker : Baker<StatefulCollisionEventBufferAuthoring>
        {
            public override void Bake(StatefulCollisionEventBufferAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.CalculateDetails)
                {
                    var dynamicBufferTag = new StatefulCollisionEventDetails
                    {
                        CalculateDetails = authoring.CalculateDetails
                    };

                    AddComponent(entity, dynamicBufferTag);
                }

                AddBuffer<StatefulCollisionEvent>(entity);
            }
        }
    }

    public struct StatefulCollisionEventDetails : IComponentData
    {
        public bool CalculateDetails;
    }

    // 可以存储在 DynamicBuffer 内的碰撞事件
    public struct StatefulCollisionEvent : IBufferElementData, IStatefulSimulationEvent<StatefulCollisionEvent>
    {
        public Entity EntityA { get; set; }
        public Entity EntityB { get; set; }
        public int BodyIndexA { get; set; }
        public int BodyIndexB { get; set; }
        public ColliderKey ColliderKeyA { get; set; }
        public ColliderKey ColliderKeyB { get; set; }
        public StatefulEventState State { get; set; }
        public float3 Normal;

        // 仅当所选 entity 的 PhysicsCollisionEventBuffer 上选中 CalculateDetails 时，
        // 该字段将具有有效值，否则它将被初始化为零
        internal Details CollisionDetails;

        public StatefulCollisionEvent(CollisionEvent collisionEvent)
        {
            EntityA = collisionEvent.EntityA;
            EntityB = collisionEvent.EntityB;
            BodyIndexA = collisionEvent.BodyIndexA;
            BodyIndexB = collisionEvent.BodyIndexB;
            ColliderKeyA = collisionEvent.ColliderKeyA;
            ColliderKeyB = collisionEvent.ColliderKeyB;
            State = default;
            Normal = collisionEvent.Normal;
            CollisionDetails = default;
        }

        // 该结构描述了有关两个物体碰撞的附加、可选细节
        public struct Details
        {
            internal bool IsValid;

            // 如果为 1，则为顶点碰撞
            // 如果为 2，则为边缘碰撞
            // 如果 3 个或更多，则为面碰撞
            public int NumberOfContactPoints;

            // 估计施加的脉冲
            public float EstimatedImpulse;
            // 平均接触点位置
            public float3 AverageContactPointPosition;

            public Details(int numContactPoints, float estimatedImpulse, float3 averageContactPosition)
            {
                IsValid = (0 < numContactPoints); // 我们应该添加最大检查吗？
                NumberOfContactPoints = numContactPoints;
                EstimatedImpulse = estimatedImpulse;
                AverageContactPointPosition = averageContactPosition;
            }
        }

        // 返回 EntityPair 中的另一个 entity（如果提供了其他 entity）
        public Entity GetOtherEntity(Entity entity)
        {
            Assert.IsTrue((entity == EntityA) || (entity == EntityB));
            return entity == EntityA ? EntityB : EntityA;
        }

        // 返回从传递的 entity 到另一对中的正常指向
        public float3 GetNormalFrom(Entity entity)
        {
            Assert.IsTrue((entity == EntityA) || (entity == EntityB));
            return math.select(-Normal, Normal, entity == EntityB);
        }

        public bool TryGetDetails(out Details details)
        {
            details = CollisionDetails;
            return CollisionDetails.IsValid;
        }

        public int CompareTo(StatefulCollisionEvent other) => ISimulationEventUtilities.CompareEvents(this, other);
    }
}
