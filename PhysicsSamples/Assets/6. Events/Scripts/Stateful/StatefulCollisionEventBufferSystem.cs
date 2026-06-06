using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;

namespace Unity.Physics.Stateful
{
    // 此 system 将 CollisionEvents 流转换为可存储在动态缓冲区中的 StatefulCollisionEvents。
    // 为了进行此转换，需要：
    //    1) 使用 PhysicsShapeAuthoring component 上“碰撞响应”属性的“碰撞引发碰撞事件”选项，并且
    //    2) 将 StatefulCollisionEventBufferAuthoring component 添加到 entity （并选择是否应计算详细信息）
    // 或者，如果角色控制器需要这样做：
    //    1) 勾选 CharacterControllerAuthoring component 上的“引发冲突事件”标志。
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct StatefulCollisionEventBufferSystem : ISystem
    {
        private StatefulSimulationEventBuffers<StatefulCollisionEvent> m_StateFulEventBuffers;
        private ComponentHandles m_Handles;

        // Component 不执行任何操作。为了使用通用的 job 而制作。详细信息请参见 OnUpdate() 方法。
        internal struct DummyExcludeComponent : IComponentData {};

        struct ComponentHandles
        {
            public ComponentLookup<DummyExcludeComponent> EventExcludes;
            public ComponentLookup<StatefulCollisionEventDetails> EventDetails;
            public BufferLookup<StatefulCollisionEvent> EventBuffers;

            public ComponentHandles(ref SystemState systemState)
            {
                EventExcludes = systemState.GetComponentLookup<DummyExcludeComponent>(true);
                EventDetails = systemState.GetComponentLookup<StatefulCollisionEventDetails>(true);
                EventBuffers = systemState.GetBufferLookup<StatefulCollisionEvent>(false);
            }

            public void Update(ref SystemState systemState)
            {
                EventExcludes.Update(ref systemState);
                EventBuffers.Update(ref systemState);
                EventDetails.Update(ref systemState);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulCollisionEvent>();
            m_StateFulEventBuffers.AllocateBuffers();
            state.RequireForUpdate<StatefulCollisionEvent>();

            m_Handles = new ComponentHandles(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_StateFulEventBuffers.Dispose();
        }

        [BurstCompile]
        public partial struct ClearCollisionEventDynamicBufferJob : IJobEntity
        {
            public void Execute(ref DynamicBuffer<StatefulCollisionEvent> eventBuffer) => eventBuffer.Clear();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_Handles.Update(ref state);

            state.Dependency = new ClearCollisionEventDynamicBufferJob()
                .ScheduleParallel(state.Dependency);

            m_StateFulEventBuffers.SwapBuffers();

            var currentEvents = m_StateFulEventBuffers.Current;
            var previousEvents = m_StateFulEventBuffers.Previous;

            state.Dependency = new StatefulEventCollectionJobs.
                CollectCollisionEventsWithDetails
            {
                CollisionEvents = currentEvents,
                PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                EventDetails = m_Handles.EventDetails
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);


            state.Dependency = new StatefulEventCollectionJobs.
                ConvertEventStreamToDynamicBufferJob<StatefulCollisionEvent, DummyExcludeComponent>
            {
                CurrentEvents = currentEvents,
                PreviousEvents = previousEvents,
                EventLookup = m_Handles.EventBuffers,

                UseExcludeComponent = false,
                EventExcludeLookup = m_Handles.EventExcludes
            }.Schedule(state.Dependency);
        }
    }
}
