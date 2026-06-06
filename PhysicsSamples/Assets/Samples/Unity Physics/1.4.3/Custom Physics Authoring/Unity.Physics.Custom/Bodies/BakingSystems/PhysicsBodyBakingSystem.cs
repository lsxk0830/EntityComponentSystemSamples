using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    [TemporaryBakingType]
    public struct PhysicsBodyAuthoringData : IComponentData
    {
        public bool IsDynamic;
        public float Mass;
        public bool OverrideDefaultMassDistribution;
        public MassDistribution CustomMassDistribution;
    }

    class PhysicsBodyAuthoringBaker : BasePhysicsBaker<PhysicsBodyAuthoring>
    {
        internal List<UnityEngine.Collider> colliderComponents = new List<UnityEngine.Collider>();
        internal List<PhysicsShapeAuthoring> physicsShapeComponents = new List<PhysicsShapeAuthoring>();

        public override void Bake(PhysicsBodyAuthoring authoring)
        {
            // 优先考虑旧版 Components。如果由 Legacy 烘焙，请忽略。
            if (GetComponent<Rigidbody>()  || GetComponent<UnityEngine.Collider>())
            {
                return;
            }

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            // 稍后在 Baking System 中进行处理
            AddComponent(entity, new PhysicsBodyAuthoringData
            {
                IsDynamic = (authoring.MotionType == BodyMotionType.Dynamic),
                Mass = authoring.Mass,
                OverrideDefaultMassDistribution = authoring.OverrideDefaultMassDistribution,
                CustomMassDistribution = authoring.CustomMassDistribution
            });

            AddSharedComponent(entity, new PhysicsWorldIndex(authoring.WorldIndex));

            var bodyTransform = GetComponent<Transform>();

            var motionType = authoring.MotionType;
            var hasSmoothing = authoring.Smoothing != BodySmoothing.None;

            PostProcessTransform(bodyTransform, motionType);

            var customTags = authoring.CustomTags;
            if (!customTags.Equals(CustomPhysicsBodyTags.Nothing))
                AddComponent(entity, new PhysicsCustomTags { Value = customTags.Value });

            // 检查层次结构中至少有一个 collider 以添加这三个
            GetComponentsInChildren(colliderComponents);
            GetComponentsInChildren(physicsShapeComponents);
            if (colliderComponents.Count > 0 || physicsShapeComponents.Count > 0)
            {
                AddComponent(entity, new PhysicsCompoundData()
                {
                    AssociateBlobToBody = false,
                    ConvertedBodyInstanceID = authoring.GetInstanceID(),
                    Hash = default,
                });
                AddComponent<PhysicsRootBaked>(entity);
                AddComponent<PhysicsCollider>(entity);
            }

            if (authoring.MotionType == BodyMotionType.Static || IsStatic())
                return;

            var massProperties = MassProperties.UnitSphere;

            AddComponent(entity, authoring.MotionType == BodyMotionType.Dynamic ?
                PhysicsMass.CreateDynamic(massProperties, authoring.Mass) :
                PhysicsMass.CreateKinematic(massProperties));

            var physicsVelocity = new PhysicsVelocity
            {
                Linear = authoring.InitialLinearVelocity,
                Angular = authoring.InitialAngularVelocity
            };
            AddComponent(entity, physicsVelocity);

            if (authoring.MotionType == BodyMotionType.Dynamic)
            {
                // TODO 在编辑器中将这些设置为可选吗？
                AddComponent(entity, new PhysicsDamping
                {
                    Linear = authoring.LinearDamping,
                    Angular = authoring.AngularDamping
                });
                if (authoring.GravityFactor != 1)
                {
                    AddComponent(entity, new PhysicsGravityFactor
                    {
                        Value = authoring.GravityFactor
                    });
                }
            }
            else if (authoring.MotionType == BodyMotionType.Kinematic)
            {
                AddComponent(entity, new PhysicsGravityFactor
                {
                    Value = 0
                });
            }

            if (hasSmoothing)
            {
                AddComponent(entity, new PhysicsGraphicalSmoothing());
                if (authoring.Smoothing == BodySmoothing.Interpolation)
                {
                    AddComponent(entity, new PhysicsGraphicalInterpolationBuffer
                    {
                        PreviousTransform = Math.DecomposeRigidBodyTransform(bodyTransform.localToWorldMatrix),
                        PreviousVelocity = physicsVelocity,
                    });
                }
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(EndColliderBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsBodyBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            // 根据没有 colliders 的实体的自定义质量属性填写质量属性
            foreach (var(physicsMass, bodyData, entity) in
                     SystemAPI.Query<RefRW<PhysicsMass>, RefRO<PhysicsBodyAuthoringData>>()
                         .WithNone<PhysicsCollider>()
                         .WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                physicsMass.ValueRW = CreatePhysicsMass(entityManager, entity, bodyData.ValueRO, MassProperties.UnitSphere);
            }

            // 根据 collider 和自定义质量属性（如果提供）填写质量属性。
            foreach (var(physicsMass, bodyData, collider, entity) in
                     SystemAPI.Query<RefRW<PhysicsMass>, RefRO<PhysicsBodyAuthoringData>, RefRO<PhysicsCollider>>()
                         .WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                physicsMass.ValueRW = CreatePhysicsMass(entityManager, entity, bodyData.ValueRO,
                    collider.ValueRO.MassProperties, true);
            }
        }

        private PhysicsMass CreatePhysicsMass(EntityManager entityManager, in Entity entity,
            in PhysicsBodyAuthoringData inBodyData, in MassProperties inMassProperties, in bool hasCollider = false)
        {
            var massProperties = inMassProperties;
            var scale = 1f;

            // 按 LocalTransform.Scale 值缩放提供的质量属性以创建正确的质量属性
            // 刚体的初始质量分布。
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                scale = localTransform.Scale;

                massProperties.Scale(scale);
            }

            // 如果指定，则使用用户提供的值覆盖质量属性
            if (inBodyData.OverrideDefaultMassDistribution)
            {
                massProperties.MassDistribution = inBodyData.CustomMassDistribution;
                if (hasCollider)
                {
                    // 增加角膨胀系数以解决质心的移动
                    massProperties.AngularExpansionFactor += math.length(massProperties.MassDistribution.Transform.pos -
                        inBodyData.CustomMassDistribution.Transform.pos);
                }
            }

            // 创建物理质量属性。其中，这可以缩放单位质量惯性张量
            // 由刚体的标量质量。
            var physicsMass = inBodyData.IsDynamic ?
                PhysicsMass.CreateDynamic(massProperties, inBodyData.Mass) :
                PhysicsMass.CreateKinematic(massProperties);

            // 现在，将反比例应用于最终的烘焙物理质量属性，以防止无效的模拟质量属性
            // 由稍后构建物理 world 时质量属性的运行时缩放引起。
            physicsMass = physicsMass.ApplyScale(math.rcp(scale));

            return physicsMass;
        }
    }
}
