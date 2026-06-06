using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Physics.Systems
{
    /// <summary>
    /// 用于立即在主线程上运行物理模拟的实用程序类。
    /// 不适合 ECS job 中的字段！
    /// </summary>
    public struct ImmediatePhysicsWorldStepper : IDisposable
    {
        // 模拟环境
        public SimulationContext SimulationContext;
#if HAVOK_PHYSICS_EXISTS
        public Havok.Physics.SimulationContext HavokSimulationContext;
#endif

        public bool Created;

        /// <summary>
        /// 创建方法。使用它代替构造函数。
        /// </summary>
        public static ImmediatePhysicsWorldStepper Create()
        {
            ImmediatePhysicsWorldStepper instance = new ImmediatePhysicsWorldStepper();
            instance.Created = true;
            instance.SimulationContext = new SimulationContext();

#if HAVOK_PHYSICS_EXISTS
            instance.HavokSimulationContext = new Havok.Physics.SimulationContext(Havok.Physics.HavokConfiguration.Default);
#endif

            return instance;
        }

#if HAVOK_PHYSICS_EXISTS
        /// <summary>
        /// 创建方法。提供传入 HavokConfiguration 的选项（例如，VDB 选项）。
        /// </summary>
        /// <param name="havokConfiguration"></param>
        public static ImmediatePhysicsWorldStepper Create(Havok.Physics.HavokConfiguration havokConfiguration)
        {
            ImmediatePhysicsWorldStepper instance = new ImmediatePhysicsWorldStepper();
            instance.Created = true;
            instance.SimulationContext = new SimulationContext();
            instance.HavokSimulationContext = new Havok.Physics.SimulationContext(havokConfiguration);
            return instance;
        }

#endif

        /// <summary>
        /// 步骤 UnityPhysics 模拟（在当前线程上）
        /// </summary>
        public static void StepUnityPhysicsSimulationImmediate(in SimulationStepInput stepInput, ref SimulationContext simulationContext)
        {
            Simulation.StepImmediate(stepInput, ref simulationContext);
        }

#if HAVOK_PHYSICS_EXISTS
        /// <summary>
        /// 步骤 HavokPhysics 模拟（在当前线程上）
        /// </summary>
        public static void StepHavokPhysicsSimulationImmediate(in SimulationStepInput stepInput, ref Havok.Physics.SimulationContext havokSimulationContext)
        {
            Havok.Physics.HavokSimulation.StepImmediate(stepInput, ref havokSimulationContext);
        }

#endif

        public void Dispose()
        {
            SimulationContext.Dispose();
#if HAVOK_PHYSICS_EXISTS
            HavokSimulationContext.Dispose();
#endif
            Created = false;
        }

        /// <summary>
        /// 准备模拟上下文并逐步执行模拟（在当前线程上）
        /// </summary>
        public void StepImmediate(SimulationType simType, ref PhysicsWorld physicsWorld, in SimulationStepInput stepInput)
        {
            if (simType == SimulationType.UnityPhysics)
            {
                SimulationContext.Reset(stepInput);
                Simulation.StepImmediate(stepInput, ref SimulationContext);
            }
#if HAVOK_PHYSICS_EXISTS
            else
            {
                HavokSimulationContext.Reset(ref physicsWorld);
                Havok.Physics.HavokSimulation.StepImmediate(stepInput, ref HavokSimulationContext);
            }
#endif
        }
    }
}
