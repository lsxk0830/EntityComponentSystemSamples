using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Physics.Tests
{
    interface IDeterminismTestSystem
    {
        void BeginTest();
        bool TestingFinished();
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    [CreateAfter(typeof(PhysicsInitializeGroup))]
    partial class UnityPhysicsDeterminismTestSystem : SystemBase, IDeterminismTestSystem
    {
        protected bool m_TestingFinished = false;
        protected bool m_RecordingBegan = false;

        public int SimulatedFramesInCurrentTest = 0;
        public const int k_TestDurationInFrames = 100;

        public void BeginTest()
        {
            SimulatedFramesInCurrentTest = 0;
            Enabled = true;
            World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>().Enabled = true;
            m_TestingFinished = false;
        }

        public bool TestingFinished() => m_TestingFinished;

        protected override void OnCreate()
        {
            Enabled = false;
        }

        protected void FinishTesting()
        {
            SimulatedFramesInCurrentTest = 0;
            World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>().Enabled = false;
            Enabled = false;

            m_TestingFinished = true;
        }

        protected override void OnStartRunning()
        {
            // 读/写显示单例以注册为写入数据
            // 确保显示 systems 不会干扰测试
            if (SystemAPI.HasSingleton<PhysicsDebugDisplayData>())
            {
                var data = SystemAPI.GetSingleton<PhysicsDebugDisplayData>();
                SystemAPI.SetSingleton(data);
            }
        }

        protected override void OnUpdate()
        {
            if (!m_RecordingBegan)
            {
                // > 1 因为默认静态主体，逻辑上应该 > 0
                m_RecordingBegan = SystemAPI.GetSingleton<PhysicsWorldSingleton>().NumBodies > 1;
            }
            else
            {
                SimulatedFramesInCurrentTest++;

                if (SimulatedFramesInCurrentTest == k_TestDurationInFrames)
                {
                    Dependency.Complete();
                    FinishTesting();
                }
            }
        }
    }

    // 仅适用于独立构建，因为它需要同步 Burst 编译。
#if (!UNITY_EDITOR && UNITY_PHYSICS_INCLUDE_SLOW_TESTS) || UNITY_PHYSICS_INCLUDE_END2END_TESTS
    [TestFixture]
#endif
    class UnityPhysicsEndToEndDeterminismTest
    {
        protected static World DefaultWorld => World.DefaultGameObjectInjectionWorld;
        protected const int k_BusyWaitPeriodInSeconds = 1;

        // 自 Entities 0.17.0-preview.35 起，在加载第一个 world 之前处理 scene 会导致问题
        // 解决方法是避免在加载第一个 scene 时调用 SwitchWorld()
        protected bool FirstSceneLoad = true;

        protected virtual IDeterminismTestSystem GetTestSystem() => DefaultWorld.GetExistingSystemManaged<UnityPhysicsDeterminismTestSystem>();

        protected static void SwitchWorlds()
        {
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            defaultWorld.EntityManager.CompleteAllTrackedJobs();
            foreach (var system in defaultWorld.Systems)
            {
                system.Enabled = false;
            }

            defaultWorld.Dispose();
            DefaultWorldInitialization.Initialize("Default World", false);
        }

        // 进行确定性测试没有意义的演示
        private static string[] s_FilteredOutDemos =
        {
            "InitTestScene", "LoaderScene", "SingleThreadedRagdoll",

            // 删除 1c。转换由于没有 ECS 数据，所以会导致 EndToEndDeterminismTest 超时
            "1c. Conversion"
        };

        protected static IEnumerable GetScenes()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var scenes = new List<string>();

            for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);

                bool addScene = true;

                for (int i = 0; i < s_FilteredOutDemos.Length; i++)
                {
                    if (scenePath.Contains(s_FilteredOutDemos[i]))
                    {
                        addScene = false;
                        break;
                    }
                }

                if (addScene)
                {
                    scenes.Add(scenePath);
                }
            }

            scenes.Sort();
            return scenes;
        }

        protected void LoadSceneIntoNewWorld(string scenePath)
        {
            if (!FirstSceneLoad)
            {
                SwitchWorlds();
            }
            FirstSceneLoad = false;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>().Enabled = false;
            foreach (var system in World.DefaultGameObjectInjectionWorld.Systems)
            {
                // MouseHoverSystem 可以实现端到端确定性
                // 目前无法找到类型，因此只需按名称检查
                if (system.ToString().Contains("MouseHoverSystem") || system.ToString().Contains("SmoothlyTrackCameraTarget"))
                    system.Enabled = false;
            }

            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
        }

        public void StartTest()
        {
            GetTestSystem().BeginTest();
        }

        public List<RigidTransform> EndTest()
        {
            var system = DefaultWorld.GetExistingSystemManaged<UnityPhysicsDeterminismTestSystem>();
            var world = new EntityQueryBuilder(system.WorldUpdateAllocator).WithAll<PhysicsWorldSingleton>().Build(system).GetSingleton<PhysicsWorldSingleton>();

            List<RigidTransform> results = new List<RigidTransform>();

            for (int i = 0; i < world.NumBodies; i++)
            {
                results.Add(world.Bodies[i].WorldFromBody);
            }

            return results;
        }

        [TearDown]
        public void TearDown()
        {
            SwitchWorlds();
        }

        // 仅适用于独立构建，因为它需要同步 Burst 编译。
