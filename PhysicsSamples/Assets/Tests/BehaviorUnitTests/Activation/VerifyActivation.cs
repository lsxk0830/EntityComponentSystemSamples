using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Unity.Physics.Tests
{
    public struct VerifyActivationData : IComponentData
    {
        public int PureFilter;
        public int Remove;
        public int MotionChange;
        public int Teleport;
        public int ColliderChange;
        public int NewCollider;
    }

    public class VerifyActivationScene : SceneCreationSettings {}

    public class VerifyActivation : SceneCreationAuthoring<VerifyActivationScene>
    {
        public bool PureFilter = true;
        public bool Remove = true;
        public bool MotionChange = true;
        public bool Teleport = true;
        public bool ColliderChange = true;
        public bool NewCollider = true;

        class VerifyActivationBaker : Baker<VerifyActivation>
        {
            public override void Bake(VerifyActivation authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, new VerifyActivationScene()
                {
                    DynamicMaterial = authoring.DynamicMaterial,
                    StaticMaterial = authoring.StaticMaterial
                });
                AddComponent(entity, new VerifyActivationData
                {
                    PureFilter = authoring.PureFilter ? 1 : 0,
                    Remove = authoring.Remove ? 1 : 0,
                    MotionChange = authoring.MotionChange ? 1 : 0,
                    Teleport = authoring.Teleport ? 1 : 0,
                    ColliderChange = authoring.ColliderChange ? 1 : 0,
                    NewCollider = authoring.NewCollider ? 1 : 0
                });
            }
        }
    }

    public partial class VerifyActivationSystem : SceneCreationSystem<VerifyActivationScene>
    {
        public override void CreateScene(VerifyActivationScene sceneSettings)
        {
            // 常用参数
            float3 groundSize = new float3(5.0f, 1.0f, 5.0f);
            float3 boxSize = new float3(1.0f, 1.0f, 1.0f);
            float mass = 1.0f;

            Entity e = SystemAPI.ManagedAPI.GetSingletonEntity<VerifyActivationScene>();
            VerifyActivationData data = SystemAPI.GetComponent<VerifyActivationData>(e);

            // 地面不做任何事情（除了更换过滤器）和动态盒在其上
            if (data.PureFilter == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(-30.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(-30.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }

            // 接地以移除其上的动态框
            if (data.Remove == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(-20.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(-20.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }

            // 地面转换为动态并在其上方动态框
            if (data.MotionChange == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(-10.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(-10.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }

            // 地面传送和其上的动态框
            if (data.Teleport == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(0.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(0.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }

            // 接地改 collider 及其上的动力盒
            if (data.ColliderChange == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(10.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(10.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }

            // 地面设置新的 collider 和动态盒在其上
            if (data.NewCollider == 1)
            {
                var groundCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = groundSize });
                CreateStaticBody(new float3(20.0f, 0.0f, 0.0f), quaternion.identity, groundCollider);
                var boxCollider = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = boxSize });
                CreateDynamicBody(new float3(20.0f, 1.0f, 0.0f), quaternion.identity, boxCollider, float3.zero, float3.zero, mass);

                CreatedColliders.Add(groundCollider);
                CreatedColliders.Add(boxCollider);
            }
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [BurstCompile]
    public partial struct TestSystem : ISystem
    {
        private int m_Counter;

        EntityQuery m_VerificationGroup;
        ComponentLookup<VerifyActivationData> m_ActivationData;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_VerificationGroup = state.GetEntityQuery(ComponentType.ReadWrite<VerifyActivationData>());
            state.RequireForUpdate(m_VerificationGroup);
            m_Counter = 0;

            m_ActivationData = state.GetComponentLookup<VerifyActivationData>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            HandleUpdate(ref state);
        }

        internal void HandleUpdate(ref SystemState state)
        {
            m_Counter++;
            m_ActivationData.Update(ref state);
            if (m_Counter == 30)
            {
                VerifyActivationSystem system = state.World.GetExistingSystemManaged<VerifyActivationSystem>();

                // 先换滤镜 colliders 全地无碰撞
                var bpwData = state.EntityManager.GetComponentData<BuildPhysicsWorldData>(state.World.GetExistingSystem<BuildPhysicsWorld>());
                var staticEntities = bpwData.StaticEntityGroup.ToEntityArray(Allocator.TempJob);
                for (int i = 0; i < staticEntities.Length; i++)
                {
                    var colliderComponent = state.EntityManager.GetComponentData<PhysicsCollider>(staticEntities[i]);
                    colliderComponent.Value.Value.SetCollisionFilter(new CollisionFilter
                    {
                        BelongsTo = ~CollisionFilter.Default.BelongsTo,
                        CollidesWith = ~CollisionFilter.Default.CollidesWith,
                        GroupIndex = 1
                    });
                    state.EntityManager.SetComponentData(staticEntities[i], colliderComponent);
                }

                var verificationData = m_VerificationGroup.ToEntityArray(Allocator.TempJob);
                var verificationComponentData = m_ActivationData[verificationData[0]];

                // 对接地 0 不执行任何操作（除了更换滤波器）
                int counter = 0;
                if (verificationComponentData.PureFilter > 0)
                {
                    counter++;
                }

                // 完全移除一处地面 (1)
                if (verificationComponentData.Remove > 0)
                {
                    state.EntityManager.DestroyEntity(staticEntities[counter]);
                    counter++;
                }

                // 将地面转换为动态物体 (2)
                if (verificationComponentData.MotionChange > 0)
                {
                    var colliderComponent = state.EntityManager.GetComponentData<PhysicsCollider>(staticEntities[counter]);
                    state.EntityManager.AddComponentData(staticEntities[counter], PhysicsMass.CreateDynamic(colliderComponent.MassProperties, 1.0f));
                    state.EntityManager.AddComponentData(staticEntities[counter], new PhysicsVelocity
                    {
                        Linear = new float3(0.0f, -1.0f, 0.0f),
                        Angular = float3.zero
                    });
                    counter++;
                }

                // 传送一地 (3)
                if (verificationComponentData.Teleport > 0)
                {

                    var localTransformComponent = state.EntityManager.GetComponentData<LocalTransform>(staticEntities[counter]);
                    localTransformComponent.Position.y = -10.0f;
                    state.EntityManager.SetComponentData(staticEntities[counter], localTransformComponent);

                    counter++;
                }

                // 改一地 collider (4)
                if (verificationComponentData.ColliderChange > 0)
                {
                    var colliderComponent = state.EntityManager.GetComponentData<PhysicsCollider>(staticEntities[counter]);
                    var oldFilter = colliderComponent.Value.Value.GetCollisionFilter();
                    colliderComponent.Value = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = new float3(5.0f, 1.0f, 50.0f) });
                    colliderComponent.Value.Value.SetCollisionFilter(oldFilter);
                    system.CreatedColliders.Add(colliderComponent.Value);
                    state.EntityManager.SetComponentData(staticEntities[counter], colliderComponent);
                    counter++;
                }

                // 一地新 collider (5)
                if (verificationComponentData.NewCollider > 0)
                {
                    var colliderComponent = state.EntityManager.GetComponentData<PhysicsCollider>(staticEntities[counter]);
                    var newColliderComponent = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = new float3(5.0f, 1.0f, 50.0f) });
                    system.CreatedColliders.Add(newColliderComponent);
                    newColliderComponent.Value.SetCollisionFilter(colliderComponent.Value.Value.GetCollisionFilter());
                    state.EntityManager.SetComponentData(staticEntities[counter], newColliderComponent.AsComponent());
                    counter++;
                }

                verificationData.Dispose();
                staticEntities.Dispose();
            }
            else if (m_Counter == 40)
            {
                // 验证地面改变后所有盒子都开始掉落
                var bpwData = state.EntityManager.GetComponentData<BuildPhysicsWorldData>(state.World.GetExistingSystem<BuildPhysicsWorld>());
                var dynamicEntities = bpwData.DynamicEntityGroup.ToEntityArray(Allocator.TempJob);
                for (int i = 0; i < dynamicEntities.Length; i++)
                {

                    var localTransform = state.EntityManager.GetComponentData<LocalTransform>(dynamicEntities[i]);
                    Assert.IsTrue(localTransform.Position.y < 0.99f, "Box didn't start falling!");

                }

                dynamicEntities.Dispose();
            }
        }
    }
}
