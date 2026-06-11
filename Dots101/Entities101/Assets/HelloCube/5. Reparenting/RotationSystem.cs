using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.Reparenting
{
    /// <summary>
    /// 只旋转 RotatingCube根立方体
    /// </summary>
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExecuteReparenting>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
            {
                transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadiansPerSecond * deltaTime);
            }
        }
    }
}
