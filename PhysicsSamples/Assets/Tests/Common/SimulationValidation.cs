using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Physics.Tests
{
    public class SimulationValidationAuthoring : MonoBehaviour
    {
        [Header("General Settings")]

        [Tooltip("Enables simulation validation.")]
        public bool EnableValidation = false;
        [Tooltip("Time period during which any validation is performed as simulation time interval [start, end] in seconds. Specify -1 as end value for a validation that never ends (default).")]
        public float2 ValidationTimeRange = new(0, -1);

        [Header("Validation Types")]

        [Tooltip("Validates if joints behave as expected, by comparing relative body positions and orientations and their relative angular and linear velocities.")]
        public bool ValidateJointBehavior = false;
        [Tooltip("Validates that all rigid bodies are at rest and don't exceed the provided linear and angular velocity error tolerances.")]
        public bool ValidateRigidBodiesAtRest = false;

        [Header("Tolerances")]

        [Tooltip("Linear velocity error tolerance in meters/s")]
        public float LinearVelocityErrorTolerance = 0.005f;
        [Tooltip("Angular velocity error tolerance in radians/s")]
        public float AngularVelocityErrorTolerance = 0.01f;
        [Tooltip("Position error tolerance in meters")]
        public float PositionErrorTolerance = 0.01f;
        [Tooltip("Orientation error tolerance in radians")]
        public float OrientationErrorTolerance = 0.01f;
    }

    public class SimulationValidationBaker : Baker<SimulationValidationAuthoring>
    {
        public override void Bake(SimulationValidationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SimulationValidationSettings()
            {
                EnableValidation = authoring.EnableValidation,
                ValidateJointBehavior = authoring.ValidateJointBehavior,
                ValidateRigidBodiesAtRest = authoring.ValidateRigidBodiesAtRest,
                LinearVelocityErrorTolerance = authoring.LinearVelocityErrorTolerance,
                AngularVelocityErrorTolerance = authoring.AngularVelocityErrorTolerance,
                PositionErrorTolerance = authoring.PositionErrorTolerance,
                OrientationErrorTolerance = authoring.OrientationErrorTolerance,
                ValidationTimeRange = authoring.ValidationTimeRange
            });
        }
    }
    public struct SimulationValidationSettings : IComponentData
    {
        public bool EnableValidation;
        public bool ValidateJointBehavior;
        public bool ValidateRigidBodiesAtRest;
        public float LinearVelocityErrorTolerance;
        public float AngularVelocityErrorTolerance;
        public float PositionErrorTolerance;
        public float OrientationErrorTolerance;
        public float2 ValidationTimeRange;
    }

    /// <summary>
    /// 验证模拟中的所有 PhysicsJoint 对象。
    ///
    /// 预期的行为对应于使用创建的关节
    /// PhysicsJoint、e.g.、CreatePrismatic、CreateHinge 等中的 joint 创建函数
    /// </summary>
    [BurstCompile]
    public partial struct ValidateJointBehaviorJob : IJobEntity
    {
        [NativeDisableUnsafePtrRestriction]
        public SimulationValidationSystem.ErrorCounter Errors;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

        [ReadOnly] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
        [ReadOnly] public ComponentLookup<PhysicsMass> PhysicsMassLookup;

        [ReadOnly] public DynamicsWorld DynamicsWorld;
        [ReadOnly] public NativeArray<Joint> Joints;

        [ReadOnly] public float PositionErrorTol;
        [ReadOnly] public float PositionErrorTolSq;
        [ReadOnly] public float OrientationErrorTol;
        [ReadOnly] public float OrientationErrorTolCos;
        [ReadOnly] public float AngVelErrorTol;
        [ReadOnly] public float AngVelErrorTolSq;
        [ReadOnly] public float LinVelErrorTol;
        [ReadOnly] public float LinVelErrorTolSq;

        [GenerateTestsForBurstCompatibility]
        static void ValidateConstraintType(in Constraint constraint, in ConstraintType expectedType)
        {
            Assert.AreEqual(expectedType, constraint.Type, $"Validation ({expectedType}): unexpected constraint type '{constraint.Type}'.");
        }

        [GenerateTestsForBurstCompatibility]
        void Execute(in Entity entity, in PhysicsJoint joint, in PhysicsConstrainedBodyPair bodyPair)
        {
            var jointIndex = DynamicsWorld.GetJointIndex(entity);
            var dynamicsJoint = Joints[jointIndex];
            var bodyAIx = dynamicsJoint.BodyPair.BodyIndexA;
            var bodyBIx = dynamicsJoint.BodyPair.BodyIndexB;

            var bodyAIsStatic = bodyAIx < 0 || bodyAIx >= DynamicsWorld.NumMotions;
            var bodyBIsStatic = bodyBIx < 0 || bodyBIx >= DynamicsWorld.NumMotions;
            if (bodyAIsStatic && bodyBIsStatic)
            {
                return;
            }

            var bodyAWorld = bodyPair.EntityA != Entity.Null
                ? TransformLookup[bodyPair.EntityA].ToMatrix()
                : float4x4.identity;
            var bodyBWorld = bodyPair.EntityB != Entity.Null
                ? TransformLookup[bodyPair.EntityB].ToMatrix()
                : float4x4.identity;

            var anchorALocal = joint.BodyAFromJoint;
            var anchorBLocal = joint.BodyBFromJoint;
            var rigidAWorld = new RigidTransform(bodyAWorld);
            var rigidBWorld = new RigidTransform(bodyBWorld);
            var anchorAWorld = math.mul(rigidAWorld, anchorALocal.AsRigidTransform());
            var anchorBWorld = math.mul(rigidBWorld, anchorBLocal.AsRigidTransform());

            // PhysicsJoints 的姿势验证
            switch (joint.JointType)
            {
                case JointType.BallAndSocket:
                {
                    var deltaPos = anchorAWorld.pos - anchorBWorld.pos;
                    var posErrorSq = math.lengthsq(deltaPos);
                    if (posErrorSq > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (BallAndSocket): joint anchor position is violated by {math.sqrt(posErrorSq)} meters, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                case JointType.Hinge:
                case JointType.LimitedHinge:
                case JointType.AngularVelocityMotor:
                case JointType.RotationalMotor:
                {
                    // 获取铰链轴（连接到主体 A）
                    byte hingeConstraintBlockIndex = (byte)(joint.JointType == JointType.Hinge ? 0 : 1);
                    var hingeConstraint = joint[hingeConstraintBlockIndex];
                    ValidateConstraintType(hingeConstraint, ConstraintType.Angular);
                    var hingeAxisIndex = hingeConstraint.FreeAxis2D;
                    var hingeAxis = new float3x3(anchorAWorld.rot)[hingeAxisIndex];

                    // 确保绕铰链轴旋转
                    var rotBToA = math.mul(math.inverse(anchorAWorld.rot), anchorBWorld.rot);
                    rotBToA = math.normalize(rotBToA);
                    ((Quaternion)rotBToA).ToAngleAxis(out var angle, out var actualRotationAxis);
                    // 只有存在一定量的增量旋转，我们才能在两个锚点之间获得有意义的旋转轴。
                    // Note: 这里角度以度为单位
                    var absAngle = math.abs(angle);
                    var epsValidationAngle = 10.0f;
                    if (absAngle > epsValidationAngle && absAngle < 360f - epsValidationAngle)
                    {
                        actualRotationAxis = math.mul(anchorAWorld.rot, actualRotationAxis);
                        actualRotationAxis = math.normalize(actualRotationAxis);

                        // 确保铰链轴在两个锚架中对齐
                        var cosAngle = math.dot(actualRotationAxis, hingeAxis);
                        var absCosAngle = math.abs(cosAngle);
                        var epsCos = OrientationErrorTolCos;
                        if (absCosAngle < epsCos)
                        {
                            Errors.Add($"Validation (Hinge or equivalent): hinge axis orientation violated by {math.acos(absCosAngle)} radians, which exceeds orientation error tolerance of {OrientationErrorTol} radians");
                        }
                    }

                    // 确保锚点位置足够近，因为主体围绕它们旋转。
                    var deltaPos = anchorAWorld.pos - anchorBWorld.pos;
                    var posErrorSq = math.lengthsq(deltaPos);
                    if (posErrorSq > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (Hinge or equivalent): joint anchor position is violated by {math.sqrt(posErrorSq)} meters, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                case JointType.Fixed:
                {
                    // 确保锚框对齐

                    // 方向
                    var relQ = math.mul(math.inverse(anchorAWorld.rot), anchorBWorld.rot);
                    relQ = math.normalize(relQ);
                    var angle = 2.0 * math.acos(relQ.value.w);
                    var cosAngle = math.cos(angle);
                    if (cosAngle < OrientationErrorTolCos)
                    {
                        Errors.Add($"Validation (Fixed): relative orientation violated by {angle} radians, which exceeds orientation error tolerance of {OrientationErrorTol} radians");
                    }

                    // 位置
                    var deltaPos = anchorAWorld.pos - anchorBWorld.pos;
                    var posErrorSq = math.lengthsq(deltaPos);
                    if (posErrorSq > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (Fixed): joint anchor position is violated by {math.sqrt(posErrorSq)} meters, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                case JointType.Prismatic:
                case JointType.PositionalMotor:
                {
                    var constrainedAxisIndex = -1;
                    if (joint.JointType == JointType.Prismatic)
                    {
                        var linearConstraint = joint[1];
                        ValidateConstraintType(linearConstraint, ConstraintType.Linear);
                        constrainedAxisIndex = linearConstraint.ConstrainedAxis1D;
                    }
                    else if (joint.JointType == JointType.PositionalMotor)
                    {
                        var motorConstraint = joint[0];
                        ValidateConstraintType(motorConstraint, ConstraintType.PositionMotor);
                        constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;
                    }

                    Assert.IsTrue(constrainedAxisIndex > -1);

                    // 我们期望两个锚框架中的棱柱轴平行且方向相同。
                    var axisA = new float3x3(anchorAWorld.rot)[constrainedAxisIndex];
                    var axisB = new float3x3(anchorBWorld.rot)[constrainedAxisIndex];
                    var absCosAngle = math.dot(axisA, axisB);
                    if (absCosAngle < OrientationErrorTolCos)
                    {
                        Errors.Add($"Validation (Prismatic or equivalent): prismatic axis orientation violated by {math.acos(absCosAngle)} radians, which exceeds orientation error tolerance of {OrientationErrorTol} radians");
                    }

                    // 确保锚点位于棱柱轴上：
                    // A 中的锚点位置按设计位于棱柱轴 (i.e.、axisA) 上，因为两者都连接到同一刚体 A。
                    // 所以我们只需要检查 B 中的锚点位置到 A 中的棱柱轴的距离是否低于
                    // 提供位置误差容限。
                    var ab = anchorBWorld.pos - anchorAWorld.pos;
                    // 计算 ab 相对于由 axisA 和 anchorAWorld.pos 形成的平面的拒绝
                    ab -= math.dot(ab, axisA) * axisA;
                    var distToPrismaticAxisSq = math.lengthsq(ab);
                    if (distToPrismaticAxisSq > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (Prismatic or equivalent): joint anchor lies {math.sqrt(distToPrismaticAxisSq)} meters from prismatic axis, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                case JointType.LinearVelocityMotor:
                {
                    var motorConstraint = joint[0];
                    ValidateConstraintType(motorConstraint, ConstraintType.LinearVelocityMotor);
                    var constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;

                    // 我们期望两个锚架中的线速度电机轴（棱柱轴）平行且方向相同
                    var axisA = new float3x3(anchorAWorld.rot)[constrainedAxisIndex];
                    var axisB = new float3x3(anchorBWorld.rot)[constrainedAxisIndex];
                    var absCosAngle = math.dot(axisA, axisB);
                    if (absCosAngle < OrientationErrorTolCos)
                    {
                        Errors.Add($"Validation (LinearVelocityMotor): prismatic axis orientation violated by {math.acos(absCosAngle)} radians, which exceeds orientation error tolerance of {OrientationErrorTol} radians");
                    }

                    // 我们还期望 A 中的锚点位置位于附加到 B 的棱柱轴上。
                    var ba = anchorAWorld.pos - anchorBWorld.pos;
                    // 计算 ba 对于 axisB 和 anchorBWorld.pos 形成的平面的拒绝
                    ba -= math.dot(ba, axisB) * axisB;
                    var distToPrismaticAxisSq = math.lengthsq(ba);
                    if (distToPrismaticAxisSq > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (LinearVelocityMotor): joint anchor lies {math.sqrt(distToPrismaticAxisSq)} meters from prismatic axis, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                case JointType.LimitedDistance:
                {
                    var distanceConstraint = joint[0];
                    ValidateConstraintType(distanceConstraint, ConstraintType.Linear);

                    var min = distanceConstraint.Min;
                    var max = distanceConstraint.Max;

                    var deltaPos = anchorAWorld.pos - anchorBWorld.pos;
                    var distance = math.length(deltaPos);

                    if (distance < min - PositionErrorTol || distance > max + PositionErrorTol)
                    {
                        Errors.Add($"Validation (LimitedDistance): joint distance {distance} is out of admissible (min, max) range ({min}, {max}) by more than position error tolerance of {PositionErrorTol} meters.");
                    }

                    break;
                }
                default:
                    break;
            }

            // PhysicsJoints 的目标验证
            switch (joint.JointType)
            {
                case JointType.AngularVelocityMotor:
                {
                    // 得到期望的角速度
                    var motorConstraint = joint[2];
                    ValidateConstraintType(motorConstraint, ConstraintType.AngularVelocityMotor);
                    int constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;

                    // world 空间中的预期角速度
                    var speed = motorConstraint.Target[constrainedAxisIndex];
                    var expectedAngVelRel = new float3x3(anchorAWorld.rot)[constrainedAxisIndex] * speed;

                    // 获取实际角速度
                    var wA = bodyAIsStatic ? float3.zero
                        : PhysicsVelocityLookup[bodyPair.EntityA].GetAngularVelocityWorldSpace(PhysicsMassLookup[bodyPair.EntityA], new quaternion(bodyAWorld));
                    var wB = bodyBIsStatic ? float3.zero
                        : PhysicsVelocityLookup[bodyPair.EntityB].GetAngularVelocityWorldSpace(PhysicsMassLookup[bodyPair.EntityB], new quaternion(bodyBWorld));

                    // world 空间中的实际角速度（相对于 B）
                    var angVelRel = wA - wB;
                    var check = math.abs(math.lengthsq(expectedAngVelRel - angVelRel));
                    if (check > AngVelErrorTolSq)
                    {
                        Errors.Add($"Validation (AngularVelocityMotor): angular joint velocity {angVelRel} ({check}) exceeds expected angular velocity {expectedAngVelRel} by more than provided error tolerance of {AngVelErrorTol} rad/s.");
                    }

                    break;
                }
                case JointType.LinearVelocityMotor:
                {
                    var motorConstraint = joint[0];
                    ValidateConstraintType(motorConstraint, ConstraintType.LinearVelocityMotor);
                    int constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;

                    // world 空间中的预期角速度
                    var speed = motorConstraint.Target[constrainedAxisIndex];
                    var expectedLinVelRel = new float3x3(anchorBWorld.rot)[constrainedAxisIndex] * speed;

                    // 获取实际线速度
                    var vA = bodyAIsStatic ? float3.zero : PhysicsVelocityLookup[bodyPair.EntityA].Linear;
                    var vB = bodyBIsStatic ? float3.zero : PhysicsVelocityLookup[bodyPair.EntityB].Linear;

                    // world 空间中的实际线速度（相对于 B）
                    var linVelRel = vA - vB;

                    if (math.abs(math.lengthsq(expectedLinVelRel - linVelRel)) > LinVelErrorTolSq)
                    {
                        Errors.Add($"Validation (LinearVelocityMotor): linear joint velocity {linVelRel} exceeds expected linear velocity {expectedLinVelRel} by more than provided error tolerance of {LinVelErrorTol} m/s.");
                    }

                    break;
                }
                case JointType.RotationalMotor:
                {
                    var motorConstraint = joint[0];
                    ValidateConstraintType(motorConstraint, ConstraintType.RotationMotor);
                    int constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;
                    var targetAngle = motorConstraint.Target[constrainedAxisIndex];

                    // 计算 joint 附件框架之间的角度。
                    // Note: 我们已经确认 joint 轴在上面的姿势验证中的两个锚帧中对齐。
                    var qDelta = math.normalize(math.mul(math.inverse(anchorBWorld.rot), anchorAWorld.rot));
                    ((Quaternion)qDelta).ToAngleAxis(out var currentAngle, out var axis);
                    // 在 ToAngleAxis 计算中考虑轴翻转
                    currentAngle *= axis[constrainedAxisIndex];
                    currentAngle = math.radians(currentAngle);
                    var deltaAngle = currentAngle - targetAngle;
                    var deltaAngleCos = math.cos(deltaAngle);
                    // Note: 下面我们排除了顺从接头，因为在一般情况下这些接头无法以合理的精度达到目标。
                    var compliantJoint = motorConstraint.SpringFrequency < 1e3;
                    if (deltaAngleCos < OrientationErrorTolCos && !compliantJoint)
                    {
                        Errors.Add($"Validation (RotationalMotor): angle between anchor frames differs from target angle {targetAngle} radians by {deltaAngle} radians, which exceeds the orientation error tolerance of {OrientationErrorTol} radians.");
                    }

                    // 检查我们是否在限制范围内
                    if (currentAngle + OrientationErrorTol <= motorConstraint.Min || currentAngle - OrientationErrorTol >= motorConstraint.Max)
                    {
                        Errors.Add($"Validation (RotationalMotor): angle between anchor frames {currentAngle} is out of admissible (min, max) range ({motorConstraint.Min}, {motorConstraint.Max}) by more than orientation error tolerance of {OrientationErrorTol} radians.");
                    }
                    break;
                }
                case JointType.PositionalMotor:
                {
                    var motorConstraint = joint[0];
                    ValidateConstraintType(motorConstraint, ConstraintType.PositionMotor);
                    int constrainedAxisIndex = motorConstraint.ConstrainedAxis1D;
                    var targetCoordinate = motorConstraint.Target[constrainedAxisIndex];
                    var prismaticAxis = new float3x3(anchorAWorld.rot)[constrainedAxisIndex];
                    var targetAnchorPosA = prismaticAxis * targetCoordinate + anchorBWorld.pos;
                    var error = math.lengthsq(targetAnchorPosA - anchorAWorld.pos);
                    if (error > PositionErrorTolSq)
                    {
                        Errors.Add($"Validation (PositionalMotor): joint anchor lies {math.sqrt(error)} meters from target position, which exceeds position error tolerance of {PositionErrorTol} meters.");
                    }
                    break;
                }
                default:
                    break;
            }
        }
    }

    [BurstCompile]
    public partial struct ValidateRigidBodyAtRestJob : IJobEntity
    {
        [NativeDisableUnsafePtrRestriction] public SimulationValidationSystem.ErrorCounter Errors;

        [ReadOnly] public float MaxLinVel;
        [ReadOnly] public float MaxAngVel;
        [ReadOnly] public float MaxLinVelSq;
        [ReadOnly] public float MaxAngVelSq;

        [GenerateTestsForBurstCompatibility]
        void Execute(Entity entity, ref LocalTransform transform, ref PhysicsVelocity pv, ref PhysicsMass pm)
        {
            var vSq = math.lengthsq(pv.Linear);
            var wSq = math.lengthsq(pv.Angular);
            bool linVelAtRest = vSq <= MaxLinVelSq;
            bool angVelAtRest = wSq <= MaxAngVelSq;
            if (!linVelAtRest || !angVelAtRest)
            {
                Errors.Add(
                    $"Validation (Rigid Body, Entity: {entity.ToFixedString()}): (linear, angular) velocity is ({math.sqrt(vSq)}, {math.sqrt(wSq)}), which exceeds the (linear, angular) velocity error tolerance of ({MaxLinVel}, {MaxAngVel}).");
            }
        }
    }

    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct SimulationValidationSystem : ISystem
    {
        private ComponentLookup<LocalTransform> TransformLookup;

        private ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
        private ComponentLookup<PhysicsMass> PhysicsMassLookup;
        private int NumErrorsDetected;
        private ErrorCounter Errors;

        private float ElapsedTime;

        public struct ErrorCounter
        {
            private UnsafeAtomicCounter32 Counter;

            public unsafe ErrorCounter(int* errorCount)
            {
                Counter = new UnsafeAtomicCounter32(errorCount);
            }

            public void Add(in FixedString512Bytes errorMessage)
            {
                Debug.LogWarning(errorMessage);
                Counter.Add(1);
            }

            public unsafe int GetCount()
            {
                return *Counter.Counter;
            }

            public void Reset()
            {
                Counter.Reset();
            }
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationValidationSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            TransformLookup = state.GetComponentLookup<LocalTransform>();

            PhysicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>();
            PhysicsMassLookup = state.GetComponentLookup<PhysicsMass>();
            unsafe
            {
                fixed(int* numErrorsDetectedPtr = &NumErrorsDetected)
                {
                    Errors = new ErrorCounter(numErrorsDetectedPtr);
                }
            }
            ElapsedTime = 0.0f;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            // 因为我们需要 SimulationValidationSettings 存在才能更新此 system，
            // 我们可以确定我们可以找回它。
            var settings = SystemAPI.GetSingleton<SimulationValidationSettings>();

            if (!settings.EnableValidation)
            {
                return;
            }
            // else:

            // 检查在验证 jobs 安排的最后一帧中是否检测到任何错误（见下文）
            var numErrorsDetectedLastFrame = Errors.GetCount();
            // 为即将进行的验证重置错误计数器 jobs
            Errors.Reset();

            // Note: 我们需要计算自第一次更新 system 以来经过的时间。这是因为在 SubScene 流式传输过程中，SubScenes 已关闭
            // SubScenes 中的 systems 不会立即创建和步进。他们可能只会在几帧延迟后才被踩踏。因此，一段时间可能已经
            // passed (i.e., SystemAPI.Time.ElapsedTime > 0) the first time this system is updated.
            var elapsedTime = ElapsedTime;
            ElapsedTime += SystemAPI.Time.DeltaTime;
            if (settings.ValidationTimeRange[0] <= elapsedTime && (elapsedTime <= settings.ValidationTimeRange[1] || settings.ValidationTimeRange[1] < 0))
            {
                TransformLookup.Update(ref state);

                PhysicsVelocityLookup.Update(ref state);
                PhysicsMassLookup.Update(ref state);

                var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                var combinedHandle = new JobHandle();

                if (settings.ValidateJointBehavior)
                {
                    var handle = new ValidateJointBehaviorJob()
                    {
                        Errors = Errors,
                        TransformLookup = TransformLookup,

                        PhysicsVelocityLookup = PhysicsVelocityLookup,
                        PhysicsMassLookup = PhysicsMassLookup,
                        DynamicsWorld = physicsWorld.DynamicsWorld,
                        Joints = physicsWorld.DynamicsWorld.Joints,

                        PositionErrorTol = settings.PositionErrorTolerance,
                        PositionErrorTolSq = settings.PositionErrorTolerance * settings.PositionErrorTolerance,
                        OrientationErrorTol = settings.OrientationErrorTolerance,
                        OrientationErrorTolCos = math.cos(settings.OrientationErrorTolerance),
                        AngVelErrorTol = settings.AngularVelocityErrorTolerance,
                        AngVelErrorTolSq = settings.AngularVelocityErrorTolerance * settings.AngularVelocityErrorTolerance,
                        LinVelErrorTol = settings.LinearVelocityErrorTolerance,
                        LinVelErrorTolSq = settings.LinearVelocityErrorTolerance * settings.LinearVelocityErrorTolerance
                    }.ScheduleParallel(state.Dependency);
                    combinedHandle = JobHandle.CombineDependencies(combinedHandle, handle);
                }

                if (settings.ValidateRigidBodiesAtRest)
                {
                    var handle = new ValidateRigidBodyAtRestJob()
                    {
                        Errors = Errors,
                        MaxLinVel = settings.LinearVelocityErrorTolerance,
                        MaxAngVel = settings.AngularVelocityErrorTolerance,
                        MaxLinVelSq = settings.LinearVelocityErrorTolerance * settings.LinearVelocityErrorTolerance,
                        MaxAngVelSq = settings.AngularVelocityErrorTolerance * settings.AngularVelocityErrorTolerance
                    }.ScheduleParallel(state.Dependency);
                    combinedHandle = JobHandle.CombineDependencies(combinedHandle, handle);
                }

                state.Dependency = combinedHandle;
            }

            // 断言最后一帧是否检测到任何错误
            Assert.AreEqual(0, numErrorsDetectedLastFrame, $"SimulationValidationSystem: {numErrorsDetectedLastFrame} errors detected in simulation.");
        }
    }
}
