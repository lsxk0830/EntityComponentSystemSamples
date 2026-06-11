using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.GameObjectSync
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DirectoryManaged>();
            state.RequireForUpdate<ExecuteGameObjectSync>();
        }

        // 这个 OnUpdate 访问托管对象，因此无法进行突发编译。
        public void OnUpdate(ref SystemState state)
        {
            var directory = SystemAPI.ManagedAPI.GetSingleton<DirectoryManaged>();

            if (!directory.RotationToggle.isOn) return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed, go) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>, RotatorGO>())
            {
                transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadiansPerSecond * deltaTime);

                // 更新关联的 GameObject 的转换以匹配。
                go.Value.transform.rotation = transform.ValueRO.Rotation;
            }
        }
    }
#endif
}
