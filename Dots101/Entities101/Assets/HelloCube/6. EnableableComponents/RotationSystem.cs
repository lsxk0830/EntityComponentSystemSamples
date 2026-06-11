using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.EnableableComponents
{
    public partial struct RotationSystem : ISystem
    {
        float m_Timer;
        const float k_Interval = 1.3f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Timer = k_Interval;
            state.RequireForUpdate<ExecuteEnableableComponents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            m_Timer -= deltaTime;

            // 切换每个 RotationSpeed 的启用状态
            if (m_Timer < 0)
            {
                foreach (var rotationSpeedEnabled in SystemAPI.Query<EnabledRefRW<RotationSpeed>>()
                             .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)) // 该查询将匹配所有具有 RotationSpeed component 的 entities，无论它们是否启用。
                {
                    rotationSpeedEnabled.ValueRW = !rotationSpeedEnabled.ValueRO;
                }

                m_Timer = k_Interval;
            }

            // query 仅匹配启用了 RotationSpeed 的 entities。
            foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
            {
                transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadiansPerSecond * deltaTime);
            }
        }
    }
}
