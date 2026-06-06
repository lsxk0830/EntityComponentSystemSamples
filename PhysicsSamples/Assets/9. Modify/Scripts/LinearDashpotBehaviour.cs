using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;

/*
 * 问题：
 *  - 如果不使用 GameObjects，则设置约束
 *  - 为 Component 和直接数据操作提供实用功能
 *  - 将多个相同类型的 Components 分配给单个 Entity
 */

public struct LinearDashpot : IComponentData
{
    public Entity localEntity;
    public float3 localOffset;
    public Entity parentEntity;
    public float3 parentOffset;

    public int dontApplyImpulseToParent;
    public float strength;
    public float damping;
}

public class LinearDashpotBehaviour : MonoBehaviour
{
    public PhysicsBodyAuthoring parentBody;
    public float3 parentOffset;
    public float3 localOffset;

    public bool dontApplyImpulseToParent = false;
    public float strength;
    public float damping;

    void OnEnable() {}
}

class LinearDashpotBaker : Baker<LinearDashpotBehaviour>
{
    public override void Bake(LinearDashpotBehaviour authoring)
    {
        if (authoring.enabled)
        {
            // Note: GetPrimaryEntity 当前创建了一个新的 Entity
            //       如果 parentBody 不是 scene 层次结构中的子级
            var componentData = new LinearDashpot
            {
                localEntity = GetEntity(TransformUsageFlags.Dynamic),
                localOffset = authoring.localOffset,
                parentEntity = authoring.parentBody == null ? Entity.Null : GetEntity(authoring.parentBody, TransformUsageFlags.Dynamic),
                parentOffset = authoring.parentOffset,
                dontApplyImpulseToParent = authoring.dontApplyImpulseToParent ? 1 : 0,
                strength = authoring.strength,
                damping = authoring.damping
            };

            Entity dashpotEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
            AddComponent(dashpotEntity, componentData);
        }
    }
}

#region System
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct LinearDashpotSystem : ISystem
{
    public EntityQuery LinearDashpotQuery;
    public ComponentHandles Handles;

    public struct ComponentHandles
    {
        public ComponentLookup<PhysicsVelocity> Velocities;

        public ComponentLookup<LocalTransform> LocalTransforms;

        public ComponentLookup<PhysicsMass> Masses;
        public ComponentLookup<LocalToWorld> LocalToWorlds;
        public ComponentTypeHandle<LinearDashpot> LinearDashpotHandle;

        public ComponentHandles(ref SystemState state)
        {
            Velocities = state.GetComponentLookup<PhysicsVelocity>(false);

            LocalTransforms = state.GetComponentLookup<LocalTransform>(true);

            Masses = state.GetComponentLookup<PhysicsMass>(true);
            LocalToWorlds = state.GetComponentLookup<LocalToWorld>(true);
            LinearDashpotHandle = state.GetComponentTypeHandle<LinearDashpot>(true);
        }

        public void Update(ref SystemState state)
        {
            Velocities.Update(ref state);

            LocalTransforms.Update(ref state);

            Masses.Update(ref state);
            LocalToWorlds.Update(ref state);
            LinearDashpotHandle.Update(ref state);
        }
    }


    [BurstCompile]
    struct LinearDashpotJob : IJobChunk
    {
        public ComponentLookup<PhysicsVelocity> Velocities;

        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransforms;

        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorlds;
        [ReadOnly] public ComponentTypeHandle<LinearDashpot> LinearDashpotHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<LinearDashpot> chunkLinearDashpots = chunk.GetNativeArray(ref LinearDashpotHandle);

            bool hasChunkLinearDashpotType = chunk.Has(ref LinearDashpotHandle);

            if (!hasChunkLinearDashpotType)
            {
                // 永远不应该发生
                return;
            }

            int instanceCount = chunk.Count;

            ChunkEntityEnumerator enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, instanceCount);

            while (enumerator.NextEntityIndex(out var i))
            {
                var linearDashpot = chunkLinearDashpots[i];

                if (linearDashpot.strength == 0)
                {
                    continue;
                }

                var eA = linearDashpot.localEntity;
                var eB = linearDashpot.parentEntity;

                var eAIsNull = eA == Entity.Null;
                if (eAIsNull)
                {
                    continue;
                }

                var eBIsNull = eB == Entity.Null;

                var hasVelocityA = !eAIsNull && Velocities.HasComponent(eA);
                var hasVelocityB = !eBIsNull && Velocities.HasComponent(eB);

                if (!hasVelocityA)
                {
                    return;
                }


                LocalTransform localTransformA = LocalTransform.Identity;

                PhysicsVelocity velocityA = default;
                PhysicsMass massA = default;


                LocalTransform localTransformB = localTransformA;

                PhysicsVelocity velocityB = velocityA;
                PhysicsMass massB = massA;


                if (LocalTransforms.HasComponent(eA)) localTransformA = LocalTransforms[eA];

                if (Velocities.HasComponent(eA)) velocityA = Velocities[eA];
                if (Masses.HasComponent(eA)) massA = Masses[eA];

                if (LocalToWorlds.HasComponent(eB))
                {
                    // 父级可以是静态的并且没有平移或旋转
                    var worldFromBody = Math.DecomposeRigidBodyTransform(LocalToWorlds[eB].Value);

                    localTransformB.Position = worldFromBody.pos;
                    localTransformB.Rotation = worldFromBody.rot;
                }

                if (LocalTransforms.HasComponent(eB)) localTransformB = LocalTransforms[eB];

                if (Velocities.HasComponent(eB)) velocityB = Velocities[eB];
                if (Masses.HasComponent(eB)) massB = Masses[eB];


                var posA = math.transform(new RigidTransform(localTransformA.Rotation, localTransformA.Position), linearDashpot.localOffset);
                var posB = math.transform(new RigidTransform(localTransformB.Rotation, localTransformB.Position), linearDashpot.parentOffset);
                var lvA = velocityA.GetLinearVelocity(massA, localTransformA.Position, localTransformA.Rotation, posA);
                var lvB = velocityB.GetLinearVelocity(massB, localTransformB.Position, localTransformB.Rotation, posB);


                var impulse = linearDashpot.strength * (posB - posA) + linearDashpot.damping * (lvB - lvA);
                impulse = math.clamp(impulse, new float3(-100.0f), new float3(100.0f));


                velocityA.ApplyImpulse(massA, localTransformA.Position, localTransformA.Rotation, impulse, posA);

                Velocities[eA] = velocityA;

                if (0 == linearDashpot.dontApplyImpulseToParent && hasVelocityB)
                {
                    velocityB.ApplyImpulse(massB, localTransformB.Position, localTransformB.Rotation, -impulse, posB);

                    Velocities[eB] = velocityB;
                }
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Handles = new ComponentHandles(ref state);

        LinearDashpotQuery = state.GetEntityQuery(ComponentType.ReadOnly<LinearDashpot>());
        state.RequireForUpdate(LinearDashpotQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Handles.Update(ref state);

        state.Dependency = new LinearDashpotJob
        {
            Velocities = Handles.Velocities,

            LocalTransforms = Handles.LocalTransforms,

            Masses = Handles.Masses,
            LocalToWorlds = Handles.LocalToWorlds,
            LinearDashpotHandle = Handles.LinearDashpotHandle
        }.Schedule(LinearDashpotQuery, state.Dependency);
    }
}
#endregion
