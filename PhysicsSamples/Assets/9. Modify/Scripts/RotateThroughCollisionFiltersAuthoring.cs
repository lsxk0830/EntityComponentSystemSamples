using Unity.Entities;
using UnityEngine;

namespace Unity.Physics
{
    /// <summary>
    /// 用于更改 entity 上的碰撞过滤器的倒计时器。当倒计时结束时，碰撞
    /// 过滤器更改为列表中的下一个过滤器，并重置倒计时。
    /// </summary>
    public struct ChangeCollisionFilterCountdown : IComponentData
    {
        public int Countdown;
        internal int ResetCountdown;
    }

    /// <summary>
    /// component 存储用于映射到的红色、绿色和蓝色材质的材质索引
    /// RenderMeshArray 中的 scene
    /// </summary>
    public struct ColoursForFilter : IComponentData
    {
        public int RedIndex;
        public int BlueIndex;
        public int GreenIndex;
    }

    public class RotateThroughCollisionFiltersAuthoring : MonoBehaviour
    {
        public int Countdown = 60;

        class RotateThroughCollisionFiltersBaker : Baker<RotateThroughCollisionFiltersAuthoring>
        {
            public override void Bake(RotateThroughCollisionFiltersAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ChangeCollisionFilterCountdown
                {
                    Countdown = authoring.Countdown,
                    ResetCountdown = authoring.Countdown
                });
            }
        }
    }
}
