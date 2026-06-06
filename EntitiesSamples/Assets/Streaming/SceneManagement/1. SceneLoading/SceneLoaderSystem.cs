using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace Streaming.SceneManagement.SceneLoading
{
    public partial struct SceneLoaderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SceneReference>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 加载所有请求的 scenes 并从 entities 中删除请求
            var query = SystemAPI.QueryBuilder().WithAll<SceneReference>().Build();
            var requests = query.ToComponentDataArray<SceneReference>(Allocator.Temp);
            for (int i = 0; i < requests.Length; i += 1)
            {
                // 创建一个 entity 与 scene 相关的 components，稍后将加载 trigger scene
                // 在 scene 中加载 systems。
                // （因为此方法可能会将 component 添加到 entity，所以不能在 foreach query 中调用它。）
                SceneSystem.LoadSceneAsync(state.WorldUnmanaged, requests[i].Value);
            }
            state.EntityManager.DestroyEntity(query);
        }
    }
}
