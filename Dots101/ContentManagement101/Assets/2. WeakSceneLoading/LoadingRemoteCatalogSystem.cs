using System.IO;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace ContentManagement.Sample
{
    /// <summary>
    /// 使内容管理 API 能够从远程源检索内容，
    /// 将其加载到内存中，然后连接所有引用。
    /// </summary>
    [UpdateBefore(typeof(WeakSceneLoadingSystem))]
    public partial struct LoadingRemoteCatalogSystem : ISystem
    {
        private bool initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RemoteContent>();
            state.RequireForUpdate<HighLowWeakScene>();
            initialized = false;
            ContentDeliveryGlobalState.RegisterForContentUpdateCompletion(UpdateStateCallback);
        }

        private void UpdateStateCallback(ContentDeliveryGlobalState.ContentUpdateState contentUpdateState)
        {
            Debug.Log($"<color=green>Content Delivery Global State:</color> {contentUpdateState}");
            if (contentUpdateState >= ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
            {
                // 跟踪内容的状态并设置内容何时可供使用
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!initialized)
            {
                var settings = SystemAPI.GetSingleton<RemoteContent>();
                initialized = true;
                var contentPath = Path.Combine( settings.URL.ToString(), WeakSceneListScriptableObject.ContentDir) + "/";

                Debug.Log($"<color=green>Loading Content Delivery From Remote source:</color>{contentPath}");
                // 当设置 ENABLE_CONTENT_DELIVERY 可编写脚本的定义 (https://docs.unity3d.com/6000.2/Documentation/Manual/custom-scripting-symbols.html) 时，
                // 加载任何资源之前必须初始化内容目录。
                // 我们指定远程内容路径、本地缓存路径（用于存储下载的内容）、
                // 并通过仅在需要时获取内容来防止不必要的下载。
                // 内容集名称是指构建过程中定义的特定内容包。
                RuntimeContentSystem.LoadContentCatalog(contentPath, WeakSceneListScriptableObject.CachePath,
                    WeakSceneListScriptableObject.ContentSetName, true);
            }


            var entityQuery = SystemAPI.QueryBuilder().WithAll<ContentIsReady>().Build();

            if (entityQuery.CalculateEntityCount() < 1 &&
                ContentDeliveryGlobalState.CurrentContentUpdateState >=
                ContentDeliveryGlobalState.ContentUpdateState.ContentReady)
            {
                Debug.Log($"Content Delivery is <color=green>Ready From Remote</color> source");
                state.EntityManager.CreateEntity(typeof(ContentIsReady));
            }
        }
    }
}