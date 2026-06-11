using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.Reparenting
{
    /// <summary>
    /// 每隔 0.7f 秒执行一次切换逻辑
    /// </summary>
    public partial struct ReparentingSystem : ISystem
    {
        bool m_Attached;
        float m_Timer;
        const float k_Interval = 0.7f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Timer = k_Interval;
            m_Attached = true;
            state.RequireForUpdate<ExecuteReparenting>();
            state.RequireForUpdate<RotationSpeed>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_Timer -= SystemAPI.Time.DeltaTime;

            if (m_Timer > 0) return;

            m_Timer = k_Interval;

            var rotatorEntity = SystemAPI.GetSingletonEntity<RotationSpeed>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (m_Attached)
            {
                DynamicBuffer<Child> children = SystemAPI.GetBuffer<Child>(rotatorEntity);
                for (int i = 0; i < children.Length; i++)
                {
                    ecb.RemoveComponent<Parent>(children[i].Value);
                }

                // 替代上述循环的解决方案：
                // 一次调用即可从数组中的所有 entities 中删除父级 component。
                // 由于该方法需要 NativeArray<Entity>，因此我们创建 DynamicBuffer 的 NativeArray<Entity> 别名。
                /*
                ecb.RemoveComponent<Parent>(children.AsNativeArray().Reinterpret<Entity>());
                */
            }
            else
            {
                //遍历所有“有 LocalTransform，但没有 RotationSpeed”的实体，并且同时拿到它们的 Entity 本身
                foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithNone<RotationSpeed>().WithEntityAccess())
                {
                    ecb.AddComponent(entity, new Parent { Value = rotatorEntity });
                }

                // 替代上述循环的解决方案：将父值添加到与 query 匹配的所有 entities。
                /*
                var query = SystemAPI.QueryBuilder().WithAll<LocalTransform>().WithNone<RotationSpeed>().Build();
                ecb.AddComponent(query, new Parent { Value = rotatorEntity });
                */
            }

            ecb.Playback(state.EntityManager);

            m_Attached = !m_Attached;
        }
    }
}
