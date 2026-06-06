using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Rendering;
using UnityEngine;

namespace KickBall
{
    public class PlayerAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
                AddComponent<Player>(entity);
                AddComponent<Color>(entity);
            }
        }
    }

    public struct Player : IComponentData
    {
    }

    // 该属性意味着该值将传递给着色器的 _BaseColor
    [MaterialProperty("_BaseColor")]
    public struct Color : IComponentData
    {
        [GhostField] public float4 Value;
    }
}
