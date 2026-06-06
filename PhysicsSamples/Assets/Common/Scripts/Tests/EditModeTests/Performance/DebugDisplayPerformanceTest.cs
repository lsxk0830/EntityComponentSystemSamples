using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Scenes;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Physics.Tests.Performance
{
    public class DebugDisplayPerformanceTests
    {
        IEnumerator MeasurePerformance(int numWarmupFrames, int numMeasureFrames, string frameTimeName, Action<int> frameAction = null)
        {
            for (int i = 0; i < numWarmupFrames; ++i)
            {
                yield return null;
            }
            for (int i = 0; i < numMeasureFrames; ++i)
            {
                using var scope = Measure.Scope(frameTimeName);
                frameAction?.Invoke(i);
                yield return null;
            }
        }

        [UnityTest, Performance]
        public IEnumerator TestDebugDisplayPerformance()
        {
            const string scenePath = "Assets/Tests/Performance/DebugDisplayPerformanceTest.unity";
            if (!File.Exists(scenePath))
            {
                Assert.Inconclusive("The path to the Scene is not correct.");
            }

            EditorSceneManager.OpenScene(scenePath);
            var subScenes = SubScene.AllSubScenes;
            foreach (var subScene in subScenes)
            {
                // 启用子 scene 进行编辑以确保其立即可用
                Scenes.Editor.SubSceneUtility.EditScene(subScene);
            }

            // 定位相机游戏对象
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(camera);
            var cameraPos = camera.transform.localPosition;
            var cameraDeltaPos = new Vector3(0, 0.01f, 0);

            // 在编辑模式下测量
            const int kMeasureFrames = 60;
            const int kWarmupFrames = 10;
            yield return MeasurePerformance(kWarmupFrames, kMeasureFrames, "EditMode Frame Time", (frameIndex)
                => camera.transform.localPosition = cameraPos + (frameIndex % 2) * cameraDeltaPos); // Note: 我们将每一帧稍微移动相机到 trigger 在 EditMode 中重新渲染

            // 进入播放模式并在播放模式下再次测量
            yield return new EnterPlayMode();

            yield return MeasurePerformance(kWarmupFrames, kMeasureFrames, "PlayMode Frame Time");
        }
    }
}
