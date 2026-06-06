// 用于：5.修改/5m。Collider 修改/用于 Tile prefab（用于 ModifyGridSpawner）
// 此 component 的目的是跟踪单个图块上的 trigger 事件。评估这些 trigger 事件
// 在 TileTriggerSystem 中。由于我们不想产生无限的 colliders （稍后对 trigger 事件的反应），
// 我们限制了图块 trigger 可以生成 collider (MaxTriggerCount) 的次数。
using Unity.Entities;
using UnityEngine;

public struct TileTriggerCounter : IComponentData
{
    public int MaxTriggerCount;
    public int TriggerCount;
}

public class TileTriggerAuthoring : MonoBehaviour
{
    public int MaxTriggerCount = 3;
    public int TriggerCount = 0;

    class TileTriggerBaker : Baker<TileTriggerAuthoring>
    {
        public override void Bake(TileTriggerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TileTriggerCounter()
            {
                MaxTriggerCount = authoring.MaxTriggerCount,
                TriggerCount = authoring.TriggerCount,
            });
        }
    }
}
