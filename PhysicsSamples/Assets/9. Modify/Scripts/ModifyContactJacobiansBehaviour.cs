using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

public struct ModifyContactJacobians : IComponentData
{
    public enum ModificationType
    {
        None,
        SoftContact,
        SurfaceVelocity,
        InfiniteInertia,
        BiggerInertia,
        NoAngularEffects,
        DisabledContact,
        DisabledAngularFriction,
    }

    public ModificationType type;
}

[Serializable]
public class ModifyContactJacobiansBehaviour : MonoBehaviour
{
    public ModifyContactJacobians.ModificationType ModificationType;
}

class ModifyContactJacobiansBaker : Baker<ModifyContactJacobiansBehaviour>
{
    public override void Bake(ModifyContactJacobiansBehaviour authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new ModifyContactJacobians { type = authoring.ModificationType });
    }
}

[UpdateInGroup(typeof(PhysicsCreateJacobiansGroup), OrderFirst = true)]
public partial struct SetContactFlagsSystem : ISystem
{
    private ComponentLookup<ModifyContactJacobians> m_JacobianData;

    // 此 job 读取修改 component 并设置联系人上的一些数据，以传播到雅可比
    // 用于在我们的雅可比修改器 job 中进行处理。这是必要的，因为某些标志需要额外的数据
    // 与雅可比一起分配（e.g.、SurfaceVelocity 数据通常不存在）。我们还设置了
    // jacobianFlags 中的用户数据位使我们免于稍后查找 ComponentLookup。
    [BurstCompile]
    struct SetContactFlagsJob : IContactsJob
    {
        [ReadOnly]
        public ComponentLookup<ModifyContactJacobians> modificationData;

        public void Execute(ref ModifiableContactHeader manifold, ref ModifiableContactPoint contact)
        {
            Entity entityA = manifold.EntityA;
            Entity entityB = manifold.EntityB;

            ModifyContactJacobians.ModificationType typeA = ModifyContactJacobians.ModificationType.None;
            if (modificationData.HasComponent(entityA))
            {
                typeA = modificationData[entityA].type;
            }

            ModifyContactJacobians.ModificationType typeB = ModifyContactJacobians.ModificationType.None;
            if (modificationData.HasComponent(entityB))
            {
                typeB = modificationData[entityB].type;
            }

            if (ModifyContactJacobiansSystem.IsModificationType(ModifyContactJacobians.ModificationType.SurfaceVelocity, typeA, typeB))
            {
                manifold.JacobianFlags |= JacobianFlags.EnableSurfaceVelocity;
            }

            if (ModifyContactJacobiansSystem.IsModificationType(ModifyContactJacobians.ModificationType.InfiniteInertia, typeA, typeB) ||
                ModifyContactJacobiansSystem.IsModificationType(ModifyContactJacobians.ModificationType.BiggerInertia, typeA, typeB))
            {
                manifold.JacobianFlags |= JacobianFlags.EnableMassFactors;
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<ModifyContactJacobians>()));
        m_JacobianData = state.GetComponentLookup<ModifyContactJacobians>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_JacobianData.Update(ref state);
        var simulationSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;

        if (simulationSingleton.Type == SimulationType.NoPhysics)
        {
            return;
        }

        // Schedule jobs
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

        var job = new SetContactFlagsJob
        {
            modificationData = m_JacobianData
        };

        state.Dependency = job.Schedule(simulationSingleton, ref world, state.Dependency);
    }
}

// 一个 system，它配置模拟步骤以各种方式修改接触 jacobains
[UpdateInGroup(typeof(PhysicsSolveAndIntegrateGroup), OrderFirst = true)]
public partial struct ModifyContactJacobiansSystem : ISystem
{
    private ComponentLookup<ModifyContactJacobians> m_JacobianData;

    internal static bool IsModificationType(ModifyContactJacobians.ModificationType typeToCheck,
        ModifyContactJacobians.ModificationType typeOfA, ModifyContactJacobians.ModificationType typeOfB) => typeOfA == typeToCheck || typeOfB == typeToCheck;

    [BurstCompile]
    struct ModifyJacobiansJob : IJacobiansJob
    {
        [ReadOnly]
        public ComponentLookup<ModifyContactJacobians> modificationData;

        // 不要为触发器做任何事情
        public void Execute(ref ModifiableJacobianHeader h, ref ModifiableTriggerJacobian j) {}

