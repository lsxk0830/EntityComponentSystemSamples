using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using UnityEngine;

public struct ModifyNarrowphaseContacts : IComponentData
{
    public Entity surfaceEntity;
    public float3 surfaceNormal;
}

[RequireComponent(typeof(PhysicsBodyAuthoring))]
[DisallowMultipleComponent]
public class ModifyNarrowphaseContactsBehaviour : MonoBehaviour
{
    // SurfaceUpNormal 用于非网格表面。
    // 对于网格表面，我们从单个多边形中获取法线
    public Vector3 SurfaceUpNormal = Vector3.up;

    void OnEnable() {}
}

class ModifyNarrowphaseContactsBehaviourBaker : Baker<ModifyNarrowphaseContactsBehaviour>
{
    public override void Bake(ModifyNarrowphaseContactsBehaviour authoring)
    {
        if (authoring.enabled)
        {
            var transform = GetComponent<Transform>();
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ModifyNarrowphaseContacts
            {
                surfaceEntity = GetEntity(TransformUsageFlags.Dynamic),
                surfaceNormal = transform.rotation * authoring.SurfaceUpNormal
            });
        }
    }
}

// system，配置模拟步骤以旋转某些接触法线
[UpdateInGroup(typeof(PhysicsSimulationGroup))]
[UpdateAfter(typeof(PhysicsCreateContactsGroup)), UpdateBefore(typeof(PhysicsCreateJacobiansGroup))]
public partial struct ModifyNarrowphaseContactsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<ModifyNarrowphaseContacts>()));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var simulationSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;

        if (simulationSingleton.Type == SimulationType.NoPhysics)
        {
            return;
        }

        var modifier = SystemAPI.GetSingleton<ModifyNarrowphaseContacts>();

        var surfaceNormal = modifier.surfaceNormal;
        var surfaceEntity = modifier.surfaceEntity;

        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

        var job = new ModifyNormalsJob
        {
            SurfaceEntity = surfaceEntity,
            SurfaceNormal = surfaceNormal,
            CollisionWorld = world.CollisionWorld
        };

        state.Dependency = job.Schedule(simulationSingleton, ref world, state.Dependency);
    }

    [BurstCompile]
    struct ModifyNormalsJob : IContactsJob
    {
        public Entity SurfaceEntity;
        public float3 SurfaceNormal;
        [ReadOnly] public CollisionWorld CollisionWorld;
        float distanceScale;

        public void Execute(ref ModifiableContactHeader contactHeader, ref ModifiableContactPoint contactPoint)
        {
            bool isBodyA = (contactHeader.EntityA == SurfaceEntity);
            bool isBodyB = (contactHeader.EntityB == SurfaceEntity);
            if (isBodyA || isBodyB)
            {
                if (contactPoint.Index == 0)
                {
                    // 如果我们有一个网格表面，我们可以从多边形平面获得表面法线
                    var rbIdx = CollisionWorld.GetRigidBodyIndex(SurfaceEntity);
                    var body = CollisionWorld.Bodies[rbIdx];
                    if (body.Collider.Value.CollisionType == CollisionType.Composite)
                    {
                        unsafe
                        {
                            body.Collider.Value.GetLeaf(isBodyA ? contactHeader.ColliderKeyA : contactHeader.ColliderKeyB, out ChildCollider leafCollider);
                            if (leafCollider.Collider->Type == ColliderType.Triangle || leafCollider.Collider->Type == ColliderType.Quad)
                            {
                                PolygonCollider* polygonCollider = (PolygonCollider*)leafCollider.Collider;
                                // 潜在优化：如果 TransformFromChild 没有旋转，只需使用 body.WorldFromBody.rot
                                // 如果您只有一个没有层次结构的 MeshCollider，则可能会出现这种情况。
                                quaternion rotation = math.mul(body.WorldFromBody.rot, leafCollider.TransformFromChild.rot);
                                float3 surfaceNormal = math.rotate(rotation, polygonCollider->Planes[0].Normal);
                                distanceScale = math.dot(surfaceNormal, contactHeader.Normal);
                                contactHeader.Normal = surfaceNormal;
                            }
                        }
                    }
                }
                contactPoint.Distance *= distanceScale;
            }
        }
    }
}
