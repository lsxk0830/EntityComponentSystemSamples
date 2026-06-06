using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Streaming.SceneManagement.SceneLoading
{
    public class SceneReferenceAuthoring : MonoBehaviour
    {
#if UNITY_EDITOR
        public SceneAsset scene;

        class Baker : Baker<SceneReferenceAuthoring>
        {
            public override void Bake(SceneReferenceAuthoring authoring)
            {
                // 我们希望创建对 scene 的依赖关系，以防 scene 被删除。
                // 这需要在下面的空检查之外，以防资产文件被删除然后恢复。
                DependsOn(authoring.scene);

                if (authoring.scene != null)
                {
                    var entity = GetEntity(TransformUsageFlags.None);
                    AddComponent(entity, new SceneReference
                    {
                        // 烘焙对 scene 的引用，以在运行时加载 scene
                        Value = new EntitySceneReference(authoring.scene)
                    });
                }
            }
        }
#endif
    }

    // 触发引用的 scene 的负载
    public struct SceneReference : IComponentData
    {
        // 参考 scene 来加载
        public EntitySceneReference Value;
    }
}
