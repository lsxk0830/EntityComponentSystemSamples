using Streaming.SceneManagement.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Streaming.SceneManagement.StreamingVolume
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float MovementSpeedMetersPerSecond = 5.0f;
        public Vector3 CameraOffset;

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Player
                {
                    Speed = authoring.MovementSpeedMetersPerSecond,
                    CameraOffset = authoring.CameraOffset
                });

                // 在“完整”示例中，更接近相关 entities 的图块会以更高的 LODs 加载。
                AddComponent<Relevant>(entity);
            }
        }
    }

    struct Player : IComponentData
    {
        public float Speed; // 米每秒
        public float3 CameraOffset;
    }
}
