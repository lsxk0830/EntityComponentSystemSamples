// 此测试验证 CollisionEventData 结构以及碰撞事件的脉冲累积是否正确。
// 为了验证碰撞事件脉冲累积，应测试子步和求解器计数
// DynamicSphere 测试单个持续接触的脉冲累积（numContacts = 1）。
// DynamicBox 测试多次持续接触的脉冲累积 (numContacts > 1)。
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Physics.Tests
{
    public struct VerifyCollisionEventDataData : IComponentData
    {
        public int StabilizationTime;
    }

    [Serializable]
    public class VerifyCollisionEventData : MonoBehaviour
    {
        [Tooltip("Do not validate data until this number of time steps has passed")]
        public int StabilizationTime = 5;

        class VerifyCollisionEventDataBaker : Baker<VerifyCollisionEventData>
        {
            public override void Bake(VerifyCollisionEventData authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new VerifyCollisionEventDataData
                {
                    StabilizationTime = authoring.StabilizationTime
                });

#if HAVOK_PHYSICS_EXISTS
                Havok.Physics.HavokConfiguration config = Havok.Physics.HavokConfiguration.Default;
                config.EnableSleeping = 0;
                AddComponent(entity, config);
#endif
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(ExportPhysicsWorld))]
    public partial struct VerifyCollisionEventDataSystem : ISystem
    {
        private int Counter;
        private ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
        private ComponentLookup<PhysicsCollider> PhysicsColliderData;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VerifyCollisionEventDataData>();

            PhysicsVelocityData = state.GetComponentLookup<PhysicsVelocity>(true);
            PhysicsColliderData = state.GetComponentLookup<PhysicsCollider>(true);
            Counter = 0;
        }

        struct VerifyCollisionEventDataJob : ICollisionEventsJob
        {
            [ReadOnly] public PhysicsWorld World;
            [ReadOnly] public float3 Gravity;
            [ReadOnly] public bool StabilizationComplete;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
            [ReadOnly] public ComponentLookup<PhysicsCollider> PhysicsColliderData;

            public void Execute(CollisionEvent collisionEvent)
            {
                ColliderType type = ColliderType.Box; //选择随机默认值

                // 只有 EntityA 或 EntityB 之一具有动态实体。弄清楚它是什么以及 collider 的类型
                // 这样我们就可以确定预期的接触点数量。
                Entity entityA = collisionEvent.EntityA;
                bool isBodyADynamic = PhysicsVelocityData.HasComponent(entityA);
                if (isBodyADynamic)
                {
                    var collider = PhysicsColliderData[entityA];
                    type = collider.Value.Value.Type;
                }

                Entity entityB = collisionEvent.EntityB;
                bool isBodyBDynamic = PhysicsVelocityData.HasComponent(entityB);
                if (isBodyBDynamic)
                {
                    var collider = PhysicsColliderData[entityB];
                    type = collider.Value.Value.Type;
                }

                int expectedNumPoints = 1;
                switch (type)
                {
                    case ColliderType.Box:
                        expectedNumPoints = 4;
                        break;

                    case ColliderType.Sphere:
                        expectedNumPoints = 1;
                        break;
                }

                // 碰撞事件发生在静态和动态盒子之间。
                // 验证提供的事件结构中的所有数据。
                CollisionEvent.Details details = collisionEvent.CalculateDetails(ref World);
                Assert.IsTrue(details.EstimatedImpulse >= 0.0f);
                Assert.AreNotEqual(collisionEvent.BodyIndexA, collisionEvent.BodyIndexB);
                Assert.AreEqual(collisionEvent.ColliderKeyA.Value, ColliderKey.Empty.Value);
                Assert.AreEqual(collisionEvent.ColliderKeyB.Value, ColliderKey.Empty.Value);
                Assert.AreEqual(collisionEvent.EntityA, World.Bodies[collisionEvent.BodyIndexA].Entity);
                Assert.AreEqual(collisionEvent.EntityB, World.Bodies[collisionEvent.BodyIndexB].Entity);
                Assert.AreApproximatelyEqual(collisionEvent.Normal.x, 0.0f, 0.01f);
                Assert.AreApproximatelyEqual(collisionEvent.Normal.y, 1.0f, 0.01f);
                Assert.AreApproximatelyEqual(collisionEvent.Normal.z, 0.0f, 0.01f);

                // Collider 具体检查：
                Assert.IsTrue(details.EstimatedContactPointPositions.Length == expectedNumPoints);

                // 等到模拟稳定后再验证以下内容：
                if (StabilizationComplete)
                {
                    Assert.AreApproximatelyEqual(details.EstimatedImpulse, -Gravity.y, 0.002f, "Impulse should be equal to gravity");
                }
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            PhysicsVelocityData.Update(ref state);
            PhysicsColliderData.Update(ref state);

            if (!SystemAPI.TryGetSingleton<PhysicsStep>(out var physicsStep))
            {
                physicsStep = PhysicsStep.Default;
            }
            float timeStep = SystemAPI.Time.DeltaTime;
            var settings = SystemAPI.GetSingleton<VerifyCollisionEventDataData>();

            if (Counter < settings.StabilizationTime) Counter++;

            state.Dependency = new VerifyCollisionEventDataJob
            {
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                PhysicsVelocityData = PhysicsVelocityData,
                PhysicsColliderData = PhysicsColliderData,
                Gravity = physicsStep.Gravity * timeStep,
                StabilizationComplete = Counter >= settings.StabilizationTime
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }
    }
}
