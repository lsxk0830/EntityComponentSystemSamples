using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using static Unity.Physics.Math;

namespace Unity.Physics.Extensions
{
    // 一个鼠标拾取收集器，可存储每次点击。基于 ClosestHitCollector
    [BurstCompile]
    public struct MousePickCollector : ICollector<RaycastHit>
    {
        public bool IgnoreTriggers;
        public bool IgnoreStatic;
        public int NumDynamicBodies;

        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction { get; private set; }
        public int NumHits { get; private set; }

        public RaycastHit Hit;

        public MousePickCollector(int numDynamicBodies, float maxFraction = 1.0f)
        {
            Hit = default;
            MaxFraction = maxFraction;
            NumHits = 0;
            IgnoreTriggers = true;
            IgnoreStatic = true;
            NumDynamicBodies = numDynamicBodies;
        }

        #region ICollector

        public bool AddHit(RaycastHit hit)
        {
            Assert.IsTrue(hit.Fraction <= MaxFraction);

            var isAcceptable = true;
            if (IgnoreStatic)
            {
                isAcceptable &= hit.RigidBodyIndex >= 0 && hit.RigidBodyIndex < NumDynamicBodies;
            }
            if (IgnoreTriggers)
            {
                isAcceptable &= hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents;
            }

            if (!isAcceptable)
            {
                return false;
            }

            MaxFraction = hit.Fraction;
            Hit = hit;
            NumHits = 1;
            return true;
        }

        #endregion
    }

    public struct MousePick : IComponentData
    {
        public bool IgnoreTriggers;
        public bool IgnoreStatic;
        public bool DeleteEntityOnClick;
    }

    [DisallowMultipleComponent]
    public class MousePickAuthoring : MonoBehaviour
    {
        public bool IgnoreTriggers = true;
        public bool IgnoreStatic = true;
        public bool DeleteEntityOnClick = false;

        // Note: 覆盖 OnEnable 以便能够在编辑器中禁用 component
        protected void OnEnable() {}
    }

