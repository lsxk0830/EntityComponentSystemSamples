using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.Profiling;
using UnityEditorInternal;
#endif

namespace Unity.Physics.Tests.PerformanceTests
{
    internal class PerformanceTestUtils
    {
        public const int k_PhysicsFrameCount = 800;

        private static string[] s_FilteredOutScenes =
        {
#if UNITY_STANDALONE_LINUX
            // [DOTS-9376] 目前与 Unity.Physics.Tests.PerformanceTests.Havok_PerformanceTest_Parallel.LoadScenes 一起悬挂
            "/ConvexCollisionPerformanceTest.unity",
            "/RagdollPerformanceTest.unity",
            "/SphereCollisionPerformanceTest.unity",
            "/CubeCollisionPerformanceTest.unity",
#endif
#if UNITY_IOS
            "/RagdollPerformanceTest.unity",
#endif
#if UNITY_PS4 || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
            // DOTS-10319 - 在 PS4、Linux 和 Windows 上崩溃
            "/TreeLifetimePerformanceTest.unity",
#endif
        };

        private static string[] s_UnityPhysicsOnlyFilterIn =
        {
            "/DetailedStaticMeshCollision",
        };

        private static string[] s_SubsteppedFilterInOnly =
        {
#if UNITY_STANDALONE_LINUX
            // [DOTS-9376] 目前与 Unity.Physics.Tests.PerformanceTests.Havok_PerformanceTest_Parallel.LoadScenes 一起悬挂
            "/ChainTestWithMassPerformanceTest.unity",
#else
            "/SphereCollisionPerformanceTest.unity",
            "/CubeCollisionPerformanceTest.unity",
            "/ChainTestWithMassPerformanceTest.unity",
#endif
        };

        public static IEnumerator RunTest(int frameCount, List<SampleGroup> sampleGroups, List<string> lowLevelMarkers)
        {
            const int kWarmupCount = 10;
            if (lowLevelMarkers.Count == 0)
            {
                yield return Measure.Frames()
                    .ProfilerMarkers(sampleGroups.ToArray())
                    .WarmupCount(kWarmupCount)
                    .MeasurementCount(frameCount)
                    .Run();
            }
            else
            {
                int maxAttempts = 5;
                int attempt = 0;
                do
                {
                    for (var i = 0; i < kWarmupCount; ++i)
                    {
                        yield return null;
                    }
                }
                while (!Application.isPlaying && PerformanceTest.Active == null && attempt++ < maxAttempts);

                var frameSampleGroup = new SampleGroup("FrameTime");
                // 请求的分析器标记的采样时间
                using (Measure.ProfilerMarkers(sampleGroups.ToArray()))
                {
                    for (var i = 0; i < frameCount; ++i)
                    {
                        // 示例帧时间
                        using (Measure.Scope(frameSampleGroup))
                        {
                            yield return null;
                        }

#if false // DOTS-10456：暂时禁用，直到我们找到可靠地获取此数据的方法。
#if UNITY_EDITOR
                        // 添加低电平标记计时
                        foreach (var marker in lowLevelMarkers)
                        {
                            var accumulatedTime = GetAccumulatedTime(marker);
                            Measure.Custom(marker, accumulatedTime);
                        }
#endif
#endif
                    }

                    // Note 取自 package com.unity.test-framework.performance@3.0.3 中的 FramesMeasurement.Run()，
                    // 这里这个函数的灵感来自于：
                    // 批处理模式下编辑器上不调用 WaitForEndOfFrame 协程
                    // 这可能会导致意外行为，最好避免
                    // https://docs.unity3d.com/ScriptReference/WaitForEndOfFrame.html
                    if (!Application.isBatchMode && Application.isPlaying)
                    {
                        yield return new WaitForEndOfFrame();
                    }
                }
            }
        }

#if UNITY_EDITOR
        static bool GetAccmulatedTimeAtFrame(string markerName, int frameIndex, out float accumulatedTime)
        {
            accumulatedTime = 0;
            bool dataFound = false;
            for (int j = 0;; ++j)
            {
                using RawFrameDataView frame = ProfilerDriver.GetRawFrameDataView(frameIndex, j);
                if (!frame.valid)
                    break;

                var markerId = frame.GetMarkerId(markerName);
                for (int i = 0; i < frame.sampleCount; ++i)
                {
                    if (markerId != frame.GetSampleMarkerId(i))
                        continue;

                    dataFound = true;
                    var time = frame.GetSampleTimeMs(i);
                    accumulatedTime += time;
                }
            }

            return dataFound;
        }

