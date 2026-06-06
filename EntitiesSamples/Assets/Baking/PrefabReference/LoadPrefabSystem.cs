using Unity.Burst;
using Unity.Entities;
using Unity.Scenes;

namespace Baking.PrefabReference
{
    public partial struct LoadPrefabSystem : ISystem
    {
        [BurstCompile]
         public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            var config = SystemAPI.GetSingleton<Config>();
            var configEntity = SystemAPI.GetSingletonEntity<Config>();

            // 添加 RequestEntityPrefabLoaded component 将请求加载 prefab。
            // 它将加载 prefab 对应的 entity scene 文件，并添加一个 PrefabLoadResult
            // component 至 entity。PrefabLoadResult component 包含 entity，您可以使用
            // 实例化 prefab（请参阅 PrefabReferenceSpawnerSystem system）。
            state.EntityManager.AddComponentData(configEntity, new RequestEntityPrefabLoaded
            {
                Prefab = config.PrefabReference
            });
        }
    }
}
