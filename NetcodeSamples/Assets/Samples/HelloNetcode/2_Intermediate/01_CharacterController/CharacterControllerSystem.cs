using Unity.Entities;
using Unity.Burst;
using Unity.Transforms;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Collections;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.Physics.Systems;

namespace Samples.HelloNetcode
{
    public struct CharacterControllerConfig : IComponentData
    {
        public float MoveSpeed;
        public float JumpSpeed;
        public float Gravity;
    }

    public struct Character : IComponentData
    {
        public Entity ControllerConfig;

        [GhostField(Quantization = 1000)]
        public float3 Velocity;
        [GhostField]
        public byte OnGround;
        [GhostField]
        public NetworkTick JumpStart;
    }

#pragma warning disable CS0618 // 禁用 Aspects 过时警告
    public readonly partial struct CharacterAspect : IAspect
    {
        public readonly Entity Self;
        public readonly RefRW<LocalTransform> Transform;

        readonly RefRO<AutoCommandTarget> m_AutoCommandTarget;
        readonly RefRW<Character> m_Character;
        readonly RefRW<PhysicsVelocity> m_Velocity;
        readonly RefRO<CharacterControllerPlayerInput> m_Input;
        readonly RefRO<GhostOwner> m_Owner;

        public AutoCommandTarget AutoCommandTarget => m_AutoCommandTarget.ValueRO;
        public CharacterControllerPlayerInput Input => m_Input.ValueRO;
        public int OwnerNetworkId => m_Owner.ValueRO.NetworkId;
        public ref Character Character => ref m_Character.ValueRW;
        public ref PhysicsVelocity Velocity => ref m_Velocity.ValueRW;
    }
#pragma warning restore CS0618

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    [BurstCompile]
    partial struct CharacterControllerSystem : ISystem
    {
        const float k_DefaultTau = 0.4f;
        const float k_DefaultDamping = 0.9f;
        const float k_DefaultSkinWidth = 0f;
        const float k_DefaultContactTolerance = 0.1f;
        const float k_DefaultMaxSlope = 60f;
        const float k_DefaultMaxMovementSpeed = 10f;
        const int k_DefaultMaxIterations = 10;
        const float k_DefaultMass = 1f;

        private ProfilerMarker m_MarkerGroundCheck;
        private ProfilerMarker m_MarkerStep;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<Character>();

            m_MarkerGroundCheck = new Unity.Profiling.ProfilerMarker("GroundCheck");
            m_MarkerStep = new Unity.Profiling.ProfilerMarker("Step");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            if (!HasPhysicsWorldBeenInitialized(physicsWorldSingleton))
            {
                return;
            }
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();

