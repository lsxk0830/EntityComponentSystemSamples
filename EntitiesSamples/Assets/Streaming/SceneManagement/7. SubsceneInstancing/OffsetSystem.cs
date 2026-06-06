using Streaming.SceneManagement.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Streaming.SceneManagement.SubsceneInstancing
{
    // 加载 subscene 实例后，此 system 将其 entities 移动该实例的偏移量。
    // 在加载的 entities 移动到主 world 之前，此 system 在单独的 world 中运行。
    [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
    public partial struct OffsetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Offset>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var offsetQuery = SystemAPI.QueryBuilder().WithAll<Offset>().Build();
            var offsets = offsetQuery.ToComponentDataArray<Offset>(Allocator.Temp);
            state.EntityManager.DestroyEntity(offsetQuery);

            foreach (var offset in offsets)
            {
                // 将偏移应用于所有动态 entities
                foreach (var transform in
                         SystemAPI.Query<RefRW<LocalTransform>>())
                {
                    transform.ValueRW.Position += offset.Value;
                }

                // 将偏移量应用于所有非动态 entities
                var offsetMatrix = float4x4.Translate(offset.Value);
                foreach (var transform in
                         SystemAPI.Query<RefRW<LocalToWorld>>()
                             .WithNone<LocalTransform>())
                {
                    transform.ValueRW.Value = math.mul(offsetMatrix, transform.ValueRW.Value);
                }

                // 将偏移应用到振荡中心
                foreach (var oscillating in
                         SystemAPI.Query<RefRW<Oscillating>>())
                {
                    oscillating.ValueRW.Center += offset.Value;
                }
            }
        }
    }
}
