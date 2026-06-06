using Unity.Entities;
using Unity.NetCode.Samples;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public class EnableClientOnlyStateAuthoring: MonoBehaviour
    {
        class Baker : Baker<EnableClientOnlyStateAuthoring>
        {
            public override void Bake(EnableClientOnlyStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                //启用示例 systems
                AddComponent<EnableClientOnlyState>(entity);
                //启用了 client 仅备份 systems
                AddComponent<EnableClientOnlyBackup>(entity);
            }
        }
    }

    public struct EnableClientOnlyState : IComponentData { }
}
