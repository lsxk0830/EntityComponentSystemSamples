using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>Singleton component 启用 PlayerLists 功能，允许玩家 query 还有其他人连接到 server.</summary>
    public struct EnablePlayerListsFeature : IComponentData
    {
        /// <inheritdoc cref="PlayerListNotificationBuffer" />
        /// 将 <remarks>Set 设为 -1 以禁用此 feature.</remarks>
        public double EventListEntryDurationSeconds;
    }

    [DisallowMultipleComponent]
    public class EnablePlayerListsFeatureAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(EnablePlayerListsFeature), "EventListEntryDurationSeconds")]
        public double EventListEntryDurationSeconds;

        class EnablePlayerListsFeatureBaker : Baker<EnablePlayerListsFeatureAuthoring>
        {
            public override void Bake(EnablePlayerListsFeatureAuthoring authoring)
            {
                EnablePlayerListsFeature component = default(EnablePlayerListsFeature);
                component.EventListEntryDurationSeconds = authoring.EventListEntryDurationSeconds;
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}
