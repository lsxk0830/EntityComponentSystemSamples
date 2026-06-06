using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Systems;

namespace Unity.NetCode
{
    //我们希望这个 system 不包含 run 到任何自定义物理 world 中。所以我们不直接瞄准 BeforePhysicsSytemGroup
    //如果重建或更新物理 world，则此 system 负责检测 CustomBuildPhysicsWorld
    //基于 PhysicsLoopConfig 设置和当前模拟报价。
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct ConfigureBuildPhysicsWorld : ISystem
    {
        private SystemHandle physicsBuildWorld;
        private NetworkTick lastFullBuildTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            physicsBuildWorld = state.WorldUnmanaged.GetExistingUnmanagedSystem<CustomBuildPhysicsWorld>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            SystemAPI.TryGetSingleton<PhysicsLoopConfig>(out var loopConfig);
            var settings = state.EntityManager.GetComponentDataRW<BuildPhysicsWorldSettings>(physicsBuildWorld);
            settings.ValueRW.UseImmediateMode = loopConfig.UseImmediateMode;
            settings.ValueRW.StepImmediateMode = loopConfig.StepImmediateMode;
            //第一个 prediction 滴答声我们总是重建物理 world 无论如何
            //从干净的状态开始。
            //我们对最终的完整刻度和部分刻度也执行相同的操作，因此最后一步也使用
            //良好的起点。
            //如果物理滴答率 > 模拟滴答率，则应仅在第一个物理步骤中完成构建
            if (networkTime.IsFirstPredictionTick || networkTime.IsFinalFullPredictionTick)
            {
                if (!lastFullBuildTick.IsValid || lastFullBuildTick != networkTime.ServerTick)
                {
                    settings.ValueRW.UpdateBroadphaseAndMotion = 0;
                    lastFullBuildTick = networkTime.ServerTick;
                }
                else
                {
                    settings.ValueRW.UpdateBroadphaseAndMotion = 1;
                }
            }
            else
            {
                settings.ValueRW.UpdateBroadphaseAndMotion = 1;
            }
        }
    }

    /// <summary>
    /// System 启用/禁用哪些物理 system 需要针对给定的 predicted 更新。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedFixedStepSimulationSystemGroup))]
    partial class EnableDisablePhysicsSystems : SystemBase
    {
        private SystemHandle BuildPhysicWorldHandle;
        private SystemHandle SyncCustomPhysicsProxySystemHandle;
        private SystemHandle BufferInterpolatedRigidBodiesMotionHandle;
        private SystemHandle CopyPhysicsVelocityToSmoothingHandle;
        private ComponentSystemGroup PhysicsCreateBodyPairsGroup;
        private ComponentSystemGroup PhysicsCreateContactsGroup;
        private ComponentSystemGroup PhysicsCreateJacobiansGroup;
        private ComponentSystemGroup PhysicsSolveAndIntegrateGroup;
        //自定义物理 systems。
        private SystemHandle CustomBuildPhysicsWorldHandle;
        private SystemHandle ImmediatePhysicsStepHandle;

        protected override void OnCreate()
        {
            BuildPhysicWorldHandle = World.Unmanaged.GetExistingUnmanagedSystem<BuildPhysicsWorld>();
            SyncCustomPhysicsProxySystemHandle = World.Unmanaged.GetExistingUnmanagedSystem<SyncCustomPhysicsProxySystem>();
            BufferInterpolatedRigidBodiesMotionHandle = World.Unmanaged.GetExistingUnmanagedSystem<BufferInterpolatedRigidBodiesMotion>();
            CopyPhysicsVelocityToSmoothingHandle = World.Unmanaged.GetExistingUnmanagedSystem<CopyPhysicsVelocityToSmoothing>();
            PhysicsCreateBodyPairsGroup = World.GetExistingSystemManaged<PhysicsCreateBodyPairsGroup>();
            PhysicsCreateContactsGroup = World.GetExistingSystemManaged<PhysicsCreateContactsGroup>();
            PhysicsCreateJacobiansGroup = World.GetExistingSystemManaged<PhysicsCreateJacobiansGroup>();
            PhysicsSolveAndIntegrateGroup = World.GetExistingSystemManaged<PhysicsSolveAndIntegrateGroup>();
            CustomBuildPhysicsWorldHandle = World.Unmanaged.GetExistingUnmanagedSystem<CustomBuildPhysicsWorld>();
            ImmediatePhysicsStepHandle = World.Unmanaged.GetExistingUnmanagedSystem<ImmediatePhysicsStep>();
            RequireForUpdate<PhysicsLoopConfig>();
        }

        protected override void OnUpdate()
        {
            var loopConfig = SystemAPI.GetSingleton<PhysicsLoopConfig>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            //每次 prediction 更新时设置一次，因为 loopConfig 在 prediction 步骤之间不会更改。
            if (networkTime.IsFirstPredictionTick)
            {
                World.Unmanaged.ResolveSystemStateRef(CustomBuildPhysicsWorldHandle).Enabled = true;
                World.Unmanaged.ResolveSystemStateRef(ImmediatePhysicsStepHandle).Enabled = loopConfig.StepImmediateMode != 0;
                //禁用正常构建物理 systems。CustomBuildPhysicsWorldHandle 将处理一切
                World.Unmanaged.ResolveSystemStateRef(BuildPhysicWorldHandle).Enabled = false;
                //在立即模式下，我们不希望任何 systems 运行，因为该步骤已经执行了所有这些。
                PhysicsCreateBodyPairsGroup.Enabled = loopConfig.StepImmediateMode == 0;
                PhysicsCreateContactsGroup.Enabled = loopConfig.StepImmediateMode == 0;
                PhysicsCreateJacobiansGroup.Enabled = loopConfig.StepImmediateMode == 0;
                PhysicsSolveAndIntegrateGroup.Enabled = loopConfig.StepImmediateMode == 0;
            }
            //我们仅为最终的 prediction 启用同步代理、interpolation 和物理速度平滑
            //打钩。成本通常很少，但免费运行它们没有多大意义
            var isFinalTick = networkTime.IsFinalPredictionTick || networkTime.IsFinalFullPredictionTick;
            World.Unmanaged.ResolveSystemStateRef(SyncCustomPhysicsProxySystemHandle).Enabled = isFinalTick;
            World.Unmanaged.ResolveSystemStateRef(BufferInterpolatedRigidBodiesMotionHandle).Enabled = isFinalTick;
            World.Unmanaged.ResolveSystemStateRef(CopyPhysicsVelocityToSmoothingHandle).Enabled = isFinalTick;
        }
    }

    /// <summary>
    /// System 在 client 上运行，并在 FixedStepSimulationSystemGroup 之前禁用自定义物理构建 world。
    /// 这将使任何物理 world 模拟（i.e client0only 物理）正常执行。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial class DisableCustomPhysicsSystems : SystemBase
    {
        private SystemHandle BuildPhysicWorldHandle;
        private SystemHandle CustomBuildPhysicsWorldHandle;
        private SystemHandle SyncCustomPhysicsProxySystemHandle;
        private SystemHandle BufferInterpolatedRigidBodiesMotionHandle;
        private SystemHandle CopyPhysicsVelocityToSmoothingHandle;
        private ComponentSystemGroup PhysicsCreateBodyPairsGroup;
        private ComponentSystemGroup PhysicsCreateContactsGroup;
        private ComponentSystemGroup PhysicsCreateJacobiansGroup;
        private ComponentSystemGroup PhysicsSolveAndIntegrateGroup;
        private SystemHandle ImmediatePhysicsStepHandle;

        protected override void OnCreate()
        {
            BuildPhysicWorldHandle = World.Unmanaged.GetExistingUnmanagedSystem<BuildPhysicsWorld>();
            CustomBuildPhysicsWorldHandle = World.Unmanaged.GetExistingUnmanagedSystem<CustomBuildPhysicsWorld>();
            SyncCustomPhysicsProxySystemHandle = World.Unmanaged.GetExistingUnmanagedSystem<SyncCustomPhysicsProxySystem>();
            BufferInterpolatedRigidBodiesMotionHandle = World.Unmanaged.GetExistingUnmanagedSystem<BufferInterpolatedRigidBodiesMotion>();
            CopyPhysicsVelocityToSmoothingHandle = World.Unmanaged.GetExistingUnmanagedSystem<CopyPhysicsVelocityToSmoothing>();
            ImmediatePhysicsStepHandle = World.Unmanaged.GetExistingUnmanagedSystem<ImmediatePhysicsStep>();
            PhysicsCreateBodyPairsGroup = World.GetExistingSystemManaged<PhysicsCreateBodyPairsGroup>();
            PhysicsCreateContactsGroup = World.GetExistingSystemManaged<PhysicsCreateContactsGroup>();
            PhysicsCreateJacobiansGroup = World.GetExistingSystemManaged<PhysicsCreateJacobiansGroup>();
            PhysicsSolveAndIntegrateGroup = World.GetExistingSystemManaged<PhysicsSolveAndIntegrateGroup>();
        }

        protected override void OnUpdate()
        {
            PhysicsCreateBodyPairsGroup.Enabled  = true;
            PhysicsCreateContactsGroup.Enabled  = true;
            PhysicsCreateJacobiansGroup.Enabled  = true;
            PhysicsSolveAndIntegrateGroup.Enabled  = true;
            World.Unmanaged.ResolveSystemStateRef(BuildPhysicWorldHandle).Enabled = true;
            World.Unmanaged.ResolveSystemStateRef(SyncCustomPhysicsProxySystemHandle).Enabled = true;
            World.Unmanaged.ResolveSystemStateRef(BufferInterpolatedRigidBodiesMotionHandle).Enabled = true;
            World.Unmanaged.ResolveSystemStateRef(CopyPhysicsVelocityToSmoothingHandle).Enabled = true;
            World.Unmanaged.ResolveSystemStateRef(ImmediatePhysicsStepHandle).Enabled = false;
            World.Unmanaged.ResolveSystemStateRef(CustomBuildPhysicsWorldHandle).Enabled = false;
        }
    }
}
