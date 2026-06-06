using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Unity.Entities.Build;
using Unity.Entities.Content;
using System.Collections.Generic;

namespace ContentManagement.Sample.Editor
{
    public class ContentBuilder
    {
        // 要发布内容目录，请通过右键单击 WeakSceneListScriptableObject 来调用此功能
        // 资源窗口中的实例，然后单击发布 -> 发布目录
        [MenuItem("Assets/Publish/Publish Catalog from a WeakSceneListScriptableObject")]
        private static void PublishContent(MenuCommand command)
        {
            WeakSceneListScriptableObject weakSceneList = Selection.activeObject as WeakSceneListScriptableObject;

            if (weakSceneList == null)
            {
                Debug.LogError("Publish Catalog is only supported for WeakSceneListScriptableObject assets. Please select an existing one or create a new asset.");
                return;
            }

            Debug.Log("Publishing content catalog");

            // 收集我们想要包含在目录中的 subscenes 的 GUIDs
            var subSceneGuids = new HashSet<Unity.Entities.Hash128>();
            foreach (var weakScene in weakSceneList.LocalScenes)
            {
                subSceneGuids.Add(weakScene.Id.GlobalId.AssetGUID);
            }
            foreach (var weakScene in weakSceneList.RemoteScenes)
            {
                subSceneGuids.Add(weakScene.Id.GlobalId.AssetGUID);
            }

            var tempPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), $"ContentUpdateBuildDir/{PlayerSettings.productName}");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            // 玩家引导用于识别构建类型
            // （使用 Netcode for Entities 时，必须区分 client 和 server）
            var playerGuid = (DotsGlobalSettings.Instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client)
                ? DotsGlobalSettings.Instance.GetClientGUID()
                : DotsGlobalSettings.Instance.GetServerGUID();
            if (!playerGuid.IsValid)
            {
                throw new Exception("Invalid Player GUID");
            }

            Debug.Log($"<color=green>Content catalog will built</color>: {subSceneGuids.Count} subscenes");

            // 构建 subscenes 并将其存储在 tempPath 中
            RemoteContentCatalogBuildUtility.BuildContent(
                subSceneGuids, playerGuid, EditorUserBuildSettings.activeBuildTarget, tempPath);

            // 从 tempPath 复制到目标文件夹并将资产重命名为其内容哈希值。
            var contentPath = WeakSceneListScriptableObject.ContentPath;
            var contentSetName = WeakSceneListScriptableObject.ContentSetName;
            if (RemoteContentCatalogBuildUtility.PublishContent(tempPath, contentPath, f => new string[] { contentSetName } ))
            {
                Debug.Log($"<color=green>Content catalog published</color>");
                Directory.Delete(tempPath, true);
            }
        }
    }
}