using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct GrenadeData : IComponentData
    {
        [GhostField]
        public uint SpawnId;

        public float DestroyTimer;
    }

    public class GrenadeDataAuthoring : MonoBehaviour
    {
        class Baker : Baker<GrenadeDataAuthoring>
        {
            public override void Bake(GrenadeDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                // 通过将 DestroyTimer 设置为 inf，防止 predicted 生成的手榴弹预测它们应该被销毁。
                AddComponent(entity, new GrenadeData { DestroyTimer = float.PositiveInfinity });
            }
        }
    }
}
