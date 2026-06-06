using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Streaming.RuntimeContentManager
{
    // 创建 jobs 来计算 entities 的可见性
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DecorationVisibilitySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            new DecorationVisibilityJob
            {
                CamPos = Camera.main.transform.position,
                LoadRadius = Camera.main.farClipPlane,
                CamForward = Camera.main.transform.forward
            }.ScheduleParallel();
        }
    }

    // Job 用于计算 entity 和 trigger 加载和卸载的可见性
    [BurstCompile]
    partial struct DecorationVisibilityJob : IJobEntity
    {
        public float LoadRadius;
        public float3 CamPos;
        public float3 CamForward;

        void Execute(ref DecorationVisualComponentData dec, in LocalToWorld transform)
        {
            // 在此示例中，“视野内”仅表示在距离内。
            var distToCamera = math.distance(transform.Position, CamPos);
            var newWithinLoadRange = distToCamera < LoadRadius;
            if (dec.withinLoadRange && !newWithinLoadRange)
            {
                dec.withinLoadRange = false;
                dec.shouldRender = false;
                dec.loaded = false;
                dec.mesh.Release();
                dec.material.Release();
            }
            else if (!dec.withinLoadRange && newWithinLoadRange)
            {
                dec.withinLoadRange = true;
                dec.mesh.LoadAsync();
                dec.material.LoadAsync();
            }

            dec.withinLoadRange = newWithinLoadRange;
            if (newWithinLoadRange)
            {
                if (!dec.loaded)
                {
                    dec.loaded = dec.material.LoadingStatus >= ObjectLoadingStatus.Completed &&
                                 dec.mesh.LoadingStatus >= ObjectLoadingStatus.Completed;
                }

                dec.shouldRender = distToCamera < LoadRadius * .25f ||
                                   math.distance(transform.Position, CamPos + CamForward * LoadRadius) < LoadRadius;
            }
        }
    }
}
