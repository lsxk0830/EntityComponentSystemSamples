// 树顶的表示。这是树prefab上的 MonoBehaviour component，因此
// 再生长期间需要较少的 prefab 修改。
using Unity.Entities;
using UnityEngine;

namespace Unity.Physics
{
    public class TreeTopAuthoring : MonoBehaviour
    {
        class TreeTopBaker : Baker<TreeTopAuthoring>
        {
            public override void Bake(TreeTopAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, TreeState.Default);
                AddComponent(entity, new TreeTopTag());
                AddComponent(entity, new EnableTreeDeath());
                SetComponentEnabled<EnableTreeDeath>(entity, false); //始终将烘焙设置为禁用
                AddComponent(entity, new EnableTreeGrowth());
                SetComponentEnabled<EnableTreeGrowth>(entity, false); //始终将烘焙设置为禁用
            }
        }
    }
    public struct TreeTopTag : IComponentData {}
    public struct EnableTreeDeath : IComponentData, IEnableableComponent {}
    public struct EnableTreeGrowth : IComponentData, IEnableableComponent {}
}
