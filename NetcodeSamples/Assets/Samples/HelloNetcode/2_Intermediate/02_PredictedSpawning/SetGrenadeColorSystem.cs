using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Samples.HelloNetcode
{
    /// <summary>
    /// 设置手榴弹的颜色，使其在红色和绿色之间交替。
    /// Note 我们必须在这里执行此操作（对于所有新手榴弹）：
    /// - 它可能不是我们产生的。
    /// - 如果我们在 predicted 代码中执行此操作，则在呈现之前可能无法设置（导致球在一个渲染帧内为黑色）。
    /// - 我们可能无法预测它的生成（因此它本质上是来自我们的 POV 的新 ghost）。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [BurstCompile]
    public partial struct SetGrenadeColorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrenadeSpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Change 过滤器确保我们仅在 GrenadeData component 更改时设置此颜色，这种情况只会发生一次（当它生成时）。
            foreach (var (urpColorRw, grenadeDataRo) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, RefRO<GrenadeData>>().WithChangeFilter<GrenadeData>())
            {
                urpColorRw.ValueRW.Value = grenadeDataRo.ValueRO.SpawnId % 2 == 1
                    ? new float4(1, 0, 0, 1)
                    : new float4(0, 1, 0, 1);
            }
        }
    }
}
