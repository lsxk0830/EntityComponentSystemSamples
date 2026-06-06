using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using static CharacterControllerUtilities;
using static Unity.Physics.PhysicsStep;
using Math = Unity.Physics.Math;

[Serializable]
public struct CharacterControllerComponentData : IComponentData
{
    public float3 Gravity;
    public float MovementSpeed;
    public float MaxMovementSpeed;
    public float RotationSpeed;
    public float JumpUpwardsSpeed;
    public float MaxSlope; // 弧度
    public int MaxIterations;
    public float CharacterMass;
    public float SkinWidth;
    public float ContactTolerance;
    public byte AffectsPhysicsBodies;
    public byte RaiseCollisionEvents;
    public byte RaiseTriggerEvents;
}

public struct CharacterControllerInput : IComponentData
{
    public float2 Movement;
    public float2 Looking;
    public int Jumped;
}

[WriteGroup(typeof(PhysicsGraphicalInterpolationBuffer))]
[WriteGroup(typeof(PhysicsGraphicalSmoothing))]
public struct CharacterControllerInternalData : IComponentData
{
    public float CurrentRotationAngle;
    public CharacterSupportState SupportedState;
    public float3 UnsupportedVelocity;
    public PhysicsVelocity Velocity;
    public Entity Entity;
    public bool IsJumping;
    public CharacterControllerInput Input;
}

[Serializable]
public class CharacterControllerAuthoring : MonoBehaviour
{
    // 施加到角色控制器主体的重力
    public float3 Gravity = Default.Gravity;

    // 由用户输入启动的移动速度
    public float MovementSpeed = 2.5f;

    // 任何给定时间的最大移动速度
    public float MaxMovementSpeed = 10.0f;

    // 由用户输入启动的旋转速度
    public float RotationSpeed = 2.5f;

    // 用户输入启动的向上跳跃的速度
    public float JumpUpwardsSpeed = 5.0f;

    // 字符可以克服的最大倾斜角度（以度为单位）
    public float MaxSlope = 60.0f;

    // 角色控制器求解器迭代的最大次数
    public int MaxIterations = 10;

    // 角色的 Mass（用于影响其他刚体）
    public float CharacterMass = 1.0f;

    // 使角色与平面保持此距离（用于数值稳定性）
    public float SkinWidth = 0.02f;

    // 与角色的距离内的任何物体都将被视为潜在的接触
    // 检查支持时
    public float ContactTolerance = 0.1f;

    // 是否影响其他刚体
    public bool AffectsPhysicsBodies = true;

    // 是否引发碰撞事件
    // Note: 角色控制器引发的碰撞事件将始终计算详细信息
    public bool RaiseCollisionEvents = false;

    // 是否引发 trigger 事件
    public bool RaiseTriggerEvents = false;

    void OnEnable() {}
}

class CharacterControllerBaker : Baker<CharacterControllerAuthoring>
{
    public override void Bake(CharacterControllerAuthoring authoring)
    {
        if (authoring.enabled)
        {
            var componentData = new CharacterControllerComponentData
            {
                Gravity = authoring.Gravity,
                MovementSpeed = authoring.MovementSpeed,
                MaxMovementSpeed = authoring.MaxMovementSpeed,
                RotationSpeed = authoring.RotationSpeed,
                JumpUpwardsSpeed = authoring.JumpUpwardsSpeed,
                MaxSlope = math.radians(authoring.MaxSlope),
                MaxIterations = authoring.MaxIterations,
                CharacterMass = authoring.CharacterMass,
                SkinWidth = authoring.SkinWidth,
                ContactTolerance = authoring.ContactTolerance,
                AffectsPhysicsBodies = (byte)(authoring.AffectsPhysicsBodies ? 1 : 0),
                RaiseCollisionEvents = (byte)(authoring.RaiseCollisionEvents ? 1 : 0),
                RaiseTriggerEvents = (byte)(authoring.RaiseTriggerEvents ? 1 : 0)
            };
            var internalData = new CharacterControllerInternalData
            {
                Entity = GetEntity(TransformUsageFlags.Dynamic),
                Input = new CharacterControllerInput(),
            };

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, componentData);
            AddComponent(entity, internalData);
            if (authoring.RaiseCollisionEvents)
            {
                AddBuffer<StatefulCollisionEvent>(entity);
            }
            if (authoring.RaiseTriggerEvents)
            {
                AddBuffer<StatefulTriggerEvent>(entity);
                AddComponent(entity, new StatefulTriggerEventExclude());
            }
        }
    }
}

