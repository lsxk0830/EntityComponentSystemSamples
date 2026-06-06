using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

// 该 system 对任何与排斥器碰撞的动态施加脉冲。
// 排斥器由 PhysicsShapeAuthoring 定义，其中 `Raise Collision Events` 标志被勾选，并且
// 添加了 CollisionEventImpulse 行为。
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct ContactEventImpulseSystem : ISystem
{
    internal ComponentDataHandles m_ComponentDataHandles;

    internal struct ComponentDataHandles
    {
        public ComponentLookup<ContactEventImpulse> ContactEventImpulseLookup;
        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;

        public ComponentDataHandles(ref SystemState systemState)
        {
            ContactEventImpulseLookup = systemState.GetComponentLookup<ContactEventImpulse>(true);
            PhysicsVelocityData = systemState.GetComponentLookup<PhysicsVelocity>(false);
        }

        public void Update(ref SystemState systemState)
        {
            ContactEventImpulseLookup.Update(ref systemState);
            PhysicsVelocityData.Update(ref systemState);
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<ContactEventImpulse>()));
        m_ComponentDataHandles = new ComponentDataHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_ComponentDataHandles.Update(ref state);
        state.Dependency = new CollisionEventImpulseJob
        {
            CollisionEventImpulseData = m_ComponentDataHandles.ContactEventImpulseLookup,
            PhysicsVelocityData = m_ComponentDataHandles.PhysicsVelocityData,
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }

    [BurstCompile]
    struct CollisionEventImpulseJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<ContactEventImpulse> CollisionEventImpulseData;
        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            bool isBodyADynamic = PhysicsVelocityData.HasComponent(entityA);
            bool isBodyBDynamic = PhysicsVelocityData.HasComponent(entityB);

            bool isBodyARepulser = CollisionEventImpulseData.HasComponent(entityA);
            bool isBodyBRepulser = CollisionEventImpulseData.HasComponent(entityB);

            if (isBodyARepulser && isBodyBDynamic)
            {
                var impulseComponent = CollisionEventImpulseData[entityA];
                var velocityComponent = PhysicsVelocityData[entityB];
                velocityComponent.Linear = impulseComponent.Impulse;
                PhysicsVelocityData[entityB] = velocityComponent;
            }

            if (isBodyBRepulser && isBodyADynamic)
            {
                var impulseComponent = CollisionEventImpulseData[entityB];
                var velocityComponent = PhysicsVelocityData[entityA];
                velocityComponent.Linear = impulseComponent.Impulse;
                PhysicsVelocityData[entityA] = velocityComponent;
            }
        }
    }
}
