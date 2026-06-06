using Unity.Entities;
using UnityEngine;

namespace Unity.DotsUISample
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float MovementSpeed = 5.0f;
        public CollectablesData collectables;

        public class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new Player
                {
                    MovementSpeed = authoring.MovementSpeed
                });
                AddBuffer<InventoryItem>(entity);
                var buf = AddBuffer<CollectableCount>(entity);
                buf.Length = authoring.collectables.Collectables.Length;
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = new CollectableCount { Count = 0 };
                }
            }
        }
    }

    public struct Player : IComponentData
    {
        public float MovementSpeed;
        public int EnergyCount;
    }

    // 缓冲区长度应等于项目类型的数量
    // 每种类型的项目数量
    // collectableCountBuf[CollectableType.FireFlower] 是火花等的计数。
    public struct CollectableCount : IBufferElementData
    {
        public int Count;
    }

    // 玩家的物品按照它们应该出现在库存窗口中的顺序排列
    public struct InventoryItem : IBufferElementData
    {
        public CollectableType Type;
    }
}