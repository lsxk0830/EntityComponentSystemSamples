using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Streaming.SceneManagement.CompleteSample
{
    // System 会将图块中的所有 entities 移动/定向到正确的位置/旋转。
    [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
    public partial struct TileOffsetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TileOffset>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var offsetQuery = SystemAPI.QueryBuilder().WithAll<TileOffset>().Build();
            var offsets = offsetQuery.ToComponentDataArray<TileOffset>(Allocator.Temp);
            state.EntityManager.DestroyEntity(offsetQuery);

            foreach (var offset in offsets)
            {
                var rotation = quaternion.AxisAngle(new float3(0f, 1f, 0f), offset.Rotation);
                var offsetTransform = LocalTransform.FromPositionRotation(offset.Offset, rotation);

                // 将偏移和旋转应用于所有动态 entities
                foreach (var transform in
                         SystemAPI.Query<RefRW<LocalTransform>>()
                             .WithNone<Parent>())
                {
                    transform.ValueRW = offsetTransform.TransformTransform(transform.ValueRW);
                }

                var offsetMatrix = float4x4.TRS(offset.Offset, rotation, new float3(1f, 1f, 1f));

                // 将偏移和旋转应用于所有非动态 entities
                foreach (var transform in
                         SystemAPI.Query<RefRW<LocalToWorld>>()
                             .WithNone<Parent, LocalTransform>())
                {
                    transform.ValueRW.Value = math.mul(offsetMatrix, transform.ValueRW.Value);
                }
            }
        }
    }
}
