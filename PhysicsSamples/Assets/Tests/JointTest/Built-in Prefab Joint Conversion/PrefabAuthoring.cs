using UnityEngine;
using Unity.Entities;

namespace Unity.Physics.Tests
{
    /// <summary>
    /// 这个 component 用于存储 prefab 以测试其转换为 entity。
    /// 它允许验证生成的 entity（标有 [Prefab] 标签）是否已正确转换。
    /// 例如，它用于[内置 Prefab Joint Conversion.scene]来验证转换过程。
    /// </summary>
    public class PrefabAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        public class PrefabAuthoringBaker : Baker<PrefabAuthoring>
        {
            public override void Bake(PrefabAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PrefabComponentData
                {
                    Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }

    // 该 component 用于存储 prefab entity 参考。
    public struct PrefabComponentData : IComponentData
    {
        public Entity Prefab;
    }
}
