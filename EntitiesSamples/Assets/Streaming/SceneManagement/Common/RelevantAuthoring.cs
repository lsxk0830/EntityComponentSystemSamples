using Unity.Entities;
using UnityEngine;

namespace Streaming.SceneManagement.Common
{
    // Authoring 类将 entity 标记为相关。这用于 entity 位置的示例
    // （e.g。播放器或摄像机）指示要加载的 scene/部分。
    public class RelevantAuthoring : MonoBehaviour
    {
        class Baker : Baker<RelevantAuthoring>
        {
            public override void Bake(RelevantAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Relevant>(entity);
            }
        }
    }

    public struct Relevant : IComponentData
    {
    }
}
