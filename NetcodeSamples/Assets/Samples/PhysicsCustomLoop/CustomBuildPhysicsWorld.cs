using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Unity.NetCode
{
    /// <summary>
    /// system，它基于 entity world 构建物理 world。world 将包含一个
    /// 每个 entity 都有一个刚体，它有一个刚体 component，每个 entity 都有一个 joint
    /// 其中有 joint component。
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsBuildWorldGroup))]
    [CreateAfter(typeof(BuildPhysicsWorld))]
    public partial struct CustomBuildPhysicsWorld : ISystem
    {
        private SystemHandle buildPhysicSystemHandle;
        private int currentPhysicStep;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            buildPhysicSystemHandle = state.WorldUnmanaged.GetExistingUnmanagedSystem<BuildPhysicsWorld>();
            //为了设置正确的依赖关系，有必要在此处创建临时 PhysicsWorldData。
            //然后始终从 BuildPhysicsWorld 检索 PhysicsWorldData 本身。
            var physicsData = new PhysicsWorldData(ref state, new PhysicsWorldIndex());
            physicsData.Dispose();
            state.EntityManager.AddComponentData(state.SystemHandle, new BuildPhysicsWorldSettings());
            //系统始终启动禁用，并将根据 PhysicsCustomLoop component 启用/禁用
            //由 ConfigureBuildPhysicsWorld 设置。
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //首先，我们需要在构建或更新之前完成所有待处理的依赖项
            //物理 world。特别是 BuildPhysicsWorldData 可能有一些物理特性 jobs 需要
            //已完成，但无法自动跟踪其依赖关系。他们被内部跟踪
            // InputDepdency 已处理，并且可以使用公开的 CompleteInputDependency 方法在这里等待它们。
            ref var buildPhysicsData = ref state.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(buildPhysicSystemHandle).ValueRW;
            buildPhysicsData.CompleteInputDependency();

            float timeStep = SystemAPI.Time.DeltaTime;
            if (!SystemAPI.TryGetSingleton(out PhysicsStep stepComponent))
                stepComponent = PhysicsStep.Default;

            //BuildPhysicsWorldSettings 控制构建 system 如何构造物理 world。尤其，
            //完全重建或“增量”重建，仅通过扩大 AABB 来更新当前的 brodphase
            //通过使用最后计算的物理步骤 PhysicsVelocity 和重力。
            //请注意，在 prediction 的情况下，PhysicsVelocity 实际上可能是 server 的复制值
            //而不是最后一个 predicted 值，被用作输入-输出。
            var settings = state.EntityManager.GetComponentData<BuildPhysicsWorldSettings>(state.SystemHandle);

            // 如果我们只想更新宽相和运动数据，我们检查是否有新的
            // 物理对象已被创建或销毁（静态、动态或 joint），并且我们强制完全重建
            // 在那种情况下。
            if (settings.UpdateBroadphaseAndMotion == 1)
            {
                var dynamicObject = buildPhysicsData.PhysicsData.DynamicEntityGroup.CalculateEntityCount();
                //+1 表示默认静态主体对象
                var staticObject = buildPhysicsData.PhysicsData.StaticEntityGroup.CalculateEntityCount() + 1;
                var joints = buildPhysicsData.PhysicsData.JointEntityGroup.CalculateEntityCount();
                //如果 entity 计数发生变化，我们将强制重建（并记录此信息）
                if (buildPhysicsData.PhysicsData.PhysicsWorld.NumDynamicBodies != dynamicObject ||
                    buildPhysicsData.PhysicsData.PhysicsWorld.NumStaticBodies != staticObject ||
                    buildPhysicsData.PhysicsData.PhysicsWorld.NumJoints != joints)
                {
                    settings.UpdateBroadphaseAndMotion = 0;
                }
            }

            // 以下代码执行完全重建 (BuildPhysicsWorld)
            // 或者对 Motion 和 Broaphase 进行部分更新。
            // 不执行运动和布阶段的更新
            // 用同样的方法（i.e ScheduleUpdateBroadphase）因为这样还有一点多
            // 灵活地检查我们是否确实需要更新以及更新的内容。从技术上来说，只有模拟物理 entities 才能有 LocalTransform，
            // 和 PhysicsVelocity 导出，意味着“类似运动学”的运动数据一般不会改变，并且
            // 可能不需要更新。

            //当物理 entities 的数量较小时，立即模式可能是一个不错的选择。
            //在此示例中，这是在主线程上执行的，但也可以执行
            //在 job 中。
            if (settings.UseImmediateMode != 0)
            {
                state.CompleteDependency();
                if (settings.UpdateBroadphaseAndMotion == 0)
                {
                    PhysicsWorldBuilder.BuildPhysicsWorldImmediate(ref state, ref buildPhysicsData.PhysicsData,
                        timeStep, stepComponent.Gravity, state.LastSystemVersion);
                }
                else
                {
                    buildPhysicsData.PhysicsData.Update(ref state);
                    PhysicsWorldBuilder.UpdateMotionDataImmediate(ref state, ref buildPhysicsData.PhysicsData);
                    //此方法内部检查是否有必要更新静态树
                    PhysicsWorldBuilder.UpdateBroadphaseImmediate(ref buildPhysicsData.PhysicsData, timeStep, stepComponent.Gravity,
                        state.LastSystemVersion);
                }
            }
            else
            {
                //完全重建物理 world。
                if (settings.UpdateBroadphaseAndMotion == 0)
                {
                    state.Dependency = PhysicsWorldBuilder.SchedulePhysicsWorldBuild(ref state, ref buildPhysicsData.PhysicsData,
                        state.Dependency, timeStep, stepComponent.MultiThreaded > 0, stepComponent.Gravity, state.LastSystemVersion);
                }
                else
                {
                    buildPhysicsData.PhysicsData.Update(ref state);
                    state.Dependency = PhysicsWorldBuilder.ScheduleUpdateMotionData(ref state, ref buildPhysicsData.PhysicsData, state.Dependency);
                    //此方法内部检查是否有必要更新静态树
                    state.Dependency = PhysicsWorldBuilder.ScheduleUpdateBroadphase(
                        ref buildPhysicsData.PhysicsData, timeStep, stepComponent.Gravity, state.LastSystemVersion,
                        state.Dependency, stepComponent.MultiThreaded > 0);
                }
            }
            SystemAPI.SetSingleton(new PhysicsWorldSingleton
            {
                PhysicsWorld = buildPhysicsData.PhysicsData.PhysicsWorld,
                PhysicsWorldIndex = buildPhysicsData.WorldFilter
            });
        }
    }
}
