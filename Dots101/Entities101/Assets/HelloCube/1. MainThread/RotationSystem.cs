using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.MainThread
{
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 如果存在带 ExecuteMainThread 的实体，RotationSystem.OnUpdate() 会运行。
            // 如果不存在，RotationSystem.OnUpdate() 不会运行。
            // 系统本身还在，只是每帧更新被跳过
            state.RequireForUpdate<ExecuteMainThread>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 循环遍历每个具有 LocalTransform component 和 RotationSpeed component 的 entity。
            // 在每次迭代中，transform 都会被分配一个对 LocalTransform 的读写引用，
            // 速度被分配给 RotationSpeed component 的只读参考。
            foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
            {
                // ValueRW 和 ValueRO 均返回对实际 component 值的引用。
                // 不同之处在于，ValueRW 对读写访问进行安全检查，而
                // ValueRO 对只读访问进行安全检查。
                transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadiansPerSecond * deltaTime);
            }
        }
    }
}
