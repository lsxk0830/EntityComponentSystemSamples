using Streaming.SceneManagement.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Streaming.SceneManagement.CompleteSample
{
    // 此 system 计算到所有相关 entities 的最小平铺距离
    partial struct TileDistanceSystem : ISystem
    {
        private EntityQuery tilesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            tilesQuery = SystemAPI.QueryBuilder().WithAll<TileInfo, DistanceToRelevant>().Build();
            state.RequireForUpdate(tilesQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tiles = tilesQuery.ToComponentDataArray<TileInfo>(Allocator.Temp);
            NativeArray<float> distancesSq = new NativeArray<float>(tiles.Length, Allocator.Temp);

            // 计算从图块到最近的相关 entity 的距离
            {
                for (int index = 0; index < tiles.Length; ++index)
                {
                    distancesSq[index] = float.MaxValue;
                }

                foreach (var transform in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Relevant>())
                {
                    for (int index = 0; index < tiles.Length; ++index)
                    {
                        var pos = new float3(tiles[index].Position.x, 0f, tiles[index].Position.y);
                        var distance = transform.ValueRO.Position - pos;
                        distance.y = 0;
                        distancesSq[index] = math.min(math.lengthsq(distance), distancesSq[index]);
                    }
                }
            }

            // 复制距离
            tilesQuery.CopyFromComponentDataArray(distancesSq.Reinterpret<DistanceToRelevant>());
        }
    }
}
