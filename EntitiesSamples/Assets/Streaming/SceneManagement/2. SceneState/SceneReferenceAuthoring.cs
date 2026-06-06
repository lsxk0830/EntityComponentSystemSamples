using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;

namespace Streaming.SceneManagement.SceneState
{
    public class SceneReferenceAuthoring : MonoBehaviour
    {
#if UNITY_EDITOR
        public List<UnityEditor.SceneAsset> sceneAssets;

        public class Baker : Baker<SceneReferenceAuthoring>
        {
            public override void Bake(SceneReferenceAuthoring authoring)
            {
                foreach (var sceneAsset in authoring.sceneAssets)
                {
                    // 我们希望创建对 scene 的依赖关系，以防 scene 被删除
                    // 这需要在 authoring.scene!= null 检查之外，以防资产文件被删除然后恢复。
                    DependsOn(sceneAsset);

                    if (sceneAsset != null)
                    {
                        // 存储加载/卸载 scenes 所需的信息并在 UI 中显示当前状态
                        var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic, false, sceneAsset.name);
                        AddComponent(entity, new SceneReference
                        {
                            SceneName = new FixedString128Bytes(sceneAsset.name),
                            SceneAsset = new EntitySceneReference(sceneAsset),
                            StreamingState = default,
                            EntityScene = default
                        });
                    }
                }
            }
        }
#endif
    }

    public enum LoadingAction
    {
        None = 0, // 无需采取任何行动
        LoadAll = 1, // 加载每个部分中的 scene 和部分 entities 和 entities
        LoadMeta = 2, // 加载 scene 和部分 entities，但不加载每个部分中的 entities
        UnloadEntities = 4, // 卸载每个部分中的 entities，但保持加载 scene 和部分 entities
        UnloadAll = 8 // 卸载 scene 和部分 entities 以及每个部分的内容
    }

    public struct SceneReference : IComponentData
    {
        public FixedString128Bytes SceneName; // scene 的名称（在 UI 中显示）
        public EntitySceneReference SceneAsset; // 参考 scene（用于 scene 中的流式传输）
        public SceneSystem.SceneStreamingState StreamingState; // 当前加载状态 scene
        public LoadingAction LoadingAction; // 请求 UI 采取行动

        // 一旦加载了 scene（即使未加载这些部分），Entity 表示 scene。
        public Entity EntityScene;
    }
}
