using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Streaming.SceneManagement.CompleteSample
{
    // 每个图块的 LOD 范围。
    public class TileLODAuthoring : MonoBehaviour
    {
        public List<float> LODRadius;

        class Baker : Baker<TileLODAuthoring>
        {
            public override void Bake(TileLODAuthoring authoring)
            {
                List<float> sorted = new List<float>(authoring.LODRadius);
                sorted.Sort();

                // 索引 n 对应第 n+1 节
                // （第 0 节将始终被加载）
                for (int index = 0; index < sorted.Count; ++index)
                {
                    var entity = CreateAdditionalEntity(TransformUsageFlags.None, true);
                    AddComponent(entity, new TileLODBaking
                    {
                        LowerRadius = index > 0 ? sorted[index - 1] : 0f,
                        HigherRadius = sorted[index],
                        Section = index + 1
                    });
                }
            }
        }
    }

    // 仅用于 baking。
    [BakingType]
    public struct TileLODBaking : IComponentData
    {
        public float LowerRadius; // 加载截面的距离
        public float HigherRadius; // 卸载截面的距离
        public int Section; // 该 component 引用的节索引
    }
}
