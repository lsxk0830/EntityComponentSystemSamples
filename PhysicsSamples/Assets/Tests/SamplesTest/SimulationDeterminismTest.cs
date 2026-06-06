//#定义 DEBUG_TEST_IN_PLAYER_BUILD

#if UNITY_ANDROID && !UNITY_64
#define UNITY_ANDROID_ARM7V
#endif

using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Physics.Tests
{
    // 在同一克隆物理 world 上运行所有模拟类型
    // 预定义的步骤数并比较结果。
    // 仅适用于独立构建，因为它需要同步 Burst 编译。
#if !UNITY_EDITOR || UNITY_PHYSICS_INCLUDE_END2END_TESTS
    [TestFixture]
#endif
    partial class UnityPhysicsSimulationDeterminismTest
    {
#if HAVOK_PHYSICS_EXISTS
        public bool SimulateHavok = false;
#endif
        static World DefaultWorld => World.DefaultGameObjectInjectionWorld;

        // 放置不应该放置的演示的名称
        // 在此数组中的本次测试中为 run
        private static string[] s_FilteredOutDemos =
        {
            "SingleThreadedRagdoll", "LoaderScene",
            "InitTestScene",

            // 以下演示已从 SimulationDeterminism 中删除，因为它们占用了
            // 完成时间太长，并且没有带来特殊价值
            "Planet Gravity", "LargeMesh", "Force Field", "ComplexStacking",

            // 只要 Havok 插件不是使用 -strict 浮点模式构建的，就会被删除
            "Raycast Car", "Joints - Parade",

            // 这些演示进行了一些目前会因 UP 失败的验证
            "AllMotors.unity",

#if UNITY_ANDROID_ARM7V
            // 由于 sigbuss 崩溃而被禁用，某些东西肯定会损坏分配器中的内存
            "Character Controller",
            "Animation",
            "ClientServer",
            "DeactivatedBodiesTriggerTest",
#endif
#if !(UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
            "SimpleStacking", //由于在某些控制台设备上不稳定而被禁用
#endif
#if UNITY_GAMECORE
            "SoftJoint" // 在 Xbox Series X 上禁用，因为它失败了。Jira 票证：DOTS-6520
#endif
#if UNITY_IOS
            // iOS 上禁用场景。错误报告：DOTS-9614
            "Joints - Ragdolls",
            "ChangeGroundFilter",
            "ChangeGroundFilterChangeCollider",
            "ChangeGroundFilterChangeMotionType",
            "ChangeGroundFilterNewCollider",
            "ChangeGroundFilterRemove",
            "ChangeGroundFilterTeleport",
            "CollisionResponse.None",
            "ChangeCompoundFilter",
            "Compound",
            "FixedAngleGrid",
            "InvalidJoint",
            "RagdollGrid",
            "SoftJoint",
            "SingleThreadedRagdoll",
            "Terrain_Triangles",
            "Terrain_VertexSamples"
#endif
        };

        protected static IEnumerable GetScenes()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var scenes = new List<string>();
            for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
                var shouldAdd = true;

                for (int i = 0; i < s_FilteredOutDemos.Length; i++)
                {
                    if (scenePath.Contains(s_FilteredOutDemos[i]))
                    {
                        shouldAdd = false;
                        break;
                    }
                }

                if (shouldAdd)
                {
                    scenes.Add(scenePath);
                }
            }
            scenes.Sort();
            return scenes;
        }

#if DEBUG_TEST_IN_PLAYER_BUILD
        static bool kContinue = false;
#endif

#if !UNITY_EDITOR || UNITY_PHYSICS_INCLUDE_END2END_TESTS
        [UnityTest]
        [Timeout(240000)]
