using Unity.Entities;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public struct EnableRelayServer : IComponentData { }

    // 通过将这些类型的启用 components 添加到 entity 来启用每个示例 system
    // scene。这可以防止所有示例中的所有 systems 始终同时变为 run。
    // 然后，每个示例还可以通过添加启用 component 来启用之前示例中的 systems。
    public class EnableRelayServerAuthoring : MonoBehaviour
    {
        class Baker : Baker<EnableRelayServerAuthoring>
        {
            public override void Bake(EnableRelayServerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnableRelayServer>(entity);
            }
        }
    }
}
