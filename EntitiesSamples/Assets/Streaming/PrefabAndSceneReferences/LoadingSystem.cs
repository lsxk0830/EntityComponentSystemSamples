using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace Streaming.PrefabAndSceneReferences
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct LoadingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<SceneReference>().Build();
            var sceneRefs = query.ToComponentDataArray<SceneReference>(Allocator.Temp);
            var entities = query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 加载 entity scene 并添加引用 entity scene 的清理 component 到
            // 卸载时主 entity 将被销毁
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var sceneEntity = SceneSystem.LoadSceneAsync(state.World.Unmanaged, sceneRefs[i].Value);
                ecb.AddComponent(entity, new CleanupSceneReference()
                {
                    SceneToUnload = sceneEntity
                });
                ecb.RemoveComponent<SceneReference>(entity);
            }

            // 加载 PrefabReferences
            foreach (var (prefabRef, entity) in
                     SystemAPI.Query<RefRO<PrefabReference>>()
                         .WithNone<RequestEntityPrefabLoaded>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new RequestEntityPrefabLoaded
                {
                    Prefab = prefabRef.ValueRO.Value
                });
            }

            // 实例化 PrefabReferences
            foreach (var (loadedPrefab, entity) in
                     SystemAPI.Query<RefRO<PrefabLoadResult>>()
                         .WithAll<PrefabReference>()
                         .WithEntityAccess())
            {
                var prefabEntity = ecb.Instantiate(loadedPrefab.ValueRO.PrefabRoot);
                ecb.AddComponent(entity, new CleanupPrefabReference()
                {
                    PrefabToUnload = prefabEntity
                });
                ecb.RemoveComponent<PrefabReference>(entity);
                ecb.RemoveComponent<RequestEntityPrefabLoaded>(entity);
            }

            ecb.Playback(state.EntityManager);

            // 在 subscene 被销毁后，卸载之前手动加载的 entity scene
            query = SystemAPI.QueryBuilder().WithAll<CleanupSceneReference>().WithNone<SceneTag>().Build();
            var cleanupSceneRefs = query.ToComponentDataArray<CleanupSceneReference>(Allocator.Temp);
            entities = query.ToEntityArray(Allocator.Temp);
            ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var cleanupSceneRef = cleanupSceneRefs[i];
                SceneSystem.UnloadScene(state.World.Unmanaged, cleanupSceneRef.SceneToUnload);
                ecb.DestroyEntity(cleanupSceneRef.SceneToUnload);
                ecb.RemoveComponent<CleanupSceneReference>(entities[i]);
            }

            // 在 subscene 被销毁后，卸载之前手动实例化的 entity prefab
            foreach (var (prefabRef, entity) in
                     SystemAPI.Query<RefRO<CleanupPrefabReference>>()
                         .WithNone<SceneTag>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(prefabRef.ValueRO.PrefabToUnload);
                ecb.RemoveComponent<CleanupPrefabReference>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