        /// <summary>
        /// 使用原始帧数据访问获取最后一个模拟帧中特定标记的累积时间。
        /// 当前需要获取 C# jobs 的时序数据。
        /// </summary>
        /// 探查器标记的 <param name="markerName">Name 访问时间 of</param>
        /// <returns>Accumulated ms</returns> 时间
        static float GetAccumulatedTime(string markerName)
        {
            bool dataFound = false;

            float accumulatedTime = 0;
            int frameIndex = Time.frameCount + 1;
            do
            {
                dataFound = GetAccmulatedTimeAtFrame(markerName, frameIndex--, out accumulatedTime);
                // 如果当前帧中没有找到数据，请尝试上一帧。
                // 数据是异步记录的，可能尚不可用。
                // 我们确实认识到这并不理想，并且可能会导致数据读数重复，
                // 但结果仍能代表 system 的性能。
            }
            while (!dataFound && frameIndex > -1);
            if (!dataFound)
            {
                Debug.LogWarning($"No data found for marker {markerName}");
            }

            return accumulatedTime;
        }

#endif
        public static IEnumerable GetPerformanceTestData()
        {
            var excludeByDefault = s_FilteredOutScenes.Concat(s_UnityPhysicsOnlyFilterIn).ToArray();
            return GetPerformanceTestByDefault(excludeByDefault, new string[0]);
        }

        public static IEnumerable GetUnityPhysicsOnlyPerformanceTestData()
        {
            return GetPerformanceTestByDefault(new string[0], s_UnityPhysicsOnlyFilterIn);
        }

        public static IEnumerable GetSubsteppedPerformanceTestData()
        {
            return GetPerformanceTestByDefault(new string[0], s_SubsteppedFilterInOnly);
        }

        public static IEnumerable GetPerformanceTestByDefault(string[] s_FilteredOut, string[] s_FilteredOnly)
        {
            var scenes = new List<TestFixtureData>();

            var sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
                if (scenePath.Contains("Tests/Performance"))
                {
                    // 跳过过滤器中包含的 scenes
                    if (s_FilteredOut.Length > 0 && s_FilteredOut.Any(filter => scenePath.Contains(filter)))
                        continue;

                    // 仅添加过滤器中包含的 scenes
                    if (s_FilteredOnly.Length > 0)
                    {
                        if (s_FilteredOnly.Any(filter => scenePath.Contains(filter)))
                        {
                            var _sceneName = Path.GetFileName(scenePath);
                            scenes.Add(new TestFixtureData(_sceneName, scenePath));
                        }

                        continue;
                    }

                    // 如果未应用过滤器，则包括包含指定路径的任何 scene
                    var sceneName = Path.GetFileName(scenePath);

                    // 为此 scene 添加两个测试用例：一个启用增量静态宽相，另一个不启用。
                    if (scenePath.Contains("/TreeLifetimePerformanceTest.unity"))
                    {
                        scenes.Add(PerformanceTestFixture.CreateIncrementalBroadphaseTestFixtureData(sceneName, scenePath, true));
                        scenes.Add(PerformanceTestFixture.CreateIncrementalBroadphaseTestFixtureData(sceneName, scenePath, false));
                    }
                    else
                    {
                        scenes.Add(PerformanceTestFixture.CreateTestFixtureData(sceneName, scenePath));
                    }
                }
            }

            return scenes;
        }

