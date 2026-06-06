using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using UnityEngine;
using FloatRange = Unity.Physics.Math.FloatRange;

namespace Unity.Physics.Authoring
{
    // 存储初始值和一对标量曲线以应用于 joint 上的相关约束
    struct ModifyJointLimits : ISharedComponentData, IEquatable<ModifyJointLimits>
    {
        public PhysicsJoint InitialValue;
        public ParticleSystem.MinMaxCurve AngularRangeScalar;
        public ParticleSystem.MinMaxCurve LinearRangeScalar;

        public bool Equals(ModifyJointLimits other) =>
            AngularRangeScalar.Equals(other.AngularRangeScalar) && LinearRangeScalar.Equals(other.LinearRangeScalar);

        public override bool Equals(object obj) => obj is ModifyJointLimits other && Equals(other);

        public override int GetHashCode() =>
            unchecked((AngularRangeScalar.GetHashCode() * 397) ^ LinearRangeScalar.GetHashCode());
    }

    // 将 authoring component 添加到具有一个或多个 Joint 的 GameObject
    public class ModifyJointLimitsAuthoring : MonoBehaviour
    {
        public ParticleSystem.MinMaxCurve AngularRangeScalar = new ParticleSystem.MinMaxCurve(
            1f,
            min: new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(2f, -2f, 0f, 0f),
                new Keyframe(4f, 0f, 0f, 0f)
            )
            {
                preWrapMode = WrapMode.Loop,
                postWrapMode = WrapMode.Loop
            },
            max: new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(2f, -1f, 0f, 0f),
                new Keyframe(4f, 1f, 0f, 0f)
            )
            {
                preWrapMode = WrapMode.Loop,
                postWrapMode = WrapMode.Loop
            }
        );

        public ParticleSystem.MinMaxCurve LinearRangeScalar = new ParticleSystem.MinMaxCurve(
            1f,
            min: new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(2f, 0.5f, 0f, 0f),
                new Keyframe(4f, 1f, 0f, 0f)
            )
            {
                preWrapMode = WrapMode.Loop,
                postWrapMode = WrapMode.Loop
            },
            max: new AnimationCurve(
                new Keyframe(0f, 0.5f, 0f, 0f),
                new Keyframe(2f, 0f, 0f, 0f),
                new Keyframe(4f, 0.5f, 0f, 0f)
            )
            {
                preWrapMode = WrapMode.Loop,
                postWrapMode = WrapMode.Loop
            }
        );
    }

    [BakingType]
    public class ModifyJointLimitsBakingData : IComponentData
    {
        public ParticleSystem.MinMaxCurve AngularRangeScalar;
        public ParticleSystem.MinMaxCurve LinearRangeScalar;
    }

    class ModifyJointLimitsBaker : Baker<ModifyJointLimitsAuthoring>
    {
        public override void Bake(ModifyJointLimitsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new ModifyJointLimitsBakingData
            {
                AngularRangeScalar = authoring.AngularRangeScalar,
                LinearRangeScalar = authoring.LinearRangeScalar
            });
        }
    }

    // 关节转换后，找到它们生成的 entities，并将 ModifyJointLimits 添加到其中
    [UpdateAfter(typeof(EndJointBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct ModifyJointLimitsBakingSystem : ISystem
    {
        private EntityQuery _ModifyJointLimitsBakingDataQuery;
        private EntityQuery _JointEntityBakingQuery;

        public void OnCreate(ref SystemState state)
        {
            _ModifyJointLimitsBakingDataQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<ModifyJointLimitsBakingData>()},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });

            _JointEntityBakingQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<JointEntityBaking>()}
            });

            _ModifyJointLimitsBakingDataQuery.AddChangedVersionFilter(typeof(ModifyJointLimitsBakingData));
            _JointEntityBakingQuery.AddChangedVersionFilter(typeof(JointEntityBaking));
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_ModifyJointLimitsBakingDataQuery.IsEmpty && _JointEntityBakingQuery.IsEmpty)
            {
                return;
            }

            // 收集所有关节
            NativeParallelMultiHashMap<Entity, (Entity, PhysicsJoint)> jointsLookUp =
                new NativeParallelMultiHashMap<Entity, (Entity, PhysicsJoint)>(10, Allocator.TempJob);

            foreach (var(jointEntity, physicsJoint, entity) in SystemAPI
                     .Query<RefRO<JointEntityBaking>, RefRO<PhysicsJoint>>().WithEntityAccess()
                     .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                jointsLookUp.Add(jointEntity.ValueRO.Entity, (entity, physicsJoint.ValueRO));
            }

            foreach (var(modifyJointLimits, entity) in SystemAPI.Query<ModifyJointLimitsBakingData>()
                     .WithEntityAccess().WithOptions(EntityQueryOptions.IncludeDisabledEntities |
                         EntityQueryOptions.IncludePrefab))
            {
                var angularModification = new ParticleSystem.MinMaxCurve(
                    multiplier: math.radians(modifyJointLimits.AngularRangeScalar.curveMultiplier),
                    min: modifyJointLimits.AngularRangeScalar.curveMin,
                    max: modifyJointLimits.AngularRangeScalar.curveMax
                );

                foreach (var joint in jointsLookUp.GetValuesForKey(entity))
                {
                    state.EntityManager.SetSharedComponentManaged(joint.Item1, new ModifyJointLimits
                    {
                        InitialValue = joint.Item2,
                        AngularRangeScalar = angularModification,
                        LinearRangeScalar = modifyJointLimits.LinearRangeScalar
                    });
                }
            }

            jointsLookUp.Dispose();
        }
    }

    // 对支持的关节类型的限制应用动画效果
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PhysicsSystemGroup), OrderLast = true)]
    partial struct ModifyJointLimitsSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var(joint, modification) in SystemAPI.Query<RefRW<PhysicsJoint>, ModifyJointLimits>())
            {
                var animatedAngularScalar = new FloatRange(
                    modification.AngularRangeScalar.curveMin.Evaluate(time),
                    modification.AngularRangeScalar.curveMax.Evaluate(time)
                );
                var animatedLinearScalar = new FloatRange(
                    modification.LinearRangeScalar.curveMin.Evaluate(time),
                    modification.LinearRangeScalar.curveMax.Evaluate(time)
                );

                // 在每种情况下，根据 joint 类型从初始值获取相关属性，并应用标量
                switch (joint.ValueRW.JointType)
                {
                    // 自定义类型可以是任何类型，因此此演示仅将更改应用于所有约束
                    case JointType.Custom:
                        var constraints = modification.InitialValue.GetConstraints();
                        for (var i = 0; i < constraints.Length; i++)
                        {
                            var constraint = constraints[i];
                            var isAngular = constraint.Type == ConstraintType.Angular;
                            var scalar = math.select(animatedLinearScalar, animatedAngularScalar, isAngular);
                            var constraintRange = (FloatRange)(new float2(constraint.Min, constraint.Max) * scalar);
                            constraint.Min = constraintRange.Min;
                            constraint.Max = constraintRange.Max;
                            constraints[i] = constraint;
                        }

                        joint.ValueRW.SetConstraints(constraints);
                        break;
                    // 其他类型有相应的 getter/setter 来检索更有意义的数据
                    case JointType.LimitedDistance:
                        var distanceRange = modification.InitialValue.GetLimitedDistanceRange();
                        joint.ValueRW.SetLimitedDistanceRange(distanceRange * (float2)animatedLinearScalar);
                        break;
                    case JointType.LimitedHinge:
                        var angularRange = modification.InitialValue.GetLimitedHingeRange();
                        joint.ValueRW.SetLimitedHingeRange(angularRange * (float2)animatedAngularScalar);
                        break;
                    case JointType.Prismatic:
                        var distanceOnAxis = modification.InitialValue.GetPrismaticRange();
                        joint.ValueRW.SetPrismaticRange(distanceOnAxis * (float2)animatedLinearScalar);
                        break;
                    // 布娃娃关节由两个具有不同含义的独立关节组成
                    case JointType.RagdollPrimaryCone:
                        modification.InitialValue.GetRagdollPrimaryConeAndTwistRange(
                            out var maxConeAngle,
                            out var angularTwistRange
                        );
                        joint.ValueRW.SetRagdollPrimaryConeAndTwistRange(
                            maxConeAngle * animatedAngularScalar.Max,
                            angularTwistRange * (float2)animatedAngularScalar
                        );
                        break;
                    case JointType.RagdollPerpendicularCone:
                        var angularPlaneRange = modification.InitialValue.GetRagdollPerpendicularConeRange();
                        joint.ValueRW.SetRagdollPerpendicularConeRange(angularPlaneRange *
                            (float2)animatedAngularScalar);
                        break;
                    // 其余类型对其 Constraint 原子进行有意义的修改没有限制
                    case JointType.BallAndSocket:
                    case JointType.Fixed:
                    case JointType.Hinge:
                        break;
                }
            }
        }
    }
}
