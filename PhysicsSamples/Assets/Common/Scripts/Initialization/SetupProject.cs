#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
using UnityEditor.OSXStandalone;
#endif

class Initialization
{
    [InitializeOnLoadMethod]
    static void SetupProject()
    {
#if UNITY_2020_2_OR_NEWER && UNITY_EDITOR_OSX
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX)
            UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64;
#endif
    }

    [InitializeOnLoadMethod]
    static void AutoImportUnityPhysicsSamples()
    {
        if (Application.isPlaying)
        {
            // 进入 playmode 时不要修改项目
            return;
        }
        // else:

        // 查找 Unity Physics package 示例并自动导入它们（如果尚未导入）。
        // Note: 我们在这里将 packageVersion 设置为 null 以忽略 package 的版本。
        foreach (var sample in Sample.FindByPackage("com.unity.physics", null))
        {
            if (!sample.isImported)
            {
                //sample.importPath = Application.dataPath + "/Authoring";
                var success = sample.Import(Sample.ImportOptions.HideImportWindow |  Sample.ImportOptions.OverridePreviousImports);
                if (!success)
                {
                    Debug.LogWarning("Failed to import Unity Physics package samples");
                }
            }
        }
    }
}
#endif
