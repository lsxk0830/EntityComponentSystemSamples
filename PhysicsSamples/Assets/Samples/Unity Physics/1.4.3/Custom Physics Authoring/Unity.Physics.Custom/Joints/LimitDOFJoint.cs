using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    // 此 Joint 允许您锁定受约束体的 6 个自由度中的一个或多个。
    // 这是通过组合适当的较低级别的“constraint 原子”以形成较高级别的 Joint 来实现的。
    // 在这种情况下，线性和角度 constraint 原子被组合。
    // Joint 的一个用例是将 3d 模拟限制在 2d 平面。
    public class LimitDOFJoint : BaseJoint
    {
        public bool3 LockLinearAxes;
        public bool3 LockAngularAxes;

        public PhysicsJoint CreateLimitDOFJoint(RigidTransform offset)
        {
            var constraints = new FixedList512Bytes<Constraint>();
            if (math.any(LockLinearAxes))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = LockLinearAxes,
                    Type = ConstraintType.Linear,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = Constraint.DefaultSpringFrequency,
                    DampingRatio = Constraint.DefaultDampingRatio,
                    MaxImpulse = MaxImpulse,
                });
            }
            if (math.any(LockAngularAxes))
            {
                constraints.Add(new Constraint
                {
                    ConstrainedAxes = LockAngularAxes,
                    Type = ConstraintType.Angular,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = Constraint.DefaultSpringFrequency,
                    DampingRatio = Constraint.DefaultDampingRatio,
                    MaxImpulse = MaxImpulse,
                });
            }

            var joint = new PhysicsJoint
            {
                BodyAFromJoint = BodyFrame.Identity,
                BodyBFromJoint = offset
            };
            joint.SetConstraints(constraints);
            return joint;
        }
    }

    class LimitDOFJointBaker : Baker<LimitDOFJoint>
    {
        public Entity CreateJointEntity(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, PhysicsJoint joint)
        {
            using (var joints = new NativeArray<PhysicsJoint>(1, Allocator.Temp) { [0] = joint })
            using (var jointEntities = new NativeList<Entity>(1, Allocator.Temp))
            {
                CreateJointEntities(worldIndex, constrainedBodyPair, joints, jointEntities);
                return jointEntities[0];
            }
        }

        public void CreateJointEntities(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, NativeArray<PhysicsJoint> joints, NativeList<Entity> newJointEntities = default)
        {
            if (!joints.IsCreated || joints.Length == 0)
                return;

            if (newJointEntities.IsCreated)
                newJointEntities.Clear();
            else
                newJointEntities = new NativeList<Entity>(joints.Length, Allocator.Temp);

            // 创建所有新关节
            var multipleJoints = joints.Length > 1;

            for (var i = 0; i < joints.Length; ++i)
            {
                var jointEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddSharedComponent(jointEntity, new PhysicsWorldIndex(worldIndex));

                AddComponent(jointEntity, constrainedBodyPair);
                AddComponent(jointEntity, joints[i]);

                newJointEntities.Add(jointEntity);
            }

            if (multipleJoints)
            {
                // 为新关节设置伴随缓冲区
                for (var i = 0; i < joints.Length; ++i)
                {
                    var companions = AddBuffer<PhysicsJointCompanion>(newJointEntities[i]);
                    for (var j = 0; j < joints.Length; ++j)
                    {
                        if (i == j)
                            continue;
                        companions.Add(new PhysicsJointCompanion {JointEntity = newJointEntities[j]});
                    }
                }
            }
        }

        protected PhysicsConstrainedBodyPair GetConstrainedBodyPair(LimitDOFJoint authoring)
        {
            return new PhysicsConstrainedBodyPair(
                GetEntity(TransformUsageFlags.Dynamic),
                authoring.ConnectedBody == null ? Entity.Null : GetEntity(authoring.ConnectedBody, TransformUsageFlags.Dynamic),
                authoring.EnableCollision
            );
        }

        public uint GetWorldIndex(Component c)
        {
            uint worldIndex = 0;
            var physicsBody = GetComponent<PhysicsBodyAuthoring>(c);
            if (physicsBody != null)
            {
                worldIndex = physicsBody.WorldIndex;
            }
            return worldIndex;
        }

        public override void Bake(LimitDOFJoint authoring)
        {
            if (!math.any(authoring.LockLinearAxes) && !math.any(authoring.LockAngularAxes))
                return;

            RigidTransform bFromA = math.mul(math.inverse(authoring.worldFromB), authoring.worldFromA);
            PhysicsJoint physicsJoint = authoring.CreateLimitDOFJoint(bFromA);

            var worldIndex = GetWorldIndex(authoring);
            CreateJointEntity(
                worldIndex,
                GetConstrainedBodyPair(authoring),
                physicsJoint
            );
        }
    }
}
