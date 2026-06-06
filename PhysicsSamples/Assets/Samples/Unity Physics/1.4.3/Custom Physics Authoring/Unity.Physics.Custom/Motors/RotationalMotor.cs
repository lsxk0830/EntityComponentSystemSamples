using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    public class RotationalMotor : BaseJoint
    {
        [Tooltip("An offset from center of entity with motor. Representing the anchor/pivot point of rotation")]
        public float3 PivotPosition;
        [Tooltip("The axis of rotation of the motor. Value will be normalized")]
        public float3 AxisOfRotation;
        [Tooltip("Motor will maintain this target angle around the AxisOfRotation, in degrees")]
        public float TargetAngle;
        [Tooltip("The magnitude of the maximum impulse the motor can exert in one step. Applies only to the motor constraint.")]
        public float MaxImpulseAppliedByMotor = math.INFINITY;
        [Tooltip("The spring frequency, in Hz. Default value is 74341.31 which describes a stiff spring.")]
        public float SpringFrequency = Constraint.DefaultSpringFrequency;
        [Tooltip("A ratio describing how quickly a motor will arrive at the target. A value of 0 will oscillate about a solution indefinitely, while a value of 1 critically damped. Default value is 2530.126 which describes a stiff spring")]
        public float DampingRatio = Constraint.DefaultDampingRatio;

        private float3 PerpendicularAxisLocal;
        private float3 PositionInConnectedEntity;
        private float3 HingeAxisInConnectedEntity;
        private float3 PerpendicularAxisInConnectedEntity;

        class RotationalMotorBaker : JointBaker<RotationalMotor>
        {
            public override void Bake(RotationalMotor authoring)
            {
                float3 axisInA = math.normalize(authoring.AxisOfRotation);

                RigidTransform bFromA = math.mul(math.inverse(authoring.worldFromB), authoring.worldFromA);
                authoring.PositionInConnectedEntity = math.transform(bFromA, authoring.PivotPosition); //电动主体枢轴相对于 world 空间中连接的 Entity 的位置
                authoring.HingeAxisInConnectedEntity = math.mul(bFromA.rot, axisInA); //连接 Entity 空间中的电机轴

                // 始终计算垂直轴
                Math.CalculatePerpendicularNormalized(axisInA, out var perpendicularLocal, out _);
                authoring.PerpendicularAxisInConnectedEntity = math.mul(bFromA.rot, perpendicularLocal); //连接 Entity 空间中的 perp 电机轴

                var joint = PhysicsJoint.CreateRotationalMotor(
                    new BodyFrame
                    {
                        Axis = axisInA,
                        PerpendicularAxis = perpendicularLocal,
                        Position = authoring.PivotPosition
                    },
                    new BodyFrame
                    {
                        Axis = authoring.HingeAxisInConnectedEntity,
                        PerpendicularAxis = authoring.PerpendicularAxisInConnectedEntity,
                        Position = authoring.PositionInConnectedEntity
                    },
                    math.radians(authoring.TargetAngle),
                    authoring.MaxImpulseAppliedByMotor,

                    authoring.SpringFrequency,
                    authoring.DampingRatio
                );

                joint.SetImpulseEventThresholdAllConstraints(authoring.MaxImpulse);
                var constraintBodyPair = GetConstrainedBodyPair(authoring);

                uint worldIndex = GetWorldIndexFromBaseJoint(authoring);
                CreateJointEntity(worldIndex, constraintBodyPair, joint);
            }
        }
    }
}
