using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ContentManagement.Sample.Editor
{
   public class BuildPostprocessorFilteringDuplicates : IPostprocessBuildWithReport
{
    // 回拨顺序：较小的数字 = 较早的呼叫
    public int callbackOrder => 1;

    public void OnPostprocessBuild(BuildReport report)
    {
        var settings = AssetDatabase.LoadAssetAtPath<WeakSceneListScriptableObject>("Assets/2. WeakSceneLoading/Content Settings/WeakSceneList.asset");
        var isTargetingRemote = (settings.ContentSource & ContentSourcePath.Remote) != 0;

        // 在构建远程内容交付时，
        // 建议在构建完成后从 StreamingAssets 中删除所有冗余资源。
        // 这可以防止不必要的重复并减少最终构建的大小。
        // 如果构建仅打算使用本地资源，则不需要此步骤，
        // 因为所有必需的内容必须保留在 StreamingAssets（构建内的文件夹）中。
        if (!isTargetingRemote)
            return;

        string pathToBuiltProject = report.summary.outputPath;
        Debug.Log("Build completed: " + pathToBuiltProject);

        string streamingAssetsPath = null;

        if (report.summary.platform == BuildTarget.StandaloneWindows || report.summary.platform == BuildTarget.StandaloneWindows64)
        {
            string buildFolder = Path.GetDirectoryName(pathToBuiltProject);
            string dataFolder = Path.Combine(buildFolder, Path.GetFileNameWithoutExtension(pathToBuiltProject) + "_Data");
            streamingAssetsPath = Path.Combine(dataFolder, "StreamingAssets");
        }
        else if (report.summary.platform == BuildTarget.StandaloneOSX) {
            string buildFolder = pathToBuiltProject; // 。应用程序
            string dataFolder = Path.Combine(buildFolder, "Contents", "Resources", "Data");
            streamingAssetsPath = Path.Combine(dataFolder, "StreamingAssets");
        }

        if (streamingAssetsPath != null)
        {
            // 为了使 DebugCatalog.txt 文件启用，
            // 请将 ENABLE_CONTENT_BUILD_DIAGNOSTICS 添加到构建配置文件中的“脚本定义”中
            var path = Directory.GetParent(Application.dataPath).FullName;
            var catalogPath = Path.Combine(path, "Catalog/DebugCatalog.txt");

            if (Directory.Exists(streamingAssetsPath))
            {
                if (File.Exists(catalogPath))
                {
                    // 将整个目录内容读入一个字符串中进行搜索
                    string catalogContent = File.ReadAllText(catalogPath);

                    // 获取 streamingAssetsPath 中的所有文件（非递归）
                    var files = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories);
                    int deleteCount = 0;

                    foreach (var filePath in files)
                    {
                        string fileName = Path.GetFileName(filePath);
                        Debug.Log($"<color=yellow>FileName: {fileName}</color> ");
                        if(fileName.Contains(".bin") || fileName.Contains(".txt"))
                            continue;

                        if (catalogContent.Contains(fileName))
                        {
                            File.Delete(filePath);
                            deleteCount++;
                            Debug.Log($"Deleted: {fileName}");
                        }
                    }

                    Debug.Log($"<color=yellow>Deleted {deleteCount} file(s) from</color>: {streamingAssetsPath}");
                }
                else
                {
                    Debug.LogWarning($"Catalog file not found at path: {catalogPath}");
                }
            }
            else
            {
                Debug.LogWarning($"No folder {streamingAssetsPath} found");
            }
        }
    }
}
}
