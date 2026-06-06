using System.IO;
using UnityEngine;
using Unity.Entities.Content;

namespace ContentManagement.Sample
{
    [System.Flags]
    public enum ContentSourcePath : byte
    {
        Local = 1 << 0,
        Remote = 1 << 1,
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    [CreateAssetMenu(fileName = "SceneListSO", menuName = "Scriptable Objects/SceneListSO")]
    public class WeakSceneListScriptableObject : ScriptableObject
    {
        public ContentSourcePath ContentSource;

        // 在示例中，我们只有一个 scene 发布到目录，但我们做了这个
        // 一个数组来演示如何发布多个 scenes
        public WeakObjectSceneReference[] LocalScenes;
        public WeakObjectSceneReference[] RemoteScenes;

        private static readonly string RootPath = Directory.GetParent(Application.dataPath)?.FullName;

        // “集”是目录中可下载内容的单位
        public static readonly string ContentSetName = "remote";
        public static readonly string ContentDir = "Catalog";
        public static readonly string ContentPath = Path.Combine(RootPath, ContentDir) + Path.DirectorySeparatorChar;

        // RemoteURL 可以设置为本地文件路径或云 URL。
        //
        // - 要使用本地内容（e.g.，在开发期间或在设备上），请使用“file:///”架构将 RemoteURL 设置为文件路径：
        //     示例：“文件:///C:/git/content-management-sample/”
        //   这将导致内容管理器从指定的本地目录加载资源，而不是从 StreamingAssets 加载资源。
        //
        // - 要使用远程/云内容，请将 RemoteURL 设置为 HTTP/HTTPS 路径或 IP：
        //     示例：“https://domain.com/content/ 或 https://127.0.0.1/content/”
        //   这允许内容管理器从 server 下载（资产、目录、依赖项）。
        //
        // 切换 RemoteURL 可有效更改托管内容的源位置。
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [SerializeField]
        private string remoteURLMac = "file:///Users/<your-username>/git/EntityComponentSystemSamples/Dots101/ContentManagement101";
#else
        [SerializeField]
        private string remoteURL = "file:///C:/git/EntityComponentSystemSamples/Dots101/ContentManagement101/";
#endif
        // 播放器将从内容目录下载的对象存储在此缓存中
        public static string CachePath = Path.GetFullPath(Path.Combine(RootPath, "Cache"));

        public string RemoteURL
        {
            get
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return remoteURLMac;
#else
                return remoteURL;
#endif
            }
        }
    }
}