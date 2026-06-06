using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Unity.NetCode
{
    /// <summary>
    /// 使用立即模式运行物理步骤。当数量较多时，这通常比运行 jobs 快得多
    /// entities 比较小。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [BurstCompile]
    public partial struct ImmediatePhysicsStep : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var pw = SystemAPI.GetSingletonRW<PhysicsWorldSingleton>();
            state.CompleteDependency();
            var stepTime = SystemAPI.Time.DeltaTime;
            var simulation = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();
            if (!SystemAPI.TryGetSingleton<PhysicsStep>(out var physicsStep))
                physicsStep = PhysicsStep.Default;
            ref var buildPhysicData = ref state.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(
                state.WorldUnmanaged.GetExistingUnmanagedSystem<BuildPhysicsWorld>()).ValueRW;
            var simulationStepInput = new SimulationStepInput
            {
                World = pw.ValueRW.PhysicsWorld,
                TimeStep = stepTime,
                Gravity = physicsStep.Gravity,
                NumSolverIterations = physicsStep.SolverIterationCount,
                SynchronizeCollisionWorld = physicsStep.SynchronizeCollisionWorld != 0,
                SolverStabilizationHeuristicSettings = physicsStep.SolverStabilizationHeuristicSettings,
                HaveStaticBodiesChanged = buildPhysicData.PhysicsData.HaveStaticBodiesChanged
            };
            //从技术上讲，这也可以在 job 工作线程上执行。
            simulation.ResetSimulationContext(simulationStepInput);
            simulation.Step(simulationStepInput);
        }
    }
}
