using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;

//<todo.eoin.usermod 重命名为 ModifyOverlappingBodyPairsComponentData？
public struct ModifyBroadphasePairs : IComponentData {}

public class ModifyBroadphasePairsBehaviour : MonoBehaviour
{
}

class ModifyBroadphasePairsBehaviourBaker : Baker<ModifyBroadphasePairsBehaviour>
{
    public override void Bake(ModifyBroadphasePairsBehaviour authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new ModifyBroadphasePairs());
    }
}

// system，配置模拟步骤以禁用某些宽相对

[UpdateInGroup(typeof(PhysicsSimulationGroup))]
[UpdateAfter(typeof(PhysicsCreateBodyPairsGroup))]
[UpdateBefore(typeof(PhysicsCreateContactsGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct ModifyBroadphasePairsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<ModifyBroadphasePairs>()));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var simulation = SystemAPI.GetSingleton<SimulationSingleton>();

        if (simulation.Type == SimulationType.NoPhysics)
        {
            return;
        }

        var physicsWorld = SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld;

        var disablePairsJob = new DisablePairsJob
        {
            Bodies = physicsWorld.Bodies,
            Motions = physicsWorld.MotionVelocities
        };

        state.Dependency = disablePairsJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), ref physicsWorld, state.Dependency);
    }

    [BurstCompile]
    struct DisablePairsJob : IBodyPairsJob
    {
        [ReadOnly] public NativeArray<RigidBody> Bodies;
        [ReadOnly] public NativeArray<MotionVelocity> Motions;

        public unsafe void Execute(ref ModifiableBodyPair pair)
        {
            // 如果盒子与静态物体碰撞，则禁用该对
            int indexA = pair.BodyIndexA;
            int indexB = pair.BodyIndexB;
            if ((Bodies[indexA].Collider != null && Bodies[indexA].Collider.Value.Type == ColliderType.Box && indexB >= Motions.Length)
                || (Bodies[indexB].Collider != null && Bodies[indexB].Collider.Value.Type == ColliderType.Box && indexA >= Motions.Length))
            {
                pair.Disable();
            }
        }
    }
}
