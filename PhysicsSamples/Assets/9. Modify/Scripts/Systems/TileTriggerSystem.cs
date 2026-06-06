// 处理 trigger 事件并更新磁贴 entity 上的 TileTriggerCounter component 的 system
// 此 system 中不允许进行结构更改，因此触发器在 SpawnColliderFromTriggerSystem 中处理
// 大于 MaxTriggerCount 的触发器将被忽略。
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))] //事件有效 AFTER 已完成
public partial struct TileTriggerSystem : ISystem
{
    ComponentDataHandles m_Handles;
    private Entity m_TriggerEntity;

    struct ComponentDataHandles
    {
        public ComponentLookup<TileTriggerCounter> TileTriggerCounterGroup;

        public ComponentDataHandles(ref SystemState state)
        {
            TileTriggerCounterGroup = state.GetComponentLookup<TileTriggerCounter>(false);
        }

        public void Update(ref SystemState state)
        {
            TileTriggerCounterGroup.Update(ref state);
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadWrite<SimulationSingleton>()));
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadWrite<TileTriggerCounter>()));
        m_Handles = new ComponentDataHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state);

        state.Dependency = new TriggerColliderChangeJob()
        {
            TriggerColliderChangeGroup = m_Handles.TileTriggerCounterGroup,
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    // 这个 trigger 稍后用于在 SpawnColliderFromTriggerSystem 中生成 colliders（此处无法进行结构更改）
    [BurstCompile]
    struct TriggerColliderChangeJob : ITriggerEventsJob
    {
        public ComponentLookup<TileTriggerCounter> TriggerColliderChangeGroup;

        public void Execute(TriggerEvent triggerEvent)
        {
            // 获取 trigger 事件涉及的两个 entities
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            // 具有 TileTriggerCounter 的 entity 是 trigger 主体
            bool isBodyATrigger = TriggerColliderChangeGroup.HasComponent(entityA);
            bool isBodyBTrigger = TriggerColliderChangeGroup.HasComponent(entityB);

            // 忽略与其他触发器重叠的触发器
            if (isBodyATrigger && isBodyBTrigger)
                return;

            var triggerEntity = isBodyATrigger ? entityA : entityB; //瓷砖 entity

            var tileComponent = TriggerColliderChangeGroup[triggerEntity];
            if (tileComponent.TriggerCount < tileComponent.MaxTriggerCount) // 限制 trigger 可以被触发的次数
            {
                tileComponent.TriggerCount++;
                TriggerColliderChangeGroup[triggerEntity] = tileComponent;
            }
        }
    }
}
