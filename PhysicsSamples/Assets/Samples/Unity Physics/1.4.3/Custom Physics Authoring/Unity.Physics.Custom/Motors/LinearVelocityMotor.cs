using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    public class LinearVelocityMotor : BaseJoint
    {
        [Tooltip("An offset from the center of the body with the motor (bodyA), representing the anchor point of translation.")]
        public float3 AnchorPosition;
        [Tooltip("The direction of the motor, relative to the orientation of the Connected Body (bodyB). Value will be normalized")]
        public float3 DirectionOfMovement;
        [Tooltip("Motor will drive at this speed from the initial position of bodyA, along the Direction of Movement, in m/s.")]
        public float TargetSpeed;
        [Tooltip("The magnitude of the maximum impulse the motor can exert in a single step. Applies only to the motor constraint.")]
        public float MaxImpulseAppliedByMotor = math.INFINITY;
        [Tooltip("A ratio describing how quickly a motor will arrive at the target. A value of 0 will oscillate about a solution indefinitely, while a value of 1 is critically damped. Default value is 2530.126 which describes a stiff spring")]
        public float DampingRatio = Constraint.DefaultDampingRatio;

        private float3 PerpendicularAxisLocal;
        private float3 PositionInConnectedEntity;
        private float3 AxisInConnectedEntity;
        private float3 PerpendicularAxisInConnectedEntity;

        class LinearVelocityMotorBaker : JointBaker<LinearVelocityMotor>
        {
            public override void Bake(LinearVelocityMotor authoring)
            {
                float3 axisInB = math.normalize(authoring.DirectionOfMovement);

                RigidTransform aFromB = math.mul(math.inverse(authoring.worldFromA), authoring.worldFromB);
                float3 axisInA = math.mul(aFromB.rot, axisInB); //电机轴相对于 bodyA

                RigidTransform bFromA = math.mul(math.inverse(authoring.worldFromB), authoring.worldFromA);
                authoring.PositionInConnectedEntity = math.transform(bFromA, authoring.AnchorPosition); //电机体相对于 world 空间中连接的 Entity 的位置
                authoring.AxisInConnectedEntity = axisInB; //连接 Entity 空间中的电机轴

                // 始终计算垂直轴
                Math.CalculatePerpendicularNormalized(axisInA, out var perpendicularAxisLocal, out _);
                authoring.PerpendicularAxisInConnectedEntity = math.mul(bFromA.rot, perpendicularAxisLocal); //连接 Entity 空间中的 perp 电机轴

                var joint = PhysicsJoint.CreateLinearVelocityMotor(
                    new BodyFrame
                    {
                        Axis = axisInA,
                        PerpendicularAxis = perpendicularAxisLocal,
                        Position = authoring.AnchorPosition
                    },
                    new BodyFrame
                    {
                        Axis = authoring.AxisInConnectedEntity,
                        PerpendicularAxis = authoring.PerpendicularAxisInConnectedEntity,
                        Position = authoring.PositionInConnectedEntity
                    },
                    authoring.TargetSpeed,
                    authoring.MaxImpulseAppliedByMotor,

                    Constraint.DefaultSpringFrequency,
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
