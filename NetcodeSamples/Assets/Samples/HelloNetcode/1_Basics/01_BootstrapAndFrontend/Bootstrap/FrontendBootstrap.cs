using System;
using System.Collections.Generic;
using System.IO;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Samples.HelloNetcode
{
    // 这是处理前端菜单的设置，用户希望控制 client 和 server world 创建。
    // 我们支持：
    // - 在前端 scene 中启动游戏，允许用户选择：
    //      - “client 托管”设置。
    //      - “通过 IP 连接到现有的 server”设置。
    //      - 通过 `-scene XXX` 命令行参数自动加载 scene。
    // - 从任何其他 scene 开始时，将保留现有的“自动连接”快速启动流程。

    // 如果您不需要前端菜单（并且只想始终自动连接），通常使用就足够了
    // 一个更简单的引导程序，如下所示：
    // [UnityEngine.Scripting.Preserve]
    // public class NetCodeBootstrap : ClientServerBootstrap
    // {
    //     public override bool Initialize(string defaultWorldName)
    //     {
    //         AutoConnectPort = 7979; // Enable auto connect
    //         return base.Initialize(defaultWorldName); // Use the regular bootstrap
    //     }
    // }

    // 需要保留属性来确保在启用剥离的 il2cpp 构建中不会剥离引导程序。
    [UnityEngine.Scripting.Preserve]
    // Bootstrap 需要扩展 `ClientServerBootstrap`，项目中只能有一个类扩展它。
    public class FrontendBootstrap : ClientServerBootstrap
    {
        // Entities 调用初始化方法来创建默认的 worlds。
        public override bool Initialize(string defaultWorldName)
        {
            const string fallbackGameplayScene = "Asteroids";
            const string frontendScene = "Frontend";

            // 如果用户将 OverrideDefaultNetcodeBootstrap MonoBehaviour 添加到其活动 scene，
            // 或者在整个项目范围内禁用 Bootstrapping，我们应该在这里尊重这一点。
            if (!DetermineIfBootstrappingEnabled())
                return false;

            // 我们检查加载的 scene 是否为“前端”，这意味着我们应该 DISABLE 自动连接流。
            // 我们还检查用户是否有任何命令行参数指示我们应该加载哪个 scene。
            var isFromCommandLine = TryGetCommandLineScene(out var targetScene);
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!isFromCommandLine) targetScene = activeScene;

            var isFrontend = targetScene.Contains(frontendScene);

            // 处理 server 设置错误：
            if (IsServerPlatform)
            {
                if (isFrontend)
                {
                    Debug.LogWarning($"[FrontendBootstrap] Server build loaded the isFrontend scene ('{activeScene}'), but cannot run it, so defaulting to {nameof(fallbackGameplayScene)}: '{fallbackGameplayScene}'!");
                    targetScene = fallbackGameplayScene;
                    isFrontend = false;
                }
                else if (isFromCommandLine && string.IsNullOrEmpty(targetScene))
                {
                    Debug.LogError($"[FrontendBootstrap] Server build with invalid commandline scene, so defaulting to {nameof(fallbackGameplayScene)}: '{fallbackGameplayScene}'!");
                    targetScene = fallbackGameplayScene;
                }
            }

            // 处理流程错误：
            if (targetScene == "FrontendHUD")
            {
                targetScene = frontendScene;
                isFrontend = true;
                Debug.LogError($"[FrontendBootstrap] Cannot start via the 'FrontendHUD' scene! Loading {nameof(frontendScene)}: '{frontendScene}' instead!");
            }

            if(!Application.isEditor)
                Debug.Log($"[FrontendBootstrap] startupTime: {Time.realtimeSinceStartupAsDouble:0.0}s, targetScene: '{targetScene}', isFromCommandLine: {isFromCommandLine}, isFrontend: {isFrontend}!");

            if (isFrontend)
            {
                AutoConnectPort = 0; // 禁用前端的自动连接。
                CreateLocalWorld(defaultWorldName); // 不要创建 Client 和 Server worlds，
                                                    // 因为我们有条件地这样做（取决于用户的选择）
                                                    // 通过 FrontendHUD UI）。
            }
            else
            {
                // 这将启用自动连接。如果我们不通过前端，我们仅启用自动连接。
                // 前端将在手动连接之前解析并验证地址。
                // 使用此自动连接功能将处理来自 PlayMode 工具的 client 仅连接地址
                AutoConnectPort = 7979;

                // 从命令行运行构建时使用“-port 8000”来指定要使用的端口
                // 将覆盖默认端口
                string commandPort = CommandLineUtils.GetCommandLineValueFromKey("port");
                if (!string.IsNullOrEmpty(commandPort))
                    AutoConnectPort = UInt16.Parse(commandPort);

                // 创建适当的 worlds，然后我们可以将子 scenes 直接加载到：
                CreateDefaultClientServerWorlds();

                // 我们不在前端，因此直接加载到上述引导流程选择的任何游戏 scene 中。
                // 我们可能需要在这里更改 scene，所以这样做：
                if (activeScene != targetScene)
                {
                    Debug.Log($"[FrontendBootstrap] {nameof(activeScene)}: '{activeScene}' is not {nameof(targetScene)}: '{targetScene}', so switching to it!");
                    SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
                }
            }
            return true;
        }

        /// <summary>
        /// 这本质上是 #if UNITY_SERVER，但不必担心引入编译器错误。
        /// </summary>
        private static bool IsServerPlatform => Application.platform == RuntimePlatform.LinuxServer
                                                || Application.platform == RuntimePlatform.WindowsServer
                                                || Application.platform == RuntimePlatform.OSXServer;

        private static bool TryGetCommandLineScene(out string commandLineScene)
        {
            // 命令行总是覆盖默认值（如果存在）
            commandLineScene = CommandLineUtils.GetCommandLineValueFromKey("scene");
            if (string.IsNullOrWhiteSpace(commandLineScene))
            {
                commandLineScene = null;
                return false;
            }

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var scene = Path.GetFileNameWithoutExtension(scenePath);
                if (commandLineScene == scene)
                {
                    return true;
                }
            }

            Debug.LogError($"$TryGetCommandLineScene: '{commandLineScene}' not found. Scenes present in the build\n: {string.Join(',', GetAllScenesInBuild())}");

            static IEnumerable<string> GetAllScenesInBuild()
            {
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i)
                {
                    var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    yield return Path.GetFileNameWithoutExtension(scenePath);
                }
            }
            return false;
        }
    }
}