        public static void GetProfilingRequestInfo(ref List<SampleGroup> sampleGroups, ref List<string> lowLevelMarkers, bool havokPerformance = false)
        {
            sampleGroups.Add(new SampleGroup("Default World Unity.Entities.FixedStepSimulationSystemGroup"));
            sampleGroups.Add(new SampleGroup(PhysicsPerformanceTestsSystem.k_PhysicsContactCountName, SampleUnit.Byte));

#if UNITY_EDITOR
            if (!havokPerformance)
            {
                lowLevelMarkers.Add("Broadphase:StaticVsDynamicFindOverlappingPairsJob (Burst)");
                lowLevelMarkers.Add("Broadphase:DynamicVsDynamicFindOverlappingPairsJob (Burst)");
                lowLevelMarkers.Add("DispatchPairSequencer:CreateDispatchPairPhasesJob (Burst)");
                lowLevelMarkers.Add("NarrowPhase:ParallelCreateContactsJob (Burst)");
                lowLevelMarkers.Add("Solver:ParallelBuildJacobiansJob (Burst)");
                lowLevelMarkers.Add("Solver:ParallelSolverJob (Burst)");
            }
            else
            {
                lowLevelMarkers.Add("HavokSimulation:StepJob (Burst)");
            }
#endif
        }
    }

    [TestFixtureSource(typeof(PerformanceTestUtils), nameof(PerformanceTestUtils.GetPerformanceTestData))]
    internal class PerformanceTestFixture : UnityPhysicsSamplesTest
    {
        readonly string m_ScenePath;
        readonly bool m_IncrementalStaticBroadphase;

        public static TestFixtureData CreateTestFixtureData(string sceneName, string scenePath)
        {
            return new TestFixtureData(sceneName, scenePath);
        }

        public static TestFixtureData CreateIncrementalBroadphaseTestFixtureData(string sceneName, string scenePath, bool incrementalStaticBroadphaseEnabled)
        {
            return new TestFixtureData(sceneName, scenePath, Tuple.Create("Incremental Static Broadphase", incrementalStaticBroadphaseEnabled));
        }

        public PerformanceTestFixture(string sceneName, string scenePath)
        {
            m_ScenePath = scenePath;
            m_IncrementalStaticBroadphase = false;
        }

        public PerformanceTestFixture(string sceneName, string scenePath,
                                      Tuple<string, bool> incrementalStaticBroadphaseNamedParam)
        {
            m_ScenePath = scenePath;
            m_IncrementalStaticBroadphase = incrementalStaticBroadphaseNamedParam.Item2;
        }

        [UnityTest, Performance]
        [Timeout(10000000)]
        public IEnumerator UnityPhysicsTest()
        {
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.UnityPhysics,
                multiThreaded: true, incrementalStaticBroadphase: m_IncrementalStaticBroadphase);

            SceneManager.LoadScene(m_ScenePath);

            var sampleGroups = new List<SampleGroup>();
            var lowLevelProfilingMarkers = new List<string>();
            PerformanceTestUtils.GetProfilingRequestInfo(ref sampleGroups, ref lowLevelProfilingMarkers);

            return PerformanceTestUtils.RunTest(PerformanceTestUtils.k_PhysicsFrameCount, sampleGroups, lowLevelProfilingMarkers);
        }

#if HAVOK_PHYSICS_EXISTS
        [UnityTest, Performance]
        [Timeout(10000000)]
        public IEnumerator HavokTest()
        {
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.HavokPhysics,
                multiThreaded: true, incrementalStaticBroadphase: m_IncrementalStaticBroadphase);

            SceneManager.LoadScene(m_ScenePath);

            var sampleGroups = new List<SampleGroup>();
            var lowLevelProfilingMarkers = new List<string>();
            PerformanceTestUtils.GetProfilingRequestInfo(ref sampleGroups, ref lowLevelProfilingMarkers, havokPerformance: true);

            return PerformanceTestUtils.RunTest(PerformanceTestUtils.k_PhysicsFrameCount, sampleGroups, lowLevelProfilingMarkers);
        }

#endif
    }

    [TestFixtureSource(typeof(PerformanceTestUtils), nameof(PerformanceTestUtils.GetSubsteppedPerformanceTestData))]
    internal class SubsteppedPerformanceTestFixture : UnityPhysicsSamplesTest
    {
        readonly string m_ScenePath;

        public SubsteppedPerformanceTestFixture(string sceneName, string scenePath)
        {
            m_ScenePath = scenePath;
        }

        [UnityTest, Performance]
        [Timeout(10000000)]
        public IEnumerator UnityPhysicsTest([Values(2, 4)] int numSubsteps)
        {
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.UnityPhysics,
                multiThreaded: true, numSubsteps: numSubsteps);

            SceneManager.LoadScene(m_ScenePath);

            var sampleGroups = new List<SampleGroup>();
            var lowLevelProfilingMarkers = new List<string>();
            PerformanceTestUtils.GetProfilingRequestInfo(ref sampleGroups, ref lowLevelProfilingMarkers);

            return PerformanceTestUtils.RunTest(PerformanceTestUtils.k_PhysicsFrameCount, sampleGroups, lowLevelProfilingMarkers);
        }
    }

    [TestFixtureSource(typeof(PerformanceTestUtils), nameof(PerformanceTestUtils.GetUnityPhysicsOnlyPerformanceTestData))]
    internal class UnityPhysicsPerformanceTestFixture : UnityPhysicsSamplesTest
    {
        readonly string m_ScenePath;

        public UnityPhysicsPerformanceTestFixture(string sceneName, string scenePath)
        {
            m_ScenePath = scenePath;
        }

        [UnityTest, Performance]
        [Timeout(10000000)]
        public IEnumerator UnityPhysicsTest()
        {
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.UnityPhysics,
                multiThreaded: true);

            SceneManager.LoadScene(m_ScenePath);

            var sampleGroups = new List<SampleGroup>();
            var lowLevelProfilingMarkers = new List<string>();
            PerformanceTestUtils.GetProfilingRequestInfo(ref sampleGroups, ref lowLevelProfilingMarkers);

            return PerformanceTestUtils.RunTest(PerformanceTestUtils.k_PhysicsFrameCount, sampleGroups, lowLevelProfilingMarkers);
        }
    }
}