#if (!UNITY_EDITOR && UNITY_PHYSICS_INCLUDE_SLOW_TESTS) || UNITY_PHYSICS_INCLUDE_END2END_TESTS
        [UnityTest]
#endif
        public virtual IEnumerator LoadScenes([ValueSource(nameof(GetScenes))] string scenePath)
        {
            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            List<RigidTransform> expected = null;
            List<RigidTransform> actual = null;

            // 第一个 run
            {
                // 加载 scene
                LoadSceneIntoNewWorld(scenePath);

                // 等待 ECS 在 chunks 上分发完 entities
                // Todo: 为此找到更好的解决方案
                yield return new WaitForSeconds(1);

                StartTest();

                var testSystem = GetTestSystem();

                while (!testSystem.TestingFinished())
                {
                    yield return new WaitForSeconds(k_BusyWaitPeriodInSeconds);
                }

                expected = EndTest();
            }

            // 第二个 run
            {
                //加载 scene
                LoadSceneIntoNewWorld(scenePath);

                // 等待 ECS 在 chunks 上分发完 entities
                // Todo: 为此找到更好的解决方案
                yield return new WaitForSeconds(1);

                StartTest();

                var testSystem = GetTestSystem();

                while (!testSystem.TestingFinished())
                {
                    yield return new WaitForSeconds(k_BusyWaitPeriodInSeconds);
                }

                actual = EndTest();
            }

            // 比较结果
            {
                // 确认

                Assert.IsTrue(expected.Count > 0);
                Assert.IsTrue(actual.Count > 0);
                Assert.IsTrue(expected.Count == actual.Count);

                int numSame = 0;
                int numBodies = expected.Count;
                for (int i = 0; i < numBodies; i++)
                {
                    if (math.all(expected[i].pos == actual[i].pos) && math.all(expected[i].rot.value == actual[i].rot.value))
                    {
                        numSame++;
                    }
                    else
                    {
                        if (!math.all(expected[i].pos == actual[i].pos))
                        {
                            Debug.Log($"Expected Position: {expected[i].pos}, Actual: {actual[i].pos}, RigidBodyIndex: {i}");
                        }
                        if (!math.all(expected[i].rot.value == actual[i].rot.value))
                        {
                            Debug.Log($"Expected Rotation: {expected[i].rot.value}, Actual: {actual[i].rot.value}, RigidBodyIndex: {i}");
                        }
                    }
                }

                Assert.AreEqual(numBodies, numSame, "Not all bodies have the same transform!");

                VerifyConsoleMessages.VerifyPrintedMessages(scenePath);
            }
        }
    }
}
