// 树干的表示。这是树prefab上的 MonoBehaviour component，因此
// 再生长期间需要较少的 prefab 修改。
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics
{
    public class TagTrunkAuthoring : MonoBehaviour
    {
        class TagTrunkBaker : Baker<TagTrunkAuthoring>
        {
            public override void Bake(TagTrunkAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, TreeState.Default);
                AddComponent(entity, new TreeTrunkTag());
                AddComponent(entity, new EnableTreeDeath());
                SetComponentEnabled<EnableTreeDeath>(entity, false); //始终将烘焙设置为禁用
            }
        }
    }

    public struct TreeTrunkTag : IComponentData {}
}
