using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct TriggerVolumeForceFieldSystem : ISystem
{
    private EntityQuery m_NonTriggerDynamicBodyQuery;
    private EntityQueryMask m_NonTriggerDynamicBodyMask;
    private ComponentDataHandles m_Handles;

    struct ComponentDataHandles
    {
        public ComponentLookup<LocalTransform> LocalTransformFromEntity;

        public ComponentLookup<PhysicsMass> MassFromEntity;
        public ComponentLookup<PhysicsVelocity> VelocityFromEntity;

        public ComponentDataHandles(ref SystemState state)
        {
            LocalTransformFromEntity = state.GetComponentLookup<LocalTransform>(true);

            MassFromEntity = state.GetComponentLookup<PhysicsMass>(true);
            VelocityFromEntity = state.GetComponentLookup<PhysicsVelocity>(false);
        }

        public void Update(ref SystemState state)
        {
            LocalTransformFromEntity.Update(ref state);

            MassFromEntity.Update(ref state);
            VelocityFromEntity.Update(ref state);
        }
    }

    public static void ApplyForceField(
        in float dt,
        ref PhysicsVelocity bodyVelocity,
        in LocalTransform localTransform, in PhysicsMass bodyMass, in TriggerVolumeForceField forceField
    )
    {
        if (forceField.Strength == 0)
            return;

        // 如果进入眼睛，请勿做任何事情
        float3 dir = float3.zero;

        dir = (forceField.Center - localTransform.Position);

        if (!math.any(dir))
            return;

        // 如果力场围绕轴，则将 dir 投影到轴上
        float3 axis = float3.zero;
        if (forceField.Axis != -1)
        {
            axis[forceField.Axis] = 1f;
            dir -= axis * math.dot(dir, axis);
        }

        float strength = forceField.Strength;
        float dist2 = math.lengthsq(dir);

        // 如果处于死区，就会失去力量
        float dz2 = forceField.DeadZone * forceField.DeadZone;
        if (dz2 > dist2)
            strength = 0;

        // 如果偏离中心并按比例除以距离平方
        if (forceField.Proportional != 0)
            strength = (dist2 > 1e-4f) ? strength / dist2 : 0;

        // 如果希望所有物体均匀移动，则多重通过质量
        dir = math.normalizesafe(dir);
        float mass = math.rcp(bodyMass.InverseMass);
        if (forceField.MassInvariant != 0) mass = 1f;
        strength *= mass * dt;
        bodyVelocity.Linear += strength * dir;

        // 如果想要旋转力场，请添加额外的扭曲增量
        if ((forceField.Axis != -1) && (forceField.Rotation != 0))
        {
            bodyVelocity.Linear += forceField.Rotation * strength * dir;
            dir = math.cross(axis, -dir);
            bodyVelocity.Linear += forceField.Rotation * strength * dir;
        }
    }

    [BurstCompile]
    partial struct ApplyForceFieldJob : IJobEntity
    {
        public EntityQueryMask NonTriggerDynamicBodyMask;
        public PhysicsStep StepComponent;
        public float DeltaTime;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        public ComponentLookup<PhysicsVelocity> Velocities;

        public void Execute(Entity e, ref DynamicBuffer<StatefulTriggerEvent> triggerEventBuffer, ref TriggerVolumeForceField forceField)
        {
            forceField.Center = LocalTransforms[e].Position;


            for (int i = 0; i < triggerEventBuffer.Length; i++)
            {
                var triggerEvent = triggerEventBuffer[i];

                var otherEntity = triggerEvent.GetOtherEntity(e);

                // 排除静态物体、其他触发器和进入/退出事件
                if (triggerEvent.State != StatefulEventState.Stay || !NonTriggerDynamicBodyMask.MatchesIgnoreFilter(otherEntity))
                {
                    continue;
                }

                var physicsVelocity = Velocities[otherEntity];
                var physicsMass = Masses[otherEntity];

                var pos = LocalTransforms[otherEntity];


                ApplyForceField(DeltaTime, ref physicsVelocity, pos, physicsMass, forceField);

                // 反作用重力
                physicsVelocity.Linear += -1.25f * StepComponent.Gravity * DeltaTime;

                // 回写
                Velocities[otherEntity] = physicsVelocity;
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new EntityQueryBuilder(Unity.Collections.Allocator.Temp)
            .WithAllRW<PhysicsVelocity>()

            .WithAll<LocalTransform, PhysicsMass>()

            .WithNone<StatefulTriggerEvent>();
        m_NonTriggerDynamicBodyQuery = state.GetEntityQuery(builder);

        Assert.IsFalse(m_NonTriggerDynamicBodyQuery.HasFilter(), "The use of EntityQueryMask in this system will not respect the query's active filter settings.");
        m_NonTriggerDynamicBodyMask = m_NonTriggerDynamicBodyQuery.GetEntityQueryMask();

        state.RequireForUpdate<TriggerVolumeForceField>();
        m_Handles = new ComponentDataHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state);

        var applyForceFieldJob = new ApplyForceFieldJob
        {
            NonTriggerDynamicBodyMask = m_NonTriggerDynamicBodyMask,

            StepComponent = SystemAPI.HasSingleton<PhysicsStep>() ? SystemAPI.GetSingleton<PhysicsStep>() : PhysicsStep.Default,
            DeltaTime = SystemAPI.Time.DeltaTime,

            Masses = m_Handles.MassFromEntity,
            Velocities = m_Handles.VelocityFromEntity
        };

        applyForceFieldJob.LocalTransforms = m_Handles.LocalTransformFromEntity;

        state.Dependency = applyForceFieldJob.Schedule(state.Dependency);
    }
}
