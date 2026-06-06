#if !UNITY_DISABLE_MANAGED_COMPONENTS

using Unity.Collections;
using Unity.Entities;

namespace Streaming.SceneManagement.CompleteSample
{
    // 将 PostLoadCommandBuffer 添加到元 entities 部分。
    [UpdateAfter(typeof(TileLoadingSystem))]
    partial struct RequestPostLoadSystem : ISystem
    {
        // 无法进行 Burst 编译，因为它使用 PostLoadCommandBuffer。
        public void OnUpdate(ref SystemState state)
        {
            var requiresQuery = SystemAPI.QueryBuilder().WithAll<RequiresPostLoadCommandBuffer>().Build();
            var entities = requiresQuery.ToEntityArray(Allocator.Temp);
            var requires = requiresQuery.ToComponentDataArray<RequiresPostLoadCommandBuffer>(Allocator.Temp);

            state.EntityManager.AddComponent<PostLoadCommandBuffer>(requiresQuery);
            for (int index = 0; index < entities.Length; ++index)
            {
                var buf = new PostLoadCommandBuffer();
                buf.CommandBuffer = new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.MultiPlayback);

                // 使用 subscene 偏移和旋转创建 entity
                var postLoadEntity = buf.CommandBuffer.CreateEntity();
                buf.CommandBuffer.AddComponent(postLoadEntity, new TileOffset
                {
                    Offset = requires[index].Position,
                    Rotation = requires[index].Rotation
                });

                state.EntityManager.SetComponentData(entities[index], buf);
            }

            state.EntityManager.RemoveComponent<RequiresPostLoadCommandBuffer>(requiresQuery);
        }
    }
}

#endif
