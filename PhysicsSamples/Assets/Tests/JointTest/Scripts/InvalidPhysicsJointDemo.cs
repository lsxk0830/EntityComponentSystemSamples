using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public class InvalidPhysicsJointDemoScene : SceneCreationSettings {}

public class InvalidPhysicsJointDemo : SceneCreationAuthoring<InvalidPhysicsJointDemoScene>
{
    class InvalidPhysicsJointDemoBaker : Baker<InvalidPhysicsJointDemo>
    {
        public override void Bake(InvalidPhysicsJointDemo authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new InvalidPhysicsJointDemoScene
            {
                DynamicMaterial = authoring.DynamicMaterial,
                StaticMaterial = authoring.StaticMaterial
            });
        }
    }
}

public partial class InvalidPhyiscsJointDemoSystem : SceneCreationSystem<InvalidPhysicsJointDemoScene>
{
    public override void CreateScene(InvalidPhysicsJointDemoScene sceneSettings)
    {
        float colliderSize = 0.25f;

        BlobAssetReference<Collider> collider = BoxCollider.Create(new BoxGeometry
        {
            Center = float3.zero,
            Orientation = quaternion.identity,
            Size = new float3(colliderSize),
            BevelRadius = 0.0f
        });
        CreatedColliders.Add(collider);

        // 添加动态体约束到会死的 world
        // 一旦动体被破坏 joint 将失效
        {
            // 创造动态的身体
            float3 pivotWorld = new float3(-2f, 0, 0);
            Entity body = CreateDynamicBody(pivotWorld, quaternion.identity, collider, float3.zero, float3.zero, 1.0f);

            // 在第一个被销毁后，为 trigger Havok 同步创建额外的动态主体
            CreateDynamicBody(pivotWorld * 2.0f, quaternion.identity, collider, float3.zero, float3.zero, 1.0f);

            // 在 15 帧后为动态主体添加超时。
            EntityManager.AddComponentData(body, new LifeTime { Value = 15 });

            // 创建 joint
            float3 pivotLocal = float3.zero;
            var joint = PhysicsJoint.CreateBallAndSocket(pivotLocal, pivotWorld);
            var jointEntity = CreateJoint(joint, body, Entity.Null);

            // 在 30 帧后在 joint entity 上添加超时。
            EntityManager.AddComponentData(jointEntity, new LifeTime { Value = 30 });
        }

        // 添加两个约束在一起的静态实体
        // joint 立即无效
        {
            // 创建一个身体
            Entity bodyA = CreateStaticBody(new float3(0, 0.0f, 0), quaternion.identity, collider);
            Entity bodyB = CreateStaticBody(new float3(0, 1.0f, 0), quaternion.identity, collider);

            // 创建 joint
            float3 pivotLocal = float3.zero;
            var joint = PhysicsJoint.CreateBallAndSocket(pivotLocal, pivotLocal);
            var jointEntity = CreateJoint(joint, bodyA, bodyB);

            // 在 15 帧后在 joint entity 上添加超时。
            EntityManager.AddComponentData(jointEntity, new LifeTime { Value = 15 });
        }

        // 添加两个以 0 维度约束在一起的动态实体
        {
            // 创建一个身体
            Entity bodyA = CreateDynamicBody(new float3(0, 5.0f, 0), quaternion.identity, collider, float3.zero, float3.zero, 1.0f);
            Entity bodyB = CreateDynamicBody(new float3(0, 6.0f, 0), quaternion.identity, collider, float3.zero, float3.zero, 1.0f);

            // 创建 joint
            var joint = PhysicsJoint.CreateLimitedDOF(RigidTransform.identity, new bool3(false), new bool3(false));
            CreateJoint(joint, bodyA, bodyB);
        }
    }
}
