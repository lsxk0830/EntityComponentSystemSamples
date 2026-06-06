using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Burst;

namespace Unity.Physics.Stateful
{
    // 此 system 将 TriggerEvents 流转换为可存储在动态缓冲区中的 StatefulTriggerEvents。
    // 为了进行此转换，需要：
    //    1) 在 PhysicsShapeAuthoring component 上使用“碰撞响应”属性的“引发 Trigger 事件”选项，并且
    //    2) 将 StatefulTriggerEventBufferAuthoring component 添加到该 entity
    // 或者，如果角色控制器需要这样做：
    //    1) 勾选 CharacterControllerAuthoring component 上的“引发 Trigger 事件”标志。
    //       Note: 角色控制器不会变成 trigger，它与一个控制器重叠时会引发事件
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct StatefulTriggerEventBufferSystem : ISystem
    {
        private StatefulSimulationEventBuffers<StatefulTriggerEvent> m_StateFulEventBuffers;
        private ComponentHandles m_ComponentHandles;
        private EntityQuery m_TriggerEventQuery;

        struct ComponentHandles
        {
            public ComponentLookup<StatefulTriggerEventExclude> EventExcludes;
            public BufferLookup<StatefulTriggerEvent> EventBuffers;

            public ComponentHandles(ref SystemState systemState)
            {
                EventExcludes = systemState.GetComponentLookup<StatefulTriggerEventExclude>(true);
                EventBuffers = systemState.GetBufferLookup<StatefulTriggerEvent>();
            }

            public void Update(ref SystemState systemState)
            {
                EventExcludes.Update(ref systemState);
                EventBuffers.Update(ref systemState);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<StatefulTriggerEvent>()
                .WithNone<StatefulTriggerEventExclude>();

            m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulTriggerEvent>();
            m_StateFulEventBuffers.AllocateBuffers();

            m_TriggerEventQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(m_TriggerEventQuery);

            m_ComponentHandles = new ComponentHandles(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_StateFulEventBuffers.Dispose();
        }

        [BurstCompile]
        public partial struct ClearTriggerEventDynamicBufferJob : IJobEntity
        {
            public void Execute(ref DynamicBuffer<StatefulTriggerEvent> eventBuffer) => eventBuffer.Clear();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_ComponentHandles.Update(ref state);

            state.Dependency = new ClearTriggerEventDynamicBufferJob()
                .ScheduleParallel(m_TriggerEventQuery, state.Dependency);

            m_StateFulEventBuffers.SwapBuffers();

            var currentEvents = m_StateFulEventBuffers.Current;
            var previousEvents = m_StateFulEventBuffers.Previous;

            state.Dependency = new StatefulEventCollectionJobs.CollectTriggerEvents
            {
                TriggerEvents = currentEvents
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            state.Dependency = new StatefulEventCollectionJobs
                .ConvertEventStreamToDynamicBufferJob<StatefulTriggerEvent, StatefulTriggerEventExclude>
            {
                CurrentEvents = currentEvents,
                PreviousEvents = previousEvents,
                EventLookup = m_ComponentHandles.EventBuffers,

                UseExcludeComponent = true,
                EventExcludeLookup = m_ComponentHandles.EventExcludes
            }.Schedule(state.Dependency);
        }
    }
}
