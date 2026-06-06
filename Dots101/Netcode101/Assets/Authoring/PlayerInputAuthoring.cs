using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace KickBall
{
    public class PlayerInputAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerInputAuthoring>
        {
            public override void Bake(PlayerInputAuthoring authoring)
            {
                var entity = GetEntity(authoring.gameObject, TransformUsageFlags.None);
                AddComponent<PlayerInput>(entity);
            }
        }
    }

    // 我们只需每帧设置一次输入 component。
    // Netcode 会将值附加到 client 的输入缓冲区。
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerInput : IInputComponentData
    {
        public float Horizontal;
        public float Vertical;
        public InputEvent KickBall;
        public InputEvent SpawnBall;
    }
}