    class MousePickBaker : Baker<MousePickAuthoring>
    {
        public override void Bake(MousePickAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MousePick()
            {
                IgnoreTriggers = authoring.IgnoreTriggers,
                IgnoreStatic = authoring.IgnoreStatic,
                DeleteEntityOnClick = authoring.DeleteEntityOnClick
            });
        }
    }

    // 将虚拟弹簧附加到选取的 entity
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial class MousePickSystem : SystemBase
    {
        public const float k_MaxDistance = 100.0f;
        public NativeReference<SpringData> SpringDataRef;
        public JobHandle? PickJobHandle;

        public struct SpringData
        {
            public Entity Entity;
            public bool Picked;
            public float3 PointOnBody;
            public float MouseDepth;
        }

        [BurstCompile]
        struct Pick : IJob
        {
            [ReadOnly] public CollisionWorld CollisionWorld;
            public NativeReference<SpringData> SpringDataRef;
            public RaycastInput RayInput;
            public float Near;
            public float3 Forward;
            [ReadOnly] public bool IgnoreTriggers;
            [ReadOnly] public bool IgnoreStatic;

            public void Execute()
            {
                var mousePickCollector = new MousePickCollector(CollisionWorld.NumDynamicBodies)
                {
                    IgnoreTriggers = IgnoreTriggers,
                    IgnoreStatic = IgnoreStatic
                };

                if (CollisionWorld.CastRay(RayInput, ref mousePickCollector))
                {
                    float fraction = mousePickCollector.Hit.Fraction;
                    RigidBody hitBody = CollisionWorld.Bodies[mousePickCollector.Hit.RigidBodyIndex];

                    MTransform bodyFromWorld = Inverse(new MTransform(hitBody.WorldFromBody));
                    float3 pointOnBody = Mul(bodyFromWorld, mousePickCollector.Hit.Position);

                    SpringDataRef.Value = new SpringData
                    {
                        Entity = hitBody.Entity,
                        Picked = true,
                        PointOnBody = pointOnBody,
                        MouseDepth = Near + math.dot(math.normalize(RayInput.End - RayInput.Start), Forward) * fraction * k_MaxDistance,
                    };
                }
                else
                {
                    SpringDataRef.Value = new SpringData
                    {
                        Picked = false
                    };
                }
            }
        }

        public MousePickSystem()
        {
            SpringDataRef = new NativeReference<SpringData>(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            SpringDataRef.Value = new SpringData();
        }

        protected override void OnCreate()
        {
            RequireForUpdate<MousePick>();
        }

        protected override void OnDestroy()
        {
            SpringDataRef.Dispose();
        }

        protected override void OnUpdate()
        {
            if (Input.GetMouseButtonDown(0) && (Camera.main != null))
            {
                Vector2 mousePosition = Input.mousePosition;
                UnityEngine.Ray unityRay = Camera.main.ScreenPointToRay(mousePosition);

                var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                var mousePick = SystemAPI.GetSingleton<MousePick>();
                // Schedule 拾取 job，碰撞后 world 已建成
                Dependency = new Pick
                {
                    CollisionWorld = world.CollisionWorld,
                    SpringDataRef = SpringDataRef,
                    RayInput = new RaycastInput
                    {
                        Start = unityRay.origin,
                        End = unityRay.origin + unityRay.direction * k_MaxDistance,
                        Filter = CollisionFilter.Default,
                    },
                    Near = Camera.main.nearClipPlane,
                    Forward = Camera.main.transform.forward,
                    IgnoreTriggers = mousePick.IgnoreTriggers,
                    IgnoreStatic = mousePick.IgnoreStatic,
                }.Schedule(Dependency);

                PickJobHandle = Dependency;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (PickJobHandle != null)
                {
                    PickJobHandle.Value.Complete();
                }
                SpringDataRef.Value = new SpringData();
            }
        }
    }

    // 将任何鼠标弹簧应用为 entity 运动 component 的速度变化
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial class MouseSpringSystem : SystemBase
    {
        MousePickSystem m_PickSystem;

        protected override void OnCreate()
        {
            m_PickSystem = World.GetOrCreateSystemManaged<MousePickSystem>();
            RequireForUpdate<MousePick>();
        }

        protected override void OnUpdate()
        {
            ComponentLookup<LocalTransform> LocalTransforms = GetComponentLookup<LocalTransform>(true);

            ComponentLookup<PhysicsVelocity> Velocities = GetComponentLookup<PhysicsVelocity>();
            ComponentLookup<PhysicsMass> Masses = GetComponentLookup<PhysicsMass>(true);
            ComponentLookup<PhysicsMassOverride> MassOverrides = GetComponentLookup<PhysicsMassOverride>(true);

            // 如果有选择 job，请等待它完成
            if (m_PickSystem.PickJobHandle != null)
            {
                JobHandle.CombineDependencies(Dependency, m_PickSystem.PickJobHandle.Value).Complete();
            }

            // 如果有选中的 entity，请将其拖动
            MousePickSystem.SpringData springData = m_PickSystem.SpringDataRef.Value;
            if (springData.Picked)
            {
                var mousePick = SystemAPI.GetSingleton<MousePick>();
                if (mousePick.DeleteEntityOnClick)
                {
                    EntityManager.DestroyEntity(springData.Entity);

                    // 重置弹簧数据
                    m_PickSystem.SpringDataRef.Value = new MousePickSystem.SpringData();
                    return;
                }
                // else:

                Entity entity = springData.Entity;
                if (!Masses.HasComponent(entity))
                {
                    return;
                }

                PhysicsMass massComponent = Masses[entity];
                PhysicsVelocity velocityComponent = Velocities[entity];

                // 如果身体是运动学的
                // TODO: 你应该能够旋转质量无限但惯性有限的物体
                if (massComponent.HasInfiniteMass || MassOverrides.HasComponent(entity) && MassOverrides[entity].IsKinematic != 0)
                {
                    return;
                }


                var worldFromBody = new MTransform(LocalTransforms[entity].Rotation, LocalTransforms[entity].Position);


                // 身体到动作的变换
                var bodyFromMotion = new MTransform(Masses[entity].InertiaOrientation, Masses[entity].CenterOfMass);
                MTransform worldFromMotion = Mul(worldFromBody, bodyFromMotion);

                // TODO: 不应在惯性质量或惯性处阻尼
                // 阻尼当前速度
                const float gain = 0.95f;
                velocityComponent.Linear *= gain;
                velocityComponent.Angular *= gain;

                // 获取 world 空间中的主体和鼠标点
                float3 pointBodyWs = Mul(worldFromBody, springData.PointOnBody);
                float3 pointSpringWs = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, springData.MouseDepth));

                // 计算所需的速度变化
                float3 pointBodyLs = Mul(Inverse(bodyFromMotion), springData.PointOnBody);
                float3 deltaVelocity;
                {
                    float3 pointDiff = pointBodyWs - pointSpringWs;
                    float3 relativeVelocityInWorld = velocityComponent.Linear + math.mul(worldFromMotion.Rotation, math.cross(velocityComponent.Angular, pointBodyLs));

                    const float elasticity = 0.1f;
                    const float damping = 0.5f;
                    deltaVelocity = -pointDiff * (elasticity / SystemAPI.Time.DeltaTime) - damping * relativeVelocityInWorld;
                }

                // 在 world 空间中构建有效质量矩阵
                // TODO 如何表示具有 inf 惯性和有限质量的物体
                // TODO 如果拖动不均匀的形状，激进的阻尼会隐藏此代码中的错误
                float3x3 effectiveMassMatrix;
                {
                    float3 arm = pointBodyWs - worldFromMotion.Translation;
                    var skew = new float3x3(
                        new float3(0.0f, arm.z, -arm.y),
                        new float3(-arm.z, 0.0f, arm.x),
                        new float3(arm.y, -arm.x, 0.0f)
                    );

                    // world 空间惯量 = worldFromMotion * inertiaInMotionSpace * motionFromWorld
                    var invInertiaWs = new float3x3(
                        massComponent.InverseInertia.x * worldFromMotion.Rotation.c0,
                        massComponent.InverseInertia.y * worldFromMotion.Rotation.c1,
                        massComponent.InverseInertia.z * worldFromMotion.Rotation.c2
                    );
                    invInertiaWs = math.mul(invInertiaWs, math.transpose(worldFromMotion.Rotation));

                    float3x3 invEffMassMatrix = math.mul(math.mul(skew, invInertiaWs), skew);
                    invEffMassMatrix.c0 = new float3(massComponent.InverseMass, 0.0f, 0.0f) - invEffMassMatrix.c0;
                    invEffMassMatrix.c1 = new float3(0.0f, massComponent.InverseMass, 0.0f) - invEffMassMatrix.c1;
                    invEffMassMatrix.c2 = new float3(0.0f, 0.0f, massComponent.InverseMass) - invEffMassMatrix.c2;

                    effectiveMassMatrix = math.inverse(invEffMassMatrix);
                }

                // 计算引起所需速度变化的冲量
                float3 impulse = math.mul(effectiveMassMatrix, deltaVelocity);

                // 抑制冲动
                const float maxAcceleration = 250.0f;
                float maxImpulse = math.rcp(massComponent.InverseMass) * SystemAPI.Time.DeltaTime * maxAcceleration;
                impulse *= math.min(1.0f, math.sqrt((maxImpulse * maxImpulse) / math.lengthsq(impulse)));

                // 施加冲动
                {
                    velocityComponent.Linear += impulse * massComponent.InverseMass;

                    float3 impulseLs = math.mul(math.transpose(worldFromMotion.Rotation), impulse);
                    float3 angularImpulseLs = math.cross(pointBodyLs, impulseLs);
                    velocityComponent.Angular += angularImpulseLs * massComponent.InverseInertia;
                }

                // 回写速度
                Velocities[entity] = velocityComponent;
            }
        }
    }
}
