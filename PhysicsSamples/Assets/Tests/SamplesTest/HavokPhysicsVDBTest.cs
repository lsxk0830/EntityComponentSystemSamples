#if UNITY_EDITOR_WIN && HAVOK_PHYSICS_EXISTS

using Havok.Physics;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Tests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Generics = System.Collections.Generic;

// 用于基本 Havok Visual Debugger 测试的类。它只是加载 Hello World scene 并确保使用 Havok 物理。
class HavokPhysicsVDBTest : UnityPhysicsSamplesTest
{
    protected static IEnumerable GetVDBScenes()
    {
        var sceneCount = SceneManager.sceneCountInBuildSettings;
        var scenes = new Generics.List<string>();
        for (int sceneIndex = 0; sceneIndex < sceneCount; ++sceneIndex)
        {
            var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            if (scenePath.Contains("Hello World"))
            {
                scenes.Add(scenePath);
            }
        }
        return scenes;
    }

    private IEnumerator SetupAndLoadScene(World world, HavokConfiguration havokConfig, string scenePath)
    {
        // 确保 Havok 模拟
        ConfigureSimulation(world, SimulationType.HavokPhysics);

        var system = world.GetOrCreateSystemManaged<SimulationConfigurationSystem>();
        system.EntityManager.CreateEntity(typeof(HavokConfiguration));
        var query = new EntityQueryBuilder(system.WorldUpdateAllocator).WithAllRW<HavokConfiguration>().Build(system);
        query.SetSingleton(havokConfig);

        SceneManager.LoadScene(scenePath);
        yield return new WaitForSeconds(1);
        ResetDefaultWorld();
        yield return new WaitForFixedUpdate();
    }

    [UnityTest]
    [Timeout(240000)]
    public IEnumerator LoadScenes([ValueSource(nameof(GetVDBScenes))] string scenePath)
    {
        VerifyConsoleMessages.ClearMessagesInConsole();

        var vdbProcess = new System.Diagnostics.Process();

        // 关闭 VDB 的所有现有实例并创建一个新实例
        {
            string vdbExe = System.IO.Path.GetFullPath("Packages/com.havok.physics/Tools/VisualDebugger/HavokVisualDebugger.exe");
            string vdbProcessName = System.IO.Path.GetFileNameWithoutExtension(vdbExe);

            List<System.Diagnostics.Process> processes = new List<System.Diagnostics.Process>();
            processes.AddRange(System.Diagnostics.Process.GetProcessesByName(vdbProcessName));
            foreach (var process in processes)
            {
                process.CloseMainWindow();
                process.Close();
            }

            vdbProcess.StartInfo.FileName = vdbExe;
            vdbProcess.StartInfo.Arguments = "";
            vdbProcess.Start();
            vdbProcess.WaitForInputIdle();
            // 我们如何确保 VDB 已准备好连接？
            yield return new WaitForSeconds(2);
            vdbProcess.Refresh();
        }

        var havokConfig = HavokConfiguration.Default;

        // 已启用 VDB
        havokConfig.VisualDebugger.Enable = 1;
        yield return SetupAndLoadScene(World.DefaultGameObjectInjectionWorld, havokConfig, scenePath);

        // 已禁用 VDB
        havokConfig.VisualDebugger.Enable = 0;
        yield return SetupAndLoadScene(World.DefaultGameObjectInjectionWorld, havokConfig, scenePath);

        // 启用 VDB 且计时器内存为零
        havokConfig.VisualDebugger.Enable = 1;
        havokConfig.VisualDebugger.TimerBytesPerThread = 0;
        yield return SetupAndLoadScene(World.DefaultGameObjectInjectionWorld, havokConfig, scenePath);

        // 关闭 VDB client
        vdbProcess.CloseMainWindow();
        vdbProcess.Close();

        // 启用 VDB，但未运行 Client。
        havokConfig = HavokConfiguration.Default;
        havokConfig.VisualDebugger.Enable = 1;
        yield return SetupAndLoadScene(World.DefaultGameObjectInjectionWorld, havokConfig, scenePath);

        VerifyConsoleMessages.VerifyPrintedMessages(scenePath);
    }
}

#endif
