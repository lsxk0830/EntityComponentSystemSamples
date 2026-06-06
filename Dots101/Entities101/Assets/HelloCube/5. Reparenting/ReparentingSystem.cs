using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HelloCube.Reparenting
{
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
            if (m_Timer > 0)
            {
                return;
            }
            m_Timer = k_Interval;

            var rotatorEntity = SystemAPI.GetSingletonEntity<RotationSpeed>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (m_Attached)
            {
                // 通过从子项中删除父项 component，将所有子项与旋转器分离。
                // （下次 TransformSystemGroup 更新时，它将更新子缓冲区并进行相应的转换。）

                DynamicBuffer<Child> children = SystemAPI.GetBuffer<Child>(rotatorEntity);
                for (int i = 0; i < children.Length; i++)
                {
                    // 此处使用 ECB 是最佳选择，因为调用 EntityManager.RemoveComponent()
                    // 相反会使 DynamicBuffer 无效，这意味着我们必须重新检索
                    // 每次 EntityManager.RemoveComponent() 调用后的 DynamicBuffer。
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
                // 通过将父 component 添加到立方体，将所有小立方体连接到旋转器。
                // （下次 TransformSystemGroup 更新时，它将更新子缓冲区并进行相应的转换。）

                foreach (var (transform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithNone<RotationSpeed>()
                             .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new Parent { Value = rotatorEntity });
                }

                // 替代上述循环的解决方案：
                // 将父值添加到与 query 匹配的所有 entities。
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