#endif
        public virtual IEnumerator LoadScenes([ValueSource(nameof(GetScenes))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            // 等待下一帧
            yield return null;

            // 模拟步骤数
            const int k_StopAfterStep = 100;

            // 要模拟的 worlds 数量
            const int k_NumWorlds = 3;

            // 每次运行中的线程数（第二个 run 是立即模式模拟）
            NativeArray<int> numThreadsPerRun = new NativeArray<int>(k_NumWorlds, Allocator.Persistent);
            numThreadsPerRun[0] = 4;
            numThreadsPerRun[1] = 0;
            numThreadsPerRun[2] = -1;

            // 加载 scene 并等待 2 帧
            SceneManager.LoadScene(scenePath);

#if DEBUG_TEST_IN_PLAYER_BUILD
            // 要在播放器构建中进行测试，请在启用“开发构建”和“脚本调试”的情况下进行构建。
            // 附加您的 IDE 并在下面的行上放置一个断点。然后，将 kContinue 变量设置为 true
            // 继续测试并开始调试。
            while (!kContinue)
            {
                yield return null;
            }
#endif

            yield return null;
            yield return null;

            var sampler = DefaultWorld.GetOrCreateSystemManaged<BuildPhysicsWorldSampler>();
            sampler.BeginSampling();

            while (!sampler.FinishedSampling)
            {
                yield return new WaitForSeconds(0.05f);
            }

            var stepComponent = PhysicsStep.Default;
            using (var query = DefaultWorld.EntityManager.CreateEntityQuery(typeof(PhysicsStep)))
            {
                if (query.HasSingleton<PhysicsStep>())
                {
                    stepComponent = query.GetSingleton<PhysicsStep>();
                }
            }

            // 提取原件 world 并复印
            List<PhysicsWorld> physicsWorlds = new List<PhysicsWorld>(k_NumWorlds);
            for (int i = 0; i < k_NumWorlds; i++)
            {
                if (i == 0)
                {
                    physicsWorlds.Add(sampler.PhysicsWorld.Clone());
                }
                else
                {
                    physicsWorlds.Add(physicsWorlds[0].Clone());
                }
            }

            var buildStaticTree = new NativeReference<int>(1, Allocator.Persistent);

            // 模拟步骤输入
            var stepInput = new SimulationStepInput()
            {
                Gravity = stepComponent.Gravity,
                NumSubsteps = stepComponent.SubstepCount,
                NumSolverIterations = stepComponent.SolverIterationCount,
                SolverStabilizationHeuristicSettings = stepComponent.SolverStabilizationHeuristicSettings,
                SynchronizeCollisionWorld = true,
                TimeStep = DefaultWorld.Time.DeltaTime,
                HaveStaticBodiesChanged = buildStaticTree,
            };

            // 对所有 worlds 进行步进仿真
            for (int i = 0; i < physicsWorlds.Count; i++)
            {
                int threadCountHint = numThreadsPerRun[i];
                if (threadCountHint == -1)
                {
                    stepInput.World = physicsWorlds[i];
                    stepInput.World.CollisionWorld.BuildBroadphase(
                        ref stepInput.World, stepInput.TimeStep, stepInput.Gravity, true);

#if HAVOK_PHYSICS_EXISTS
                    if (SimulateHavok)
                    {
                        var simulationContext = new Havok.Physics.SimulationContext(Havok.Physics.HavokConfiguration.Default);
                        for (int step = 0; step < k_StopAfterStep; step++)
                        {
                            simulationContext.Reset(ref stepInput.World);
                            new StepHavokJob
                            {
                                Input = stepInput,
                                SimulationContext = simulationContext
                            }.Schedule().Complete();
                        }

                        simulationContext.Dispose();
                    }
                    else
#endif
                    {
                        var simulationContext = new SimulationContext();
                        for (int step = 0; step < k_StopAfterStep; step++)
                        {
                            simulationContext.Reset(stepInput);
                            new StepJob
                            {
                                Input = stepInput,
                                SimulationContext = simulationContext
                            }.Schedule().Complete();
                        }

                        simulationContext.Dispose();
                    }
                }
                else
                {
                    bool multiThreaded = threadCountHint > 0 ? true : false;
#if HAVOK_PHYSICS_EXISTS
                    if (SimulateHavok)
                    {
                        var simulation = new Havok.Physics.HavokSimulation(Havok.Physics.HavokConfiguration.Default);
                        stepInput.World = physicsWorlds[i];
                        stepInput.World.CollisionWorld.ScheduleBuildBroadphaseJobs(
                            ref stepInput.World, stepInput.TimeStep, stepInput.Gravity, buildStaticTree, default, multiThreaded).Complete();
                        for (int step = 0; step < k_StopAfterStep; step++)
                        {
                            var handles = new SimulationJobHandles(new JobHandle());
                            handles = simulation.ScheduleStepJobs(stepInput, default, multiThreaded);
                            handles.FinalExecutionHandle.Complete();
                            handles.FinalDisposeHandle.Complete();
                        }
                        simulation.Dispose();
                    }
                    else
#endif
                    {
                        var simulation = Simulation.Create();
                        stepInput.World = physicsWorlds[i];
                        stepInput.World.CollisionWorld.ScheduleBuildBroadphaseJobs(
                            ref stepInput.World, stepInput.TimeStep, stepInput.Gravity, buildStaticTree, default, multiThreaded).Complete();
                        for (int step = 0; step < k_StopAfterStep; step++)
                        {
                            var handles = new SimulationJobHandles(new JobHandle());

                            handles = simulation.ScheduleStepJobs(stepInput, default, multiThreaded);
                            handles.FinalExecutionHandle.Complete();
                            handles.FinalDisposeHandle.Complete();
                        }
                        simulation.Dispose();
                    }
                }
            }

            // 验证模拟结果
            for (int i = 0; i < physicsWorlds.Count - 1; i++)
            {
                for (int j = i + 1; j < physicsWorlds.Count; j++)
                {
                    var world1 = physicsWorlds[i];
                    var world2 = physicsWorlds[j];
                    for (int k = 0; k < world1.NumBodies; k++)
                    {
                        var result1 = world1.Bodies[k].WorldFromBody;
                        var result2 = world2.Bodies[k].WorldFromBody;
                        if (!math.all(result1.pos == result2.pos))
                        {
                            Debug.Log($"{i} vs {j}: Expected: {result1.pos}, Actual: {result2.pos}");
                        }
                        if (!math.all(result1.rot.value == result2.rot.value))
                        {
                            Debug.Log($"{i} vs {j}: Expected: {result1.rot.value}, Actual: {result2.rot.value}");
                        }
                    }
                }
            }

            // 清理
            {
                SwitchWorlds();
                numThreadsPerRun.Dispose();
                buildStaticTree.Dispose();
                for (int i = 0; i < physicsWorlds.Count; i++)
                {
                    physicsWorlds[i].Dispose();
                }
                VerifyConsoleMessages.VerifyPrintedMessages(scenePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            SwitchWorlds();
        }

        protected static void SwitchWorlds()
        {
            var entityManager = DefaultWorld.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            var entities = entityManager.GetAllEntities();
            entityManager.DestroyEntity(entities);
            entities.Dispose();

            foreach (var system in DefaultWorld.Systems)
            {
                system.Enabled = false;
            }
            DefaultWorld.Dispose();
            DefaultWorldInitialization.Initialize("Default World", false);
        }

        [Burst.BurstCompile]
        internal struct StepJob : IJob
        {
            public SimulationStepInput Input;
            public SimulationContext SimulationContext;

            public void Execute()
            {
                Simulation.StepImmediate(Input, ref SimulationContext);
            }
        }

        [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
        [UpdateAfter(typeof(PhysicsSystemGroup))]
        partial class BuildPhysicsWorldSampler : SystemBase
        {
            public bool FinishedSampling = false;
            public World DefaultWorld => World.DefaultGameObjectInjectionWorld;
            public PhysicsWorld PhysicsWorld;
            public EntityQuery PhysicsWorldSingletonQuery;

            public void BeginSampling()
            {
                Enabled = true;
            }

            protected override void OnCreate()
            {
                Enabled = false;
                PhysicsWorld = new PhysicsWorld(0, 0, 0);
                PhysicsWorldSingletonQuery = GetEntityQuery(
                    new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>());
            }

            protected override void OnUpdate()
            {
                CompleteDependency();
                var world = PhysicsWorldSingletonQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

                if (world.NumBodies != 0)
                {
                    EntityManager.CompleteAllTrackedJobs();
                    PhysicsWorld.Dispose();
                    PhysicsWorld = world.Clone();
                    Enabled = false;
                    FinishedSampling = true;
                }
            }

            protected override void OnDestroy()
            {
                PhysicsWorld.Dispose();
            }
        }

#if HAVOK_PHYSICS_EXISTS
        [Burst.BurstCompile]
        internal struct StepHavokJob : IJob
        {
            public SimulationStepInput Input;
            public Havok.Physics.SimulationContext SimulationContext;

            public void Execute()
            {
                Havok.Physics.HavokSimulation.StepImmediate(Input, ref SimulationContext);
            }
        }
#endif
    }

#if HAVOK_PHYSICS_EXISTS
#if !UNITY_EDITOR || UNITY_PHYSICS_INCLUDE_END2END_TESTS
    [TestFixture]
#endif
    class HavokPhysicsSimulationDeterminismTest : UnityPhysicsSimulationDeterminismTest
    {
        public HavokPhysicsSimulationDeterminismTest()
        {
            SimulateHavok = true;
        }
    }
#endif
}