// 覆盖 BufferInterpolatedRigidBodiesMotion 的行为
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup)), UpdateBefore(typeof(ExportPhysicsWorld))]
[UpdateAfter(typeof(BufferInterpolatedRigidBodiesMotion))]
[RequireMatchingQueriesForUpdate]
public partial struct BufferInterpolatedCharacterControllerMotion : ISystem
{
    public partial struct UpdateCCInterpolationBuffersJobParallel : IJobEntity
    {
        public void Execute(ref PhysicsGraphicalInterpolationBuffer interpolationBuffer, in CharacterControllerInternalData ccInternalData, in LocalTransform localTransform)
        {
            interpolationBuffer = new PhysicsGraphicalInterpolationBuffer
            {
                PreviousTransform = new RigidTransform(localTransform.Rotation, localTransform.Position),

                PreviousVelocity = ccInternalData.Velocity,
            };
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new UpdateCCInterpolationBuffersJobParallel()
            .ScheduleParallel(state.Dependency);
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct CharacterControllerSystem : ISystem
{
    const float k_DefaultTau = 0.4f;
    const float k_DefaultDamping = 0.9f;

    private CharacterControllerTypeHandles m_Handles;

    struct CharacterControllerTypeHandles
    {
        public ComponentTypeHandle<CharacterControllerInternalData> CharacterControllerInternalType;

        public ComponentTypeHandle<LocalTransform> LocalTransformType;

        public BufferTypeHandle<StatefulCollisionEvent> CollisionEventBufferType;
        public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;
        public ComponentTypeHandle<CharacterControllerComponentData> CharacterControllerComponentType;
        public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
        public ComponentTypeHandle<PhysicsGraphicalSmoothing> PhysicsGraphicalSmoothingType;

        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
        public ComponentLookup<PhysicsMass> PhysicsMassData;

        public ComponentLookup<LocalTransform> LocalTransformData;


        public CharacterControllerTypeHandles(ref SystemState state)
        {
            CharacterControllerInternalType = state.GetComponentTypeHandle<CharacterControllerInternalData>();

            LocalTransformType = state.GetComponentTypeHandle<LocalTransform>();

            CollisionEventBufferType = state.GetBufferTypeHandle<StatefulCollisionEvent>();
            TriggerEventBufferType = state.GetBufferTypeHandle<StatefulTriggerEvent>();
            PhysicsGraphicalSmoothingType = state.GetComponentTypeHandle<PhysicsGraphicalSmoothing>();
            CharacterControllerComponentType = state.GetComponentTypeHandle<CharacterControllerComponentData>(true);
            PhysicsColliderType = state.GetComponentTypeHandle<PhysicsCollider>(true);

            PhysicsVelocityData = state.GetComponentLookup<PhysicsVelocity>();
            PhysicsMassData = state.GetComponentLookup<PhysicsMass>(true);

            LocalTransformData = state.GetComponentLookup<LocalTransform>(true);
        }

        public void Update(ref SystemState state)
        {
            CharacterControllerInternalType.Update(ref state);

            LocalTransformType.Update(ref state);

            CollisionEventBufferType.Update(ref state);
            TriggerEventBufferType.Update(ref state);
            PhysicsGraphicalSmoothingType.Update(ref state);
            CharacterControllerComponentType.Update(ref state);
            PhysicsColliderType.Update(ref state);

            PhysicsVelocityData.Update(ref state);
            PhysicsMassData.Update(ref state);

            LocalTransformData.Update(ref state);
        }
    }

    [BurstCompile]
    struct CharacterControllerJob : IJobChunk
    {
        public float DeltaTime;

        [ReadOnly] public PhysicsWorldSingleton PhysicsWorldSingleton;
        public ComponentTypeHandle<CharacterControllerInternalData> CharacterControllerInternalType;

        public ComponentTypeHandle<LocalTransform> LocalTransformType;

        public BufferTypeHandle<StatefulCollisionEvent> CollisionEventBufferType;
        public BufferTypeHandle<StatefulTriggerEvent> TriggerEventBufferType;
        [ReadOnly] public ComponentTypeHandle<CharacterControllerComponentData> CharacterControllerComponentType;
        [ReadOnly] public ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;

        // 存储我们希望应用于与​​角色交互的动态物体的脉冲。
        // 当 2 个角色与角色交互时，这是为了避免竞争条件所必需的
        // 同一个身体在同一时间。
        [NativeDisableParallelForRestriction] public NativeStream.Writer DeferredImpulseWriter;

        public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            var chunkCCData = chunk.GetNativeArray(ref CharacterControllerComponentType);
            var chunkCCInternalData = chunk.GetNativeArray(ref CharacterControllerInternalType);
            var chunkPhysicsColliderData = chunk.GetNativeArray(ref PhysicsColliderType);

            var chunkLocalTransformData = chunk.GetNativeArray(ref LocalTransformType);


            var hasChunkCollisionEventBufferType = chunk.Has(ref CollisionEventBufferType);
            var hasChunkTriggerEventBufferType = chunk.Has(ref TriggerEventBufferType);

            BufferAccessor<StatefulCollisionEvent> collisionEventBuffers = default;
            BufferAccessor<StatefulTriggerEvent> triggerEventBuffers = default;
            if (hasChunkCollisionEventBufferType)
            {
                collisionEventBuffers = chunk.GetBufferAccessor(ref CollisionEventBufferType);
            }
            if (hasChunkTriggerEventBufferType)
            {
                triggerEventBuffers = chunk.GetBufferAccessor(ref TriggerEventBufferType);
            }

            DeferredImpulseWriter.BeginForEachIndex(unfilteredChunkIndex);

            for (int i = 0; i < chunk.Count; i++)
            {
                var ccComponentData = chunkCCData[i];
                var ccInternalData = chunkCCInternalData[i];
                var collider = chunkPhysicsColliderData[i];

                var localTransform = chunkLocalTransformData[i];

                DynamicBuffer<StatefulCollisionEvent> collisionEventBuffer = default;
                DynamicBuffer<StatefulTriggerEvent> triggerEventBuffer = default;

                if (hasChunkCollisionEventBufferType)
                {
                    collisionEventBuffer = collisionEventBuffers[i];
                }

                if (hasChunkTriggerEventBufferType)
                {
                    triggerEventBuffer = triggerEventBuffers[i];
                }

                // 碰撞过滤器必须有效
                if (!collider.IsValid || collider.Value.Value.GetCollisionFilter().IsEmpty)
                    continue;

                var up = math.select(math.up(), -math.normalize(ccComponentData.Gravity),
                    math.lengthsq(ccComponentData.Gravity) > 0f);

                // 字符步进输入
                CharacterControllerStepInput stepInput = new CharacterControllerStepInput
                {
                    PhysicsWorldSingleton = PhysicsWorldSingleton,
                    DeltaTime = DeltaTime,
                    Up = up,
                    Gravity = ccComponentData.Gravity,
                    MaxIterations = ccComponentData.MaxIterations,
                    Tau = k_DefaultTau,
                    Damping = k_DefaultDamping,
                    SkinWidth = ccComponentData.SkinWidth,
                    ContactTolerance = ccComponentData.ContactTolerance,
                    MaxSlope = ccComponentData.MaxSlope,
                    RigidBodyIndex = PhysicsWorldSingleton.PhysicsWorld.GetRigidBodyIndex(ccInternalData.Entity),
                    CurrentVelocity = ccInternalData.Velocity.Linear,
                    MaxMovementSpeed = ccComponentData.MaxMovementSpeed
                };

                // 字符变换
                RigidTransform transform = new RigidTransform
                {
                    pos = localTransform.Position,
                    rot = localTransform.Rotation
                };

                NativeList<StatefulCollisionEvent> currentFrameCollisionEvents = default;
                NativeList<StatefulTriggerEvent> currentFrameTriggerEvents = default;

                if (ccComponentData.RaiseCollisionEvents != 0)
                {
                    currentFrameCollisionEvents = new NativeList<StatefulCollisionEvent>(Allocator.Temp);
                }

                if (ccComponentData.RaiseTriggerEvents != 0)
                {
                    currentFrameTriggerEvents = new NativeList<StatefulTriggerEvent>(Allocator.Temp);
                }

                // 检查支持
                CheckSupport(in PhysicsWorldSingleton, ref collider, stepInput, transform,
                    out ccInternalData.SupportedState, out float3 surfaceNormal, out float3 surfaceVelocity,
                    currentFrameCollisionEvents);

                // 用户输入
                float3 desiredVelocity = ccInternalData.Velocity.Linear;
                HandleUserInput(ccComponentData, stepInput.Up, surfaceVelocity, ref ccInternalData, ref desiredVelocity);

                // 计算相对于表面的实际速度
                if (ccInternalData.SupportedState == CharacterSupportState.Supported)
                {
                    CalculateMovement(ccInternalData.CurrentRotationAngle, stepInput.Up, ccInternalData.IsJumping,
                        ccInternalData.Velocity.Linear, desiredVelocity, surfaceNormal, surfaceVelocity, out ccInternalData.Velocity.Linear);
                }
                else
                {
                    ccInternalData.Velocity.Linear = desiredVelocity;
                }

                // World 碰撞+整合
                CollideAndIntegrate(stepInput, ccComponentData.CharacterMass, ccComponentData.AffectsPhysicsBodies != 0,
                    ref collider, ref transform, ref ccInternalData.Velocity.Linear, ref DeferredImpulseWriter,
                    currentFrameCollisionEvents, currentFrameTriggerEvents);

                // 更新碰撞事件状态
                if (currentFrameCollisionEvents.IsCreated)
                {
                    UpdateCollisionEvents(currentFrameCollisionEvents, collisionEventBuffer);
                }

                if (currentFrameTriggerEvents.IsCreated)
                {
                    UpdateTriggerEvents(currentFrameTriggerEvents, triggerEventBuffer);
                }

                // 回写和定向集成

                localTransform.Position = transform.pos;
                localTransform.Rotation = quaternion.AxisAngle(up, ccInternalData.CurrentRotationAngle);


                // 写回 chunk 数据
                {
                    chunkCCInternalData[i] = ccInternalData;

                    chunkLocalTransformData[i] = localTransform;
                }
            }

            DeferredImpulseWriter.EndForEachIndex();
        }

        private void HandleUserInput(CharacterControllerComponentData ccComponentData, float3 up, float3 surfaceVelocity,
            ref CharacterControllerInternalData ccInternalData, ref float3 linearVelocity)
        {
            // 重置跳跃状态和不受支持的速度
            if (ccInternalData.SupportedState == CharacterSupportState.Supported)
            {
                ccInternalData.IsJumping = false;
                ccInternalData.UnsupportedVelocity = float3.zero;
            }

            // 移动和跳跃
            bool shouldJump = false;
            float3 requestedMovementDirection = float3.zero;
            {
                float3 forward = math.forward(quaternion.identity);
                float3 right = math.cross(up, forward);

                float horizontal = ccInternalData.Input.Movement.x;
                float vertical = ccInternalData.Input.Movement.y;
                bool jumpRequested = ccInternalData.Input.Jumped != 0;
                ccInternalData.Input.Jumped = 0; // “消费”事件
                bool haveInput = (math.abs(horizontal) > float.Epsilon) || (math.abs(vertical) > float.Epsilon);
                if (haveInput)
                {
                    float3 localSpaceMovement = forward * vertical + right * horizontal;
                    float3 worldSpaceMovement = math.rotate(quaternion.AxisAngle(up, ccInternalData.CurrentRotationAngle), localSpaceMovement);
                    requestedMovementDirection = math.normalize(worldSpaceMovement);
                }
                shouldJump = jumpRequested && ccInternalData.SupportedState == CharacterSupportState.Supported;
            }

            // 车削
            {
                float horizontal = ccInternalData.Input.Looking.x;
                bool haveInput = (math.abs(horizontal) > float.Epsilon);
                if (haveInput)
                {
                    var userRotationSpeed = horizontal * ccComponentData.RotationSpeed;
                    ccInternalData.Velocity.Angular = -userRotationSpeed * up;
                    ccInternalData.CurrentRotationAngle += userRotationSpeed * DeltaTime;
                }
                else
                {
                    ccInternalData.Velocity.Angular = 0f;
                }
            }

            // 应用输入速度
            {
                if (shouldJump)
                {
                    // 将跳跃速度添加到表面速度并使角色不受支撑
                    ccInternalData.IsJumping = true;
                    ccInternalData.SupportedState = CharacterSupportState.Unsupported;
                    ccInternalData.UnsupportedVelocity = surfaceVelocity + ccComponentData.JumpUpwardsSpeed * up;
                }
                else if (ccInternalData.SupportedState != CharacterSupportState.Supported)
                {
                    // 施加重力
                    ccInternalData.UnsupportedVelocity += ccComponentData.Gravity * DeltaTime;
                }
                // 如果没有支撑，则保持跳跃和表面动量
                linearVelocity = requestedMovementDirection * ccComponentData.MovementSpeed +
                    (ccInternalData.SupportedState != CharacterSupportState.Supported ? ccInternalData.UnsupportedVelocity : float3.zero);
            }
        }

        private void CalculateMovement(float currentRotationAngle, float3 up, bool isJumping,
            float3 currentVelocity, float3 desiredVelocity, float3 surfaceNormal, float3 surfaceVelocity, out float3 linearVelocity)
        {
            float3 forward = math.forward(quaternion.AxisAngle(up, currentRotationAngle));

            quaternion surfaceFrame;

            float3 binorm;
            {
                binorm = math.cross(forward, up);
                binorm = math.normalize(binorm);

                float3 tangent = math.cross(binorm, surfaceNormal);
                tangent = math.normalize(tangent);

                binorm = math.cross(tangent, surfaceNormal);
                binorm = math.normalize(binorm);

                surfaceFrame = new quaternion(new float3x3(binorm, tangent, surfaceNormal));
            }

            float3 relative = currentVelocity - surfaceVelocity;

            relative = math.rotate(math.inverse(surfaceFrame), relative);

            float3 diff;
            {
                float3 sideVec = math.cross(forward, up);
                float fwd = math.dot(desiredVelocity, forward);
                float side = math.dot(desiredVelocity, sideVec);
                float len = math.length(desiredVelocity);
                float3 desiredVelocitySF = new float3(-side, -fwd, 0.0f);
                desiredVelocitySF = math.normalizesafe(desiredVelocitySF, float3.zero);
                desiredVelocitySF *= len;
                diff = desiredVelocitySF - relative;
            }

            relative += diff;

            linearVelocity = math.rotate(surfaceFrame, relative) + surfaceVelocity +
                (isJumping ? math.dot(desiredVelocity, up) * up : float3.zero);
        }

        private void UpdateTriggerEvents(NativeList<StatefulTriggerEvent> triggerEvents,
            DynamicBuffer<StatefulTriggerEvent> triggerEventBuffer)
        {
            var previousFrameTriggerEvents = new NativeList<StatefulTriggerEvent>(triggerEventBuffer.Length, Allocator.Temp);

            for (int i = 0; i < triggerEventBuffer.Length; i++)
            {
                var triggerEvent = triggerEventBuffer[i];
                if (triggerEvent.State != StatefulEventState.Exit)
                {
                    previousFrameTriggerEvents.Add(triggerEvent);
                }
            }

            var eventsWithState = new NativeList<StatefulTriggerEvent>(triggerEvents.Length, Allocator.Temp);

            StatefulSimulationEventBuffers<StatefulTriggerEvent>.GetStatefulEvents(previousFrameTriggerEvents, triggerEvents, eventsWithState);

            triggerEventBuffer.Clear();

            for (int i = 0; i < eventsWithState.Length; i++)
            {
                triggerEventBuffer.Add(eventsWithState[i]);
            }
        }

        private void UpdateCollisionEvents(NativeList<StatefulCollisionEvent> collisionEvents,
            DynamicBuffer<StatefulCollisionEvent> collisionEventBuffer)
        {
            var previousFrameCollisionEvents = new NativeList<StatefulCollisionEvent>(collisionEventBuffer.Length, Allocator.Temp);

            for (int i = 0; i < collisionEventBuffer.Length; i++)
            {
                var collisionEvent = collisionEventBuffer[i];
                if (collisionEvent.State != StatefulEventState.Exit)
                {
                    previousFrameCollisionEvents.Add(collisionEvent);
                }
            }

            var eventsWithState = new NativeList<StatefulCollisionEvent>(collisionEvents.Length, Allocator.Temp);
            StatefulSimulationEventBuffers<StatefulCollisionEvent>.GetStatefulEvents(previousFrameCollisionEvents, collisionEvents, eventsWithState);

            collisionEventBuffer.Clear();
            for (int i = 0; i < eventsWithState.Length; i++)
            {
                collisionEventBuffer.Add(eventsWithState[i]);
            }
        }
    }

    [BurstCompile]
    struct ApplyDefferedPhysicsUpdatesJob : IJob
    {
        // 此时可以释放 Chunks
        [DeallocateOnJobCompletion] public NativeArray<ArchetypeChunk> Chunks;

        public NativeStream.Reader DeferredImpulseReader;

        public ComponentLookup<PhysicsVelocity> PhysicsVelocityData;
        [ReadOnly] public ComponentLookup<PhysicsMass> PhysicsMassData;

        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformData;

        public void Execute()
        {
            int index = 0;
            int maxIndex = DeferredImpulseReader.ForEachCount;
            DeferredImpulseReader.BeginForEachIndex(index++);
            while (DeferredImpulseReader.RemainingItemCount == 0 && index < maxIndex)
            {
                DeferredImpulseReader.BeginForEachIndex(index++);
            }

            while (DeferredImpulseReader.RemainingItemCount > 0)
            {
                // 读取数据
                var impulse = DeferredImpulseReader.Read<DeferredCharacterControllerImpulse>();
                while (DeferredImpulseReader.RemainingItemCount == 0 && index < maxIndex)
                {
                    DeferredImpulseReader.BeginForEachIndex(index++);
                }

                PhysicsVelocity pv = PhysicsVelocityData[impulse.Entity];
                PhysicsMass pm = PhysicsMassData[impulse.Entity];

                LocalTransform t = LocalTransformData[impulse.Entity];


                // 不适用于运动体
                if (pm.InverseMass > 0.0f)
                {
                    // 施加脉冲

                    pv.ApplyImpulse(pm, t.Position, t.Rotation, impulse.Impulse, impulse.Point);

                    // 回信
                    PhysicsVelocityData[impulse.Entity] = pv;
                }
            }
        }
    }

    // 覆盖 CopyPhysicsVelocityToSmoothing 的行为
    [BurstCompile]
    partial struct CopyVelocityToGraphicalSmoothingJob : IJobEntity
    {
        public void Execute(in CharacterControllerInternalData ccInternalData, ref PhysicsGraphicalSmoothing smoothing)
        {
            smoothing.CurrentVelocity = ccInternalData.Velocity;
        }
    }

    EntityQuery m_CharacterControllersGroup;
    EntityQuery m_SmoothedCharacterControllersGroup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<CharacterControllerComponentData, CharacterControllerInternalData>()

            .WithAllRW<LocalTransform>()

            .WithAll<PhysicsCollider>();

        m_CharacterControllersGroup = state.GetEntityQuery(queryBuilder);
        state.RequireForUpdate(m_CharacterControllersGroup);

        queryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<CharacterControllerInternalData, PhysicsGraphicalSmoothing>();

        m_SmoothedCharacterControllersGroup = state.GetEntityQuery(queryBuilder);

        m_Handles = new CharacterControllerTypeHandles(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_Handles.Update(ref state);

        var chunks = m_CharacterControllersGroup.ToArchetypeChunkArray(Allocator.TempJob);
        var deferredImpulses = new NativeStream(chunks.Length, Allocator.TempJob);
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        float dt = SystemAPI.Time.DeltaTime;
        var ccJob = new CharacterControllerJob
        {
            // Archetypes
            CharacterControllerComponentType = m_Handles.CharacterControllerComponentType,
            CharacterControllerInternalType = m_Handles.CharacterControllerInternalType,
            PhysicsColliderType = m_Handles.PhysicsColliderType,

            LocalTransformType = m_Handles.LocalTransformType,

            CollisionEventBufferType = m_Handles.CollisionEventBufferType,
            TriggerEventBufferType = m_Handles.TriggerEventBufferType,

            // 输入
            DeltaTime = dt,
            PhysicsWorldSingleton = physicsWorldSingleton,
            DeferredImpulseWriter = deferredImpulses.AsWriter()
        };

        state.Dependency = ccJob.ScheduleParallel(m_CharacterControllersGroup, state.Dependency);

        var copyVelocitiesHandle = new CopyVelocityToGraphicalSmoothingJob().ScheduleParallel(m_SmoothedCharacterControllersGroup, state.Dependency);

        var applyJob = new ApplyDefferedPhysicsUpdatesJob()
        {
            Chunks = chunks,
            DeferredImpulseReader = deferredImpulses.AsReader(),
            PhysicsVelocityData = m_Handles.PhysicsVelocityData,
            PhysicsMassData = m_Handles.PhysicsMassData,

            LocalTransformData = m_Handles.LocalTransformData,
        };

        state.Dependency = applyJob.Schedule(state.Dependency);

        state.Dependency = deferredImpulses.Dispose(state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(state.Dependency, copyVelocitiesHandle);
    }
}
