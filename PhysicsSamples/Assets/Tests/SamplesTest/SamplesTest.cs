#if UNITY_ANDROID && !UNITY_64
#define UNITY_ANDROID_ARM7V
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Physics.Systems;
using Unity.Scenes;

namespace Unity.Physics.Tests
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    partial class SimulationConfigurationSystem : SystemBase
    {
        public SimulationType SimulationType;
        public int NumSubsteps;
        public int NumSolverIterations;
        public bool MultiThreaded;
        public bool IncrementalDynamicBroadphase;
        public bool IncrementalStaticBroadphase;
        public bool SubstepOverride;
        public bool SolverIterationOverride;

        protected override void OnUpdate()
        {
            if (SystemAPI.HasSingleton<PhysicsStep>())
            {
                var component = SystemAPI.GetSingletonRW<PhysicsStep>();
                component.ValueRW.SimulationType = SimulationType;
                component.ValueRW.MultiThreaded = (byte)(MultiThreaded ? 1 : 0);
                component.ValueRW.IncrementalDynamicBroadphase = IncrementalDynamicBroadphase;
                component.ValueRW.IncrementalStaticBroadphase = IncrementalStaticBroadphase;
                if (SubstepOverride) component.ValueRW.SubstepCount = NumSubsteps;
                if (SolverIterationOverride) component.ValueRW.SolverIterationCount = NumSolverIterations;
            }
            else
            {
                CompleteDependency();
            }
        }
    }

    [TestFixture]
    abstract class UnityPhysicsSamplesTest
    {
        protected static World DefaultWorld => World.DefaultGameObjectInjectionWorld;

        protected static IEnumerable GetScenes()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var scenes = new List<string>();
            for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
                if (scenePath.Contains("InitTestScene")
                    || scenePath.Contains("Built-in Prefab Joint Conversion")
                    || scenePath.Contains("ChainTestWithMass") // 测试运行 26s。只想进行性能测试
                                                               // 为了避免 API 不影响物理的破损，一些 packages 从 CI 上的项目中删除
                                                               // 任何 scenes 引用 com.unity.inputsystem 中的资产类型都必须在 UNITY_INPUT_SYSTEM_EXISTS 后面进行保护
#if !UNITY_INPUT_SYSTEM_EXISTS
                    || scenePath.Contains("LoaderScene")
#endif
                )
                    continue;

#if UNITY_ANDROID_ARM7V || UNITY_IOS
                // 地形 scene 需要大量内存，在 Android armv7 和 IOS 上跳过它
                if (scenePath.Contains("/Terrain.unity"))
                    continue;

                // 性能 scenes 需要大量内存，在 Android armv7 和 IOS 上跳过它
                if (scenePath.Contains("/ConvexCollisionPerformanceTest.unity") ||
                    scenePath.Contains("/CubeCollisionPerformanceTest.unity") ||
                    scenePath.Contains("/RagdollPerformanceTest.unity") ||
                    scenePath.Contains("/SphereCollisionPerformanceTest.unity"))
                    continue;

                // 似乎 run 在 armv7 和 IOS 上内存不足
                if (scenePath.Contains("/Character Controller.unity") ||
                    scenePath.Contains("/Raycast Car.unity"))
                    continue;

                //SIGSEGV/SIGBUSS 错误看起来好像某处存在一些对齐/越界访问
                //示例测试似乎是在 CI 上随机 trigger 进行的，在发表此评论时，它无法在本地重现
                if (scenePath.Contains("/Animation.unity")
                    || scenePath.Contains("/ClientServer.unity")
                    || scenePath.Contains("/DeactivatedBodiesTriggerTest")) //所有 trigger 测试 scenes
                    continue;
#endif

#if UNITY_IOS
                // 由于 2023.3.0a17 上 iOS 设备特定崩溃而被禁用：DOTS-9820
                if (scenePath.Contains("/Pyramids.unity"))
                    continue;

                // 我们使用 HavokPhysics 跳过测试
                if (scenePath.Contains("/Joints - Ragdolls.unity") ||
                    scenePath.Contains("/ChangeGroundFilter.unity") ||
                    scenePath.Contains("/ChangeGroundFilterChangeCollider.unity") ||
                    scenePath.Contains("/ChangeGroundFilterChangeMotionType.unity") ||
                    scenePath.Contains("/ChangeGroundFilterNewCollider.unity") ||
                    scenePath.Contains("/ChangeGroundFilterRemove.unity") ||
                    scenePath.Contains("/ChangeGroundFilterTeleport.unity") ||
                    scenePath.Contains("/CollisionResponse.None.unity") ||
                    scenePath.Contains("/ChangeCompoundFilter.unity") ||
                    scenePath.Contains("/Compound.unity") ||
                    scenePath.Contains("/FixedAngleGrid.unity") ||
                    scenePath.Contains("/InvalidJoint.unity") ||
                    scenePath.Contains("/RagdollGrid.unity") ||
                    scenePath.Contains("/SoftJoint.unity") ||
                    scenePath.Contains("/SingleThreadedRagdoll.unity") ||
                    scenePath.Contains("/Terrain_Triangles.unity") ||
                    scenePath.Contains("/Terrain_VertexSamples.unity"))
                    continue;
#endif

#if UNITY_STANDALONE_WIN
                // DOTS-10318 RagdollPerformanceTest 在 Windows Standalone 上失败
                if (scenePath.Contains("/RagdollPerformanceTest.unity"))
                    continue;
#endif

#if UNITY_STANDALONE_LINUX
             // 由于 Ubuntu 1.4 版本失败，我们正在跳过测试
             if (scenePath.Contains("/VehicleOverTerrain.unity"))
             {
                 continue;
             }
#endif

                scenes.Add(scenePath);
            }
            scenes.Sort();
            return scenes;
        }

        // 将指定的 scenes 添加到要测试的 scenes 列表中，而不是跳过指定的 scenes。
        protected static IEnumerable GetScenesForSubstepTesting()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var scenes = new List<string>();
            for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);

