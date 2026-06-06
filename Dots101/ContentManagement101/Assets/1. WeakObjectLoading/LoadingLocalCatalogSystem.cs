using Unity.Entities;
using Unity.Entities.Content;

namespace ContentManagement.Sample
{
    [UpdateBefore(typeof(WeakSceneLoadingSystem))]
    [UpdateBefore(typeof(WeakObjectLoadingSystem))]
    public partial struct LoadingLocalCatalogSystem : ISystem
    {
        private bool initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LocalContent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!initialized)
            {
                initialized = true;
                UnityEngine.Debug.Log($"<color=green>Loading Content Delivery From local source</color>");
                // 当设置可编写脚本的定义 ENABLE_CONTENT_DELIVERY 时，
                // 我们必须在加载资源之前初始化内容目录。
                // 在这种情况下，我们传递空值，因为 WeakObject 示例不使用任何远程目录，
                // 相反，RuntimeContentSystem 将自动使用打包在 StreamingAssets 文件夹中的内容
                // Binary build. (e.g. ContentManagementSample_Data/StreamingAssets/ for windows Standalone)
                RuntimeContentSystem.LoadContentCatalog(null, null, null, true);
                state.EntityManager.CreateEntity(typeof(ContentIsReady));
            }
        }
    }
}