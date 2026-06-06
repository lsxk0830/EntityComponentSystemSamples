using Unity.Entities;
using UnityEngine;

namespace HelloCube.Prefabs
{
    // authoring component 只是一个普通的 MonoBehavior，它具有 Baker<T> 类。
    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        // 在 baking 中，此 Baker 将为 subscene 中的每个 SpawnerAuthoring 实例 run 一次。
        // （请注意，将 authoring component 的 Baker 类嵌套在 authoring MonoBehaviour 类中
        // 只是风格的一个可选问题。）
        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Spawner
                {
                    Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }

    struct Spawner : IComponentData
    {
        public Entity Prefab;
    }
}
