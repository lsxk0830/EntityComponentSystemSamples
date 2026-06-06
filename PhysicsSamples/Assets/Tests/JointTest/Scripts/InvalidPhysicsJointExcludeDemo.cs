using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

public class InvalidPhysicsJointExcludeDemoScene : SceneCreationSettings
{
    public float TimeToSwap = 0.5f;
}

public class InvalidPhysicsJointExcludeDemo : SceneCreationAuthoring<InvalidPhysicsJointExcludeDemoScene>
{
    public float TimeToSwap = 0.5f;

    class InvalidPhysicsJointExcludeDemoBaker : Baker<InvalidPhysicsJointExcludeDemo>
    {
        public override void Bake(InvalidPhysicsJointExcludeDemo authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new InvalidPhysicsJointExcludeDemoScene
            {
                DynamicMaterial = authoring.DynamicMaterial,
                StaticMaterial = authoring.StaticMaterial,
                TimeToSwap = authoring.TimeToSwap
            });
        }
    }
}

public struct InvalidPhysicsJointExcludeTimerEvent : IComponentData
{
    public float TimeLimit;
    internal float Timer;

    public void Reset() => Timer = TimeLimit;
    public void Tick(float deltaTime) => Timer -= deltaTime;
    public bool Fired(bool resetIfFired)
    {
        bool isFired = Timer < 0;
        if (isFired && resetIfFired) Reset();
        return isFired;
    }
}


public struct InvalidPhysicsJointExcludeBodies : IComponentData {}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct InvalidPhysicsJointExcludeDemoSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var timer in SystemAPI.Query<RefRW<InvalidPhysicsJointExcludeTimerEvent>>())
        {
            timer.ValueRW.Tick(deltaTime);
        }

        // 添加/删除 PhysicsExclude
        using (var commandBuffer = new EntityCommandBuffer(Allocator.TempJob))
        {
            foreach (var(timer, entity)
                     in SystemAPI.Query<RefRW<InvalidPhysicsJointExcludeTimerEvent>>().WithEntityAccess().WithAll<InvalidPhysicsJointExcludeBodies, PhysicsWorldIndex>())
            {
                if (timer.ValueRW.Fired(true))
                {
                    // 如果我们想支持多个 worlds，我们需要将 PhysicsWorldIndex.Value 存储在某个地方
                    commandBuffer.RemoveComponent<PhysicsWorldIndex>(entity);
                }
            }

            foreach (var(timer, entity)
                     in SystemAPI.Query<RefRW<InvalidPhysicsJointExcludeTimerEvent>>().WithEntityAccess().WithAll<InvalidPhysicsJointExcludeBodies>()
                         .WithNone<PhysicsWorldIndex>())
            {
                if (timer.ValueRW.Fired(true))
                {
                    commandBuffer.AddSharedComponent(entity, new PhysicsWorldIndex());
                }
            }

            commandBuffer.Playback(state.EntityManager);
        }
    }
}


public partial class InvalidPhyiscsJointExcludeDemoSceneCreationSystem : SceneCreationSystem<InvalidPhysicsJointExcludeDemoScene>
{
    public override void CreateScene(InvalidPhysicsJointExcludeDemoScene sceneSettings)
    {
        float colliderSize = 0.25f;

        BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
        {
            Center = float3.zero,
            Orientation = quaternion.identity,
            Size = new float3(colliderSize),
            BevelRadius = 0.0f
        });
        CreatedColliders.Add(collider);

        float timeToSwap = sceneSettings.TimeToSwap;

        var bodyAPos = new float3(2f, 5.0f, 2);
        var bodyBPos = new float3(2f, 6.0f, 2);

        // 添加约束动态/动态主体对，将排除其主体
        bool buildThisSection = true;
        if (buildThisSection)
        {
            bodyAPos += new float3(1, 0, 0);
            bodyBPos += new float3(1, 0, 0);

            // 创建一个身体
            Entity bodyA = CreateDynamicBody(bodyAPos, quaternion.identity, collider, float3.zero, float3.zero, 1.0f);
            Entity bodyB = CreateDynamicBody(bodyBPos, quaternion.identity, collider, float3.zero, float3.zero, 1.0f);

            for (int i = 0; i < 2; i++)
            {
                // 创建 joint
                var joint = PhysicsJoint.CreateBallAndSocket(new float3(0, colliderSize, 0), new float3(0, -colliderSize, 0));
                var jointEntity = CreateJoint(joint, bodyA, bodyB);

                var pair = EntityManager.GetComponentData<PhysicsConstrainedBodyPair>(jointEntity);
                pair.EnableCollision = 1;
                EntityManager.SetComponentData(jointEntity, pair);
            }

            // 添加排除 components。
            EntityManager.AddComponentData(bodyA, new InvalidPhysicsJointExcludeBodies());
            EntityManager.AddComponentData(bodyA, new InvalidPhysicsJointExcludeTimerEvent { TimeLimit = timeToSwap, Timer = timeToSwap });
            EntityManager.AddComponentData(bodyB, new InvalidPhysicsJointExcludeBodies());
            EntityManager.AddComponentData(bodyB, new InvalidPhysicsJointExcludeTimerEvent { TimeLimit = timeToSwap, Timer = timeToSwap });
        }
    }
}
