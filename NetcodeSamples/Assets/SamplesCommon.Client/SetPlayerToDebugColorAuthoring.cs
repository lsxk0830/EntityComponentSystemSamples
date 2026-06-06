using System;
using Unity.Entities;
using Unity.Rendering;

namespace Unity.NetCode.Samples.Common
{
    /// <summary>Denotes 将 ghost 设置为 <see cref="NetworkIdDebugColorUtility"/>.</summary> 中指定的调试颜色
    public struct SetPlayerToDebugColor : IComponentData
    {
    }

    [UnityEngine.DisallowMultipleComponent]
    public class SetPlayerToDebugColorAuthoring : UnityEngine.MonoBehaviour
    {
        class SetPlayerToDebugColorBaker : Baker<SetPlayerToDebugColorAuthoring>
        {
            public override void Bake(SetPlayerToDebugColorAuthoring authoring)
            {
                SetPlayerToDebugColor component = default(SetPlayerToDebugColor);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
                AddComponent(entity, new URPMaterialPropertyBaseColor {Value = 1});
            }
        }
    }
}
