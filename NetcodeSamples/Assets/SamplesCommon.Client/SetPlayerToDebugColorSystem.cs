using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Rendering;
using UnityEngine;
namespace Unity.NetCode.Samples.Common
{
    /// <summary>
    ///     每个 NetworkId 都有自己独特的调试颜色。这个 system 设置它。
    /// </summary>
    [AlwaysSynchronizeSystem]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    public partial class SetPlayerToDebugColorSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (color, ghostOwner) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, RefRO<GhostOwner>>()
                .WithAll<SetPlayerToDebugColor>().WithChangeFilter<MaterialMeshInfo, GhostOwner>())
            {
                color.ValueRW.Value = NetworkIdDebugColorUtility.Get(ghostOwner.ValueRO.NetworkId);
            }
        }
    }
}
