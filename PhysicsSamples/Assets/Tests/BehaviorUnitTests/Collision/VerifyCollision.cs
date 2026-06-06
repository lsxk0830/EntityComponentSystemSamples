using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Physics.Tests
{
    public struct VerifyCollisionData : IComponentData
    {
    }

    public class VerifyCollision : MonoBehaviour
    {
        class VerifyCollisionBaker : Baker<VerifyCollision>
        {
            public override void Bake(VerifyCollision authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<VerifyCollisionData>(entity);
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]
    public partial struct VerifyCollisionSystem : ISystem
    {
        EntityQuery m_VerificationGroup;

        public void OnCreate(ref SystemState state)
        {
            m_VerificationGroup = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(VerifyCollisionData) }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var entities = m_VerificationGroup.ToEntityArray(Allocator.TempJob);
            foreach (var entity in entities)
            {
                // 如果发生碰撞，“Y”component 永远不应低于 0
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.IsTrue(localTransform.Position.y > -0.001f);
            }
            entities.Dispose();
        }
    }
}
