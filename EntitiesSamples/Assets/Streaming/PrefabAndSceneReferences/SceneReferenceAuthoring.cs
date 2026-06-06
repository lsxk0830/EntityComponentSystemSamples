using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Streaming.PrefabAndSceneReferences
{
#if UNITY_EDITOR
    public class SceneReferenceAuthoring : MonoBehaviour
    {
        public SceneAsset SceneAsset;

        class Baker : Baker<SceneReferenceAuthoring>
        {
            public override void Bake(SceneReferenceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SceneReference
                {
                    // EntitySceneReferences 存储 scene 的 GUID。
                    Value = new EntitySceneReference(authoring.SceneAsset)
                });
            }
        }
    }
#endif

    struct SceneReference : IComponentData
    {
        public EntitySceneReference Value;
    }

    struct CleanupSceneReference : ICleanupComponentData
    {
        public Entity SceneToUnload;
    }
}
