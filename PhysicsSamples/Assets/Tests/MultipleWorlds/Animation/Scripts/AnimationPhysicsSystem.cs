using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;
using static Unity.Physics.Systems.PhysicsWorldExporter;

namespace Unity.Physics.Tests
{
    // system 在动画物体上执行整个物理管道
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(DriveAnimationBodySystem))]
    public partial struct AnimationPhysicsSystem : ISystem, ISystemStartStop
    {
        private PhysicsWorldData PhysicsData;
        private PhysicsWorldIndex WorldFilter;
        private ImmediatePhysicsWorldStepper m_Stepper;
        private ExportPhysicsWorldTypeHandles m_ExportPhysicsWorldTypeHandles;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DriveAnimationBodyData>();
        }

        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
            WorldFilter = new PhysicsWorldIndex(1);
            PhysicsData = new PhysicsWorldData(ref state, WorldFilter);
            m_ExportPhysicsWorldTypeHandles = new ExportPhysicsWorldTypeHandles(ref state);
            m_Stepper = ImmediatePhysicsWorldStepper.Create();
        }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            if (m_Stepper.Created == true)
            {
                m_Stepper.Dispose();
            }

            PhysicsData.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 确保依赖关系完整，我们将立即 run 一切
            state.CompleteDependency();

            float timeStep = SystemAPI.Time.DeltaTime;

            // 如果您想要对此 world 进行不同的模拟，请进行调整
            PhysicsStep stepComponent = PhysicsStep.Default;
            if (SystemAPI.HasSingleton<PhysicsStep>())
            {
                stepComponent = SystemAPI.GetSingleton<PhysicsStep>();
            }

            // 立即构建 PhysicsWorld
            PhysicsWorldBuilder.BuildPhysicsWorldImmediate(ref state, ref PhysicsData, timeStep, stepComponent.Gravity, state.LastSystemVersion);

            // 如果 world 是静态的，请尽早退出
            if (PhysicsData.PhysicsWorld.NumDynamicBodies == 0) return;

            // 在主线程上运行模拟
            m_Stepper.StepImmediate(stepComponent.SimulationType, ref PhysicsData.PhysicsWorld,
                new SimulationStepInput()
                {
                    World = PhysicsData.PhysicsWorld,
                    TimeStep = timeStep,
                    Gravity = stepComponent.Gravity,
                    SynchronizeCollisionWorld = false,
                    NumSubsteps = stepComponent.SubstepCount,
                    NumSolverIterations = stepComponent.SolverIterationCount,
                    SolverStabilizationHeuristicSettings = Solver.StabilizationHeuristicSettings.Default,
                    HaveStaticBodiesChanged = PhysicsData.HaveStaticBodiesChanged
                });

            // 仅导出物理 world（不要复制 CollisionWorld）
            ExportPhysicsWorldImmediate(ref state, ref m_ExportPhysicsWorldTypeHandles, in PhysicsData.PhysicsWorld, PhysicsData.DynamicEntityGroup);
        }
    }
}
