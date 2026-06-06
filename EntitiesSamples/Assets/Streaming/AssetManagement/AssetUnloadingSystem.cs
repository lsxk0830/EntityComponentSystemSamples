using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Scenes;
using UnityEngine;

namespace Streaming.AssetManagement
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public partial struct AssetUnloadingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<References, Loading, RequestUnload>().Build();
            var referencesArray = query.ToComponentDataArray<References>(Allocator.Temp);
            var loadingArray = query.ToComponentArray<Loading>();

            // 我们不能将 SystemAPI.Query 用于此循环，因为我们需要
            // 调用在循环中进行结构更改的方法。
            for (int index = 0; index < referencesArray.Length; ++index)
            {
                var refs = referencesArray[index];
                var loading = loadingArray[index];

                // 卸载 Entity Scene
                if (loading.EntityScene != Entity.Null)
                {
                    SceneSystem.UnloadScene(state.WorldUnmanaged, loading.EntityScene,
                        SceneSystem.UnloadParameters.DestroyMetaEntities);
                }

                // 卸载 Entity Prefab
                if (loading.EntityPrefabInstance != Entity.Null)
                {
                    state.EntityManager.DestroyEntity(loading.EntityPrefabInstance);
                }

                if (loading.EntityPrefab != Entity.Null)
                {
                    SceneSystem.UnloadScene(state.WorldUnmanaged, loading.EntityPrefab,
                        SceneSystem.UnloadParameters.DestroyMetaEntities);
                }

                // 卸载 GameObject Scene
                if (loading.GameObjectScene.IsValid())
                {
                    refs.GameObjectSceneReference.Unload(ref loading.GameObjectScene);
                }

                // 卸载 GameObject Prefab
                if (loading.GameObjectPrefabInstance != null)
                {
                    Object.Destroy(loading.GameObjectPrefabInstance);
                }

                if (refs.GameObjectPrefabReference.LoadingStatus != ObjectLoadingStatus.None)
                {
                    refs.GameObjectPrefabReference.Release();
                }

                // 卸载网格
                if (loading.MeshGameObjectInstance)
                {
                    var renderer = loading.MeshGameObjectInstance.GetComponent<MeshRenderer>();
                    var material = renderer.sharedMaterial;
                    Object.Destroy(material);
                    GameObject.Destroy(loading.MeshGameObjectInstance);
                }

                if (refs.MeshReference.LoadingStatus != ObjectLoadingStatus.None)
                {
                    refs.MeshReference.Release();
                }

                // 卸料
                if (loading.MaterialGameObjectInstance)
                {
                    GameObject.Destroy(loading.MaterialGameObjectInstance);
                }

                if (refs.MaterialReference.LoadingStatus != ObjectLoadingStatus.None)
                {
                    refs.MaterialReference.Release();
                }

                // 卸载纹理
                if (loading.TextureGameObjectInstance)
                {
                    var renderer = loading.TextureGameObjectInstance.GetComponent<MeshRenderer>();
                    var material = renderer.sharedMaterial;
                    Object.Destroy(material);
                    GameObject.Destroy(loading.TextureGameObjectInstance);
                }

                if (refs.TextureReference.LoadingStatus != ObjectLoadingStatus.None)
                {
                    refs.TextureReference.Release();
                }

                // 卸载着色器
                if (refs.ShaderReference.LoadingStatus != ObjectLoadingStatus.None)
                {
                    refs.ShaderReference.Release();
                }
            }

            // 移除加载
            var noLoadingStateQuery = SystemAPI.QueryBuilder()
                .WithAll<References, Loading, RequestUnload>().Build();
            state.EntityManager.RemoveComponent<Loading>(noLoadingStateQuery);
        }
    }
#endif
}