#if UNITY_IOS
                // 由于 2023.3.0a17 上 iOS 设备特定崩溃而被禁用：DOTS-9820
                if (scenePath.Contains("/Pyramids.unity"))
                    continue;
#endif
                // 应测试子步的一系列演示和测试。没有选择以下测试
                // 在某些平台上有条件地跳过（iOS 上的金字塔除外）
                // Note: 模拟验证失败：运动属性 - Mass.unity、BasicStacks.unity
                if (scenePath.Contains("/HelloWorld.unity") ||
                    scenePath.Contains("/GravityWell.unity") ||
                    scenePath.Contains("/Collider Parade - Basic.unity") ||
                    scenePath.Contains("/Motion Properties - Gravity Factor.unity") ||
                    scenePath.Contains("/Motion Properties - Velocity.unity") ||
                    scenePath.Contains("/Material Properties - Friction.unity") ||
                    scenePath.Contains("/Material Properties - Restitution.unity") ||
                    scenePath.Contains("/Events - Contacts.unity") ||
                    scenePath.Contains("/Events - Triggers - Gravity Factor.unity") ||
                    scenePath.Contains("/Joints - Parade.unity") ||
                    scenePath.Contains("/Motors - Parade.unity") ||
                    scenePath.Contains("/Joints - Single Ragdoll.unity") ||
                    scenePath.Contains("/Modify - Apply Impulse.unity") ||
                    scenePath.Contains("/Modify - Velocity.unity") ||
                    scenePath.Contains("/Modify - Kinematic Motion.unity") ||
                    scenePath.Contains("/Modify - Narrowphase Contacts.unity") ||
                    scenePath.Contains("/Modify - Surface Velocity.unity") ||
                    scenePath.Contains("/Pool.unity") ||

                    // 应测试子步的测试 scenes 的选择
                    scenePath.Contains("/CollisionUT.unity") ||
                    scenePath.Contains("/CollisionEventDataUT.unity") ||
                    scenePath.Contains("/CollisionEventsUT.unity") ||
                    scenePath.Contains("/RestitutionCollisionEventDataUT.unity") ||
                    scenePath.Contains("/TriggerEventDataUT.unity") ||
                    scenePath.Contains("/TriggerEventsUT.unity") ||
                    scenePath.Contains("/FrictionUT.unity") ||
                    scenePath.Contains("/RestitutionUT.unity") ||
                    scenePath.Contains("/ContactModifiersUT.unity") ||
                    scenePath.Contains("/JacobianModifiersUT.unity") ||
                    scenePath.Contains("/GravityFactorUT.unity") ||
                    scenePath.Contains("/ThinBoxes.unity") ||
                    scenePath.Contains("/Stiff Limits.unity") ||
                    scenePath.Contains("/Pyramids.unity") ||
                    scenePath.Contains("/SimpleStacking.unity"))
                {
                    scenes.Add(scenePath);
                }
            }
            scenes.Sort();
            return scenes;
        }

        [TearDown]
        public void TearDown()
        {
            ResetDefaultWorld();
        }

        protected IEnumerator LoadSceneAndSimulate(string scenePath)
        {
            VerifyConsoleMessages.ClearMessagesInConsole();

            yield return LoadScene(scenePath);

            yield return Simulate();

            ResetDefaultWorld();
            yield return new WaitForFixedUpdate();

            VerifyConsoleMessages.VerifyPrintedMessages(scenePath);
        }

        protected IEnumerator LoadScene(string scenePath)
        {
            SceneManager.LoadScene(scenePath);
            // 跳过一帧以便 trigger 加载，从而启动 Sub Scene 加载过程，我们可以发现
            // 对应下面的 scene entities。
            yield return new WaitForFixedUpdate();

            // 找到所有 Sub Scenes 并确保它们已加载，然后再继续
            using (var subSceneQuery = DefaultWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SceneReference>()))
            {
                using (var sceneEntities = subSceneQuery.ToEntityArray(Allocator.Persistent))
                {
                    bool loading = false;
                    do
                    {
                        loading = false;
                        foreach (var sceneEntity in sceneEntities)
                        {
                            if (!SceneSystem.IsSceneLoaded(DefaultWorld.Unmanaged, sceneEntity))
                            {
                                loading = true;
                                break;
                            }
                        }

                        // 当 Sub Scenes 仍在加载时，通过跳过一帧继续等待
                        if (loading)
                        {
                            yield return new WaitForFixedUpdate();
                        }
                    }
                    while (loading);
                }
            }
        }

        protected IEnumerator Simulate()
        {
            // 在加载的 scene 中查找模拟验证并启用验证（如果存在）。
            // 然后 run 进行模拟，直到模拟验证设置中指定的结束时间段，除非
            // 它设置为“无穷大”（值 < 0，请参阅 SimulationValidationAuthoring）。
            var simulationTime = 1.0f;
            using (var query = DefaultWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SimulationValidationSettings>()))
            {
                if (query.TryGetSingletonRW(out RefRW<SimulationValidationSettings> validationSettings))
                {
                    validationSettings.ValueRW.EnableValidation = true;
                    var timeRange = validationSettings.ValueRO.ValidationTimeRange;
                    // 获取模拟结束时间，除非它设置为“无穷大”
                    if (timeRange[1] >= 0)
                    {
                        simulationTime = timeRange[1];
                    }
                    else
                    {
                        // 如果请求无限模拟验证（timeRange[1] < 0），
                        // 模拟时间至少与开始验证所需的时间一样长，再加上一秒。
                        simulationTime = timeRange[0] + 1;
                    }

                    var msg = $"Performing simulation validation: test duration set to {simulationTime} seconds.";
                    Debug.Log(msg);
                    LogAssert.Expect(LogType.Log, msg);
                }
            }

            yield return new WaitForSeconds(simulationTime);
        }

        protected static void ResetDefaultWorld()
        {
            if (DefaultWorld.IsCreated)
            {
                var systems = DefaultWorld.Systems;
                foreach (var s in systems)
                {
                    s.Enabled = false;
                }
                DefaultWorld.Dispose();
            }

            DefaultWorldInitialization.Initialize("Default World", false);
        }

        protected static void ConfigureSimulation(in World world, in SimulationType simulationType, in bool multiThreaded = true,
            in bool incrementalDynamicBroadphase = false, in bool incrementalStaticBroadphase = false,
            in int numSubsteps = 1, in int numSolverIterations = 4, in bool substepOverride = false, in bool solverIterationOverride = false)
        {
            var configSystem = world.GetExistingSystemManaged<SimulationConfigurationSystem>();

            Assert.IsNull(configSystem,
                $"The '{nameof(SimulationConfigurationSystem)}' system should only be created by the '{nameof(ConfigureSimulation)}' function!");

            configSystem = new SimulationConfigurationSystem
            {
                SimulationType = simulationType,
                MultiThreaded = multiThreaded,
                IncrementalDynamicBroadphase = incrementalDynamicBroadphase,
                IncrementalStaticBroadphase = incrementalStaticBroadphase,
                NumSubsteps = numSubsteps,
                NumSolverIterations = numSolverIterations,
                SubstepOverride = substepOverride,
                SolverIterationOverride = solverIterationOverride
            };
            world.AddSystemManaged(configSystem);
            world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().AddSystemToUpdateList(configSystem);
        }
    }

    [TestFixture]
    class UnityPhysicsSamplesTestMT : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(GetScenes))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            // 启用多线程 Unity Physics 模拟
            ConfigureSimulation(DefaultWorld, SimulationType.UnityPhysics);

            yield return LoadSceneAndSimulate(scenePath);
        }
    }

    [TestFixture]
    class UnityPhysicsSamplesTestST : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(GetScenes))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            // 启用单线程 Unity Physics 模拟
            ConfigureSimulation(DefaultWorld, SimulationType.UnityPhysics, false);

            yield return LoadSceneAndSimulate(scenePath);
        }
    }

    class UnityPhysicsSamplesTestST_Substep4_Solve2 : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(GetScenesForSubstepTesting))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            var numSolverIterations = 2;
            if (scenePath.Contains("/ThinBoxes.unity"))
            {
                numSolverIterations = 4;
            }

            // 启用单线程 Unity Physics 模拟
            ConfigureSimulation(DefaultWorld, SimulationType.UnityPhysics, false,
                false, false, 4, numSolverIterations, true, true);

            yield return LoadSceneAndSimulate(scenePath);
        }
    }

    class UnityPhysicsSamplesTestMT_Substep4_Solve2 : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(GetScenesForSubstepTesting))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            var numSolverIterations = 2;
            if (scenePath.Contains("/ThinBoxes.unity"))
            {
                numSolverIterations = 4;
            }

            // 启用单线程 Unity Physics 模拟
            ConfigureSimulation(DefaultWorld, SimulationType.UnityPhysics, true,
                false, false, 4, numSolverIterations, true, true);

            yield return LoadSceneAndSimulate(scenePath);
        }
    }
}
