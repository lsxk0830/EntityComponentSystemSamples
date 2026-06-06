using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;

namespace Streaming.SceneManagement.SubsceneInstancing
{
    public class GridAuthoring : MonoBehaviour
    {
#if UNITY_EDITOR
        public UnityEditor.SceneAsset scene; // 实例化 subscene
        public int size;  // 实例数将为大小 x 大小
        public float2 spacing;  // 实例之间的距离

        class Baker : Baker<GridAuthoring>
        {
            public override void Bake(GridAuthoring authoring)
            {
                // 我们需要对 scene 的依赖，以防 scene 被删除。
                // 这需要在 authoring.scene!= null 检查之外，以防资产文件被删除然后恢复。
                DependsOn(authoring.scene);

                if (authoring.scene != null)
                {
                    var entity = GetEntity(TransformUsageFlags.Dynamic);
                    AddComponent(entity, new Grid
                    {
                        Scene = new EntitySceneReference(authoring.scene),
                        Size = authoring.size,
                        Spacing = authoring.spacing
                    });
                }
            }
        }
#endif
    }

    public struct Grid : IComponentData
    {
        public EntitySceneReference Scene;
        public int Size;
        public float2 Spacing;
    }

    public struct Offset : IComponentData
    {
        public float3 Value;
    }
}
