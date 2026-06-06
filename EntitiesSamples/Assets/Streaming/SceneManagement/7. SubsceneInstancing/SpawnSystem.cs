#if !UNITY_DISABLE_MANAGED_COMPONENTS

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;

namespace Streaming.SceneManagement.SubsceneInstancing
{
    // 以方形网格图案生成 scene。
    public partial struct SpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Grid>();
        }

        // 无法编译 Burst，因为它使用 PostLoadCommandBuffer。
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            // 设置参数以指示我们要创建实例
            var loadParameters = new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.NewInstance
            };

            var gridQuery = SystemAPI.QueryBuilder().WithAll<Grid>().Build();
            var grids = gridQuery.ToComponentDataArray<Grid>(Allocator.Temp);

            for (int index = 0; index < grids.Length; index += 1)
            {
                // 相对于原点
                float2 centerOffset = -((grids[index].Size - 1) * grids[index].Spacing);
                centerOffset /= 2f;

                // 创建 subscene 实例。
                for (int i = 0; i < grids[index].Size; ++i)
                {
                    for (int j = 0; j < grids[index].Size; ++j)
                    {
                        var sceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged,
                            grids[index].Scene, loadParameters);

                        // PostLoadCommandBuffer 包装将执行命令的 EntityCommandBuffer
                        // 加载 subscene 实例后。
                        var buf = new PostLoadCommandBuffer();
                        buf.CommandBuffer = new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.MultiPlayback);

                        // 加载单元格后，创建一个 entity 来保存单元格的偏移量。
                        var postLoadEntity = buf.CommandBuffer.CreateEntity();
                        buf.CommandBuffer.AddComponent(postLoadEntity, new Offset
                        {
                            Value = new float3(
                                grids[index].Spacing.x * i + centerOffset.x,
                                0f,
                                grids[index].Spacing.y * j + centerOffset.y)
                        });

                        state.EntityManager.AddComponentData(sceneEntity, buf);
                    }
                }
            }
        }
    }
}

#endif
