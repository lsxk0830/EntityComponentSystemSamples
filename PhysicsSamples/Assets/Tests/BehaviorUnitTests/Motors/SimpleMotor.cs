using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    public class SimpleMotor : BaseJoint
    {
        // 电机类型。选择订单以匹配 Unity.Physics.JointType
        public enum MotorType
        {
            AngularPosition,
            AngularVelocity,
            LinearPosition,
            LinearVelocity
        }

        [Tooltip("Type of the motor.")]
        public MotorType Type;
        [Tooltip("An offset from the center of the entity with the motor, representing the anchor point.")]
        public float3 AnchorPosition;
        [Tooltip("The direction of the motor in the space of the entity with the motor. Value will be normalized")]
        public float3 DirectionOfMovement;
        [Tooltip("Motor drive target (length for position, rotation in degrees for angular position, linear/angular speed (m/s and degrees/s) for velocity motor.")]
        public float Target;
        [Tooltip("The magnitude of the maximum impulse the motor can exert in a single step. Applies only to the motor constraint.")]
        public float MaxImpulseAppliedByMotor = math.INFINITY;

        class SimpleMotorBaker : JointBaker<SimpleMotor>
        {
            public override void Bake(SimpleMotor authoring)
            {
                float3 axis = math.normalize(authoring.DirectionOfMovement);
                float target = authoring.Type == MotorType.AngularPosition || authoring.Type == MotorType.AngularVelocity ? math.radians(authoring.Target) : authoring.Target;

                RigidTransform bFromA = math.mul(math.inverse(authoring.worldFromB), authoring.worldFromA);
                float3 anchorInB = math.transform(bFromA, authoring.AnchorPosition); //world 空间中锚点相对于已连接 Entity 的位置
                float3 axisInB = math.mul(bFromA.rot, axis); //连接 Entity 空间中的电机轴

                // 始终计算垂直轴
                Math.CalculatePerpendicularNormalized(axis, out var perpendicularLocal, out _);
                float3 perpendicularAxisInB = math.mul(bFromA.rot, perpendicularLocal); //连接 Entity 空间中的 perp 电机轴

                JointType jointType = JointType.Custom;
                var constraints = new FixedList512Bytes<Constraint>();
                Constraint constraint;
                switch (authoring.Type)
                {
                    case MotorType.AngularPosition:
                        jointType = JointType.RotationalMotor;
                        constraint = Constraint.MotorTwist(target, authoring.MaxImpulseAppliedByMotor);
                        break;
                    case MotorType.AngularVelocity:
                        jointType = JointType.AngularVelocityMotor;
                        constraint = Constraint.AngularVelocityMotor(target, authoring.MaxImpulseAppliedByMotor);
                        break;
                    case MotorType.LinearPosition:
                        jointType = JointType.PositionalMotor;
                        constraint = Constraint.MotorPlanar(target, authoring.MaxImpulseAppliedByMotor);
                        break;
                    case MotorType.LinearVelocity:
                        jointType = JointType.LinearVelocityMotor;
                        constraint = Constraint.LinearVelocityMotor(target, authoring.MaxImpulseAppliedByMotor);
                        break;
                    default:
                        Debug.LogError("Unsupported simple motor type");
                        return;
                }

                constraint.MaxImpulse = authoring.MaxImpulse;
                constraints.Add(constraint);

                var joint = new PhysicsJoint
                {
                    BodyAFromJoint = new BodyFrame
                    {
                        Axis = axis,
                        PerpendicularAxis = perpendicularLocal,
                        Position = authoring.AnchorPosition
                    },
                    BodyBFromJoint = new BodyFrame
                    {
                        Axis = axisInB,
                        PerpendicularAxis = perpendicularAxisInB,
                        Position = anchorInB
                    },
                    JointType = jointType
                };
                joint.SetConstraints(constraints);

                var constraintBodyPair = GetConstrainedBodyPair(authoring);

                uint worldIndex = GetWorldIndexFromBaseJoint(authoring);
                CreateJointEntity(worldIndex, constraintBodyPair, joint);
            }
        }
    }
}
