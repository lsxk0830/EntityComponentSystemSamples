using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Baking.PrefabReference
{
#if UNITY_EDITOR
    public class ConfigAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public float SpawnInterval;

        class Baker : Baker<ConfigAuthoring>
        {
            public override void Bake(ConfigAuthoring authoring)
            {
                // 从 GameObject 创建 EntityPrefabReference。
                // 通过使用引用，我们只需要一个烘焙的 prefab entity 而不是
                // 在使用 prefab entity 的地方复制它。
                var prefabEntity = new EntityPrefabReference(authoring.Prefab);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Config
                {
                    PrefabReference = prefabEntity,
                    SpawnInterval = authoring.SpawnInterval
                });
            }
        }
    }
#endif

    public struct Config : IComponentData
    {
        public float SpawnInterval;
        public EntityPrefabReference PrefabReference;
    }
}
