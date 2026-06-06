using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Baking.BakingTypes
{
    public class CompoundBBAuthoring : MonoBehaviour
    {
        class Baker : Baker<CompoundBBAuthoring>
        {
            public override void Bake(CompoundBBAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<CompoundBBComponent>(entity);
            }
        }
    }

    // 该 component 被添加到边界框内的 entities 的父级，并且
    // 存储包围它们的边界框。
    public struct CompoundBBComponent : IComponentData
    {
        public float3 MinBBVertex;
        public float3 MaxBBVertex;
    }
}