            var isMigratedLookup = SystemAPI.GetComponentLookup<IsMigrated>();

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var character in SystemAPI.Query<CharacterAspect>().WithAll<Simulate>())
            {
                if (!character.AutoCommandTarget.Enabled)
                {
                    character.Velocity.Linear = float3.zero;
                    continue;
                }

                if (isMigratedLookup.HasComponent(character.Self))
                {
                    var characterPrefabQuery = SystemAPI.QueryBuilder().WithAll<CharacterControllerConfig, Prefab>().Build();
                    if (characterPrefabQuery.CalculateEntityCount() == 1)
                    {
                        UnityEngine.Debug.Log($"Reconnecting character controller config entity was:{character.Character.ControllerConfig} is now:{characterPrefabQuery.GetSingletonEntity()}");
                        character.Character.ControllerConfig = characterPrefabQuery.GetSingletonEntity();
                        commandBuffer.RemoveComponent<IsMigrated>(character.Self);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Failed to reconnect character controller config entity was:{character.Character.ControllerConfig}");
                    }
                }

                var controllerConfig = SystemAPI.GetComponent<CharacterControllerConfig>(character.Character.ControllerConfig);
                var controllerCollider = SystemAPI.GetComponent<PhysicsCollider>(character.Character.ControllerConfig);

                // 字符步进输入
                CharacterControllerUtilities.CharacterControllerStepInput stepInput = new CharacterControllerUtilities.CharacterControllerStepInput
                {
                    PhysicsWorldSingleton = physicsWorldSingleton,
                    DeltaTime = SystemAPI.Time.DeltaTime,
                    Up = new float3(0, 1, 0),
                    Gravity = new float3(0, -controllerConfig.Gravity, 0),
                    MaxIterations = k_DefaultMaxIterations,
                    Tau = k_DefaultTau,
                    Damping = k_DefaultDamping,
                    SkinWidth = k_DefaultSkinWidth,
                    ContactTolerance = k_DefaultContactTolerance,
                    MaxSlope = math.radians(k_DefaultMaxSlope),
                    RigidBodyIndex = physicsWorldSingleton.PhysicsWorld.GetRigidBodyIndex(character.Self),
                    CurrentVelocity = character.Character.Velocity,
                    MaxMovementSpeed = k_DefaultMaxMovementSpeed
                };

                //在这里使用本地位置很好，因为角色控制器没有任何父级。
                //使用该职位是错误的，因为它不是最新的。（LocalTransform 已同步，但
                //world 变换不是）。
                RigidTransform ccTransform = new RigidTransform()
                {
                    pos = character.Transform.ValueRO.Position,
                    rot = quaternion.identity
                };

                m_MarkerGroundCheck.Begin();
                CharacterControllerUtilities.CheckSupport(
                    in physicsWorldSingleton,
                    ref controllerCollider,
                    stepInput,
                    ccTransform,
                    out CharacterControllerUtilities.CharacterSupportState supportState,
                    out _,
                    out _);
                m_MarkerGroundCheck.End();

                float2 input = character.Input.Movement;
                float3 wantedMove = new float3(input.x, 0, input.y) * controllerConfig.MoveSpeed * SystemAPI.Time.DeltaTime;

                var characterRotation = quaternion.RotateY(character.Input.Yaw);
                // 即使在空中，角色控制器的偏航旋转始终可以设置：
                character.Transform.ValueRW.Rotation = characterRotation;

                // 想要的运动是相对于相机的
                wantedMove = math.rotate(characterRotation, wantedMove);

                float3 wantedVelocity = wantedMove / SystemAPI.Time.DeltaTime;
                wantedVelocity.y = character.Character.Velocity.y;

                if (supportState == CharacterControllerUtilities.CharacterSupportState.Supported)
                {
                    character.Character.JumpStart = NetworkTick.Invalid;
                    character.Character.OnGround = 1;
                    character.Character.Velocity = wantedVelocity;
                    // 允许跳跃并在接地时停止坠落
                    if (character.Input.Jump.IsSet)
                    {
                        character.Character.Velocity.y = controllerConfig.JumpSpeed;
                        character.Character.JumpStart = networkTime.ServerTick;
                    }
                    else
                        character.Character.Velocity.y = 0;
                }
                else
                {
                    character.Character.OnGround = 0;
                    // 自由落体
                    character.Character.Velocity.y -= controllerConfig.Gravity * SystemAPI.Time.DeltaTime;
                }

                m_MarkerStep.Begin();
                // 好的，因为影响体是假的，所以没有写出脉冲
                NativeStream.Writer deferredImpulseWriter = default;
                CharacterControllerUtilities.CollideAndIntegrate(stepInput, k_DefaultMass, false, ref controllerCollider, ref ccTransform, ref character.Character.Velocity, ref deferredImpulseWriter);
                m_MarkerStep.End();

                // 设置物理速度并让物理根据该速度移动运动对象
                character.Velocity.Linear = (ccTransform.pos - character.Transform.ValueRO.Position) / SystemAPI.Time.DeltaTime;
            }
            commandBuffer.Playback(state.EntityManager);
        }

        /// <summary>
        /// 由于我们 run 在 <see cref="PhysicsInitializeGroup"/> 之前，因此可以在任何物理体之前执行
        /// 已初始化。
        ///
        /// 可能有更好的方法来做到这一点。
        /// </summary>
        static bool HasPhysicsWorldBeenInitialized(PhysicsWorldSingleton physicsWorldSingleton)
        {
            return physicsWorldSingleton.PhysicsWorld.Bodies is { IsCreated: true, Length: > 0 };
        }
    }
}