        public void Execute(ref ModifiableJacobianHeader jacHeader, ref ModifiableContactJacobian contactJacobian)
        {
            Entity entityA = jacHeader.EntityA;
            Entity entityB = jacHeader.EntityB;

            ModifyContactJacobians.ModificationType typeA = ModifyContactJacobians.ModificationType.None;
            if (modificationData.HasComponent(entityA))
            {
                typeA = modificationData[entityA].type;
            }

            ModifyContactJacobians.ModificationType typeB = ModifyContactJacobians.ModificationType.None;
            if (modificationData.HasComponent(entityB))
            {
                typeB = modificationData[entityB].type;
            }

            {
                // 检查我们想要忽略的雅可比矩阵：
                if (IsModificationType(ModifyContactJacobians.ModificationType.DisabledContact, typeA, typeB))
                {
                    jacHeader.Flags = jacHeader.Flags | JacobianFlags.Disabled;
                }

                // 检查是否应通过雅可比禁用 NoTorque 修改器或摩擦力
                if (IsModificationType(ModifyContactJacobians.ModificationType.NoAngularEffects, typeA, typeB) ||
                    IsModificationType(ModifyContactJacobians.ModificationType.DisabledAngularFriction, typeA, typeB))
                {
                    // 禁用所有摩擦角度效果
                    var friction0 = contactJacobian.Friction0;
                    friction0.AngularA = 0.0f;
                    friction0.AngularB = 0.0f;
                    contactJacobian.Friction0 = friction0;

                    var friction1 = contactJacobian.Friction1;
                    friction1.AngularA = 0.0f;
                    friction1.AngularB = 0.0f;
                    contactJacobian.Friction1 = friction1;

                    var angularFriction = contactJacobian.AngularFriction;
                    angularFriction.AngularA = 0.0f;
                    angularFriction.AngularB = 0.0f;
                    contactJacobian.AngularFriction = angularFriction;
                }

                // 检查 SurfaceVelocity 是否存在
                if (jacHeader.HasSurfaceVelocity && IsModificationType(ModifyContactJacobians.ModificationType.SurfaceVelocity, typeA, typeB))
                {
                    // 由于表面法线可能会发生变化，因此请确保角速度始终与其相关，而不是独立的
                    jacHeader.SurfaceVelocity = new SurfaceVelocity
                    {
                        LinearVelocity = float3.zero,
                        AngularVelocity = contactJacobian.Normal * (new float3(0.0f, 1.0f, 0.0f))
                    };
                }

                // 检查 MassFactors 是否存在，我们应该使惯性无限大
                if (jacHeader.HasMassFactors && IsModificationType(ModifyContactJacobians.ModificationType.InfiniteInertia, typeA, typeB))
                {
                    // 给两个物体无限的惯性
                    jacHeader.MassFactors = new MassFactors
                    {
                        InverseInertiaFactorA = float3.zero,
                        InverseMassFactorA = 1.0f,
                        InverseInertiaFactorB = float3.zero,
                        InverseMassFactorB = 1.0f
                    };
                }

                // 检查 MassFactors 是否存在，我们应该将惯性增大 10 倍
                if (jacHeader.HasMassFactors && IsModificationType(ModifyContactJacobians.ModificationType.BiggerInertia, typeA, typeB))
                {
                    // 给两个物体 10 倍大的惯性
                    jacHeader.MassFactors = new MassFactors
                    {
                        InverseInertiaFactorA = new float3(0.1f),
                        InverseMassFactorA = 1.0f,
                        InverseInertiaFactorB = new float3(0.1f),
                        InverseMassFactorB = 1.0f
                    };
                }
            }

            // 角度雅可比修改
            for (int i = 0; i < contactJacobian.NumContacts; i++)
            {
                ContactJacAngAndVelToReachCp jacobianAngular = jacHeader.GetAngularJacobian(i);

                // 检查是否有 NoTorque 修饰符
                if (IsModificationType(ModifyContactJacobians.ModificationType.NoAngularEffects, typeA, typeB))
                {
                    // 禁用所有角度效果
                    jacobianAngular.Jac.AngularA = 0.0f;
                    jacobianAngular.Jac.AngularB = 0.0f;
                }

                // 检查是否有 SoftContact 修饰符
                if (IsModificationType(ModifyContactJacobians.ModificationType.SoftContact, typeA, typeB))
                {
                    jacobianAngular.Jac.EffectiveMass *= 0.1f;
                    if (jacobianAngular.VelToReachCp > 0.0f)
                    {
                        jacobianAngular.VelToReachCp *= 0.5f;
                    }
                }

                jacHeader.SetAngularJacobian(i, jacobianAngular);
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(state.GetEntityQuery(ComponentType.ReadOnly<ModifyContactJacobians>()));
        m_JacobianData = state.GetComponentLookup<ModifyContactJacobians>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_JacobianData.Update(ref state);
        var simulationSingleton = SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW;

        if (simulationSingleton.Type == SimulationType.NoPhysics)
        {
            return;
        }

        // Schedule jobs
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

        var job = new ModifyJacobiansJob
        {
            modificationData = m_JacobianData
        };

        state.Dependency = job.Schedule(simulationSingleton, ref world, state.Dependency);
    }
}
