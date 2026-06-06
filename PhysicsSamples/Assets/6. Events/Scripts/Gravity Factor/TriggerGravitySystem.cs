using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

// 此 system 设置进入 Trigger 卷的任何动态主体的 PhysicsGravityFactor。
// Trigger 卷由 PhysicsShapeAuthoring 定义，其中勾选了 `Is Trigger` 标志和
// 添加了 TriggerGravityFactor 行为。
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct TriggerGravitySystem : ISystem
{
    ComponentDataHandles m_Handles;

    struct ComponentDataHandles
    {
        public ComponentLookup<TriggerGravityFactor> TriggerGravityFactorGroup;
        public ComponentLookup<PhysicsGravityFactor> PhysicsGravityFactorGroup;
        public ComponentLookup<PhysicsVelocity> PhysicsVelocityGroup;

        public ComponentDataHandles(ref SystemState state)
        {
            TriggerGravityFactorGroup = state.GetComponentLookup<TriggerGravityFactor>(true);
            PhysicsGravityFactorGroup = state.GetComponentLookup<PhysicsGravityFactor>(false);
            PhysicsVelocityGroup = state.GetComponentLookup<PhysicsVelocity>(false);
        }

        public void Update(ref SystemState state)
        {
            TriggerGravityFactorGroup.Update(ref state);
            PhysicsGravityFactorGroup.Update(ref state);
            PhysicsVelocityGroup.Update(ref state);
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<TriggerGravityFactor>()));
        m_Handles = new ComponentDataHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state);
        state.Dependency = new TriggerGravityFactorJob
        {
            TriggerGravityFactorGroup = m_Handles.TriggerGravityFactorGroup,
            PhysicsGravityFactorGroup = m_Handles.PhysicsGravityFactorGroup,
            PhysicsVelocityGroup = m_Handles.PhysicsVelocityGroup,
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }

    [BurstCompile]
    struct TriggerGravityFactorJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<TriggerGravityFactor> TriggerGravityFactorGroup;
        public ComponentLookup<PhysicsGravityFactor> PhysicsGravityFactorGroup;
        public ComponentLookup<PhysicsVelocity> PhysicsVelocityGroup;

        public void Execute(TriggerEvent triggerEvent)
        {
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            bool isBodyATrigger = TriggerGravityFactorGroup.HasComponent(entityA);
            bool isBodyBTrigger = TriggerGravityFactorGroup.HasComponent(entityB);

            // 忽略与其他触发器重叠的触发器
            if (isBodyATrigger && isBodyBTrigger)
                return;

            bool isBodyADynamic = PhysicsVelocityGroup.HasComponent(entityA);
            bool isBodyBDynamic = PhysicsVelocityGroup.HasComponent(entityB);

            // 忽略重叠的静态物体
            if ((isBodyATrigger && !isBodyBDynamic) ||
                (isBodyBTrigger && !isBodyADynamic))
                return;

            var triggerEntity = isBodyATrigger ? entityA : entityB;
            var dynamicEntity = isBodyATrigger ? entityB : entityA;

            var triggerGravityComponent = TriggerGravityFactorGroup[triggerEntity];
            // 调整 PhysicsGravityFactor
            {
                var component = PhysicsGravityFactorGroup[dynamicEntity];
                component.Value = triggerGravityComponent.GravityFactor;
                PhysicsGravityFactorGroup[dynamicEntity] = component;
            }
            // 阻尼速度
            {
                var component = PhysicsVelocityGroup[dynamicEntity];
                component.Linear *= triggerGravityComponent.DampingFactor;
                PhysicsVelocityGroup[dynamicEntity] = component;
            }
        }
    }
}
