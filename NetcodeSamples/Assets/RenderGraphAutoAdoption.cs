#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR && !URP_COMPATIBILITY_MODE

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RenderGraphAutoAdoption : IPreprocessBuildWithReport
{
    public int callbackOrder => int.MinValue + 99; // 就在 URPPreprocessBuild 之前

    void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
    {
        if (GraphicsSettings.currentRenderPipelineAssetType != typeof(UniversalRenderPipelineAsset))
            return;

        //通过序列化更改布尔值，因为它是私有的
        var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
        var global = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
        var so = new SerializedObject(global);
        SerializedProperty settingsProperty = null;

        //在全局设置序列化对象中找到 RenderGraphSettings
        var propertyIterator = so.FindProperty("m_Settings.m_SettingsList.m_List"); //从设置集合的根开始
        var end = propertyIterator.GetEndProperty();
        propertyIterator.NextVisible(true); //进入收藏
        while (!SerializedProperty.EqualContents(propertyIterator, end))
        {
            if (propertyIterator?.boxedValue == settings)
            {
                settingsProperty = propertyIterator;
                break;
            }
            propertyIterator.NextVisible(false);
        }
        if (settingsProperty == null)
            throw new BuildFailedException("Missing RenderGraphSettings in UniversalRenderPipeline's IRenderPipelineGraphicsSettings");

        //更新为使用 RenderGraph
        var flag = settingsProperty.FindPropertyRelative("m_EnableRenderCompatibilityMode");
        if (!flag.boolValue)
            return;

        flag.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(global);
    }
}

#endif