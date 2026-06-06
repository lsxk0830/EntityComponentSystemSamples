using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HelloCube.Prefabs
{
    public partial struct FallAndDestroySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<ExecutePrefabs>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 旋转
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (transform, speed) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
            {
                // ValueRW 和 ValueRO 均返回对实际 component 值的引用。
                // 不同之处在于，ValueRW 对读写访问进行安全检查，而
                // ValueRO 对只读访问进行安全检查。
                transform.ValueRW = transform.ValueRO.RotateY(
                    speed.ValueRO.RadiansPerSecond * deltaTime);
            }

            // 从 EntityCommandBufferSystem.Singleton 创建的 EntityCommandBuffer 将是
            // 下次更新时由 EntityCommandBufferSystem 播放并处理。
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // 向下向量
            var movement = new float3(0, -SystemAPI.Time.DeltaTime * 5f, 0);

            // WithAll() includes RotationSpeed in the query, but
            // 将不会访问 RotationSpeed component 值。
            // WithEntityAccess() includes the Entity ID as the last element of the tuple.
            foreach (var (transform, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>>()
                         .WithAll<RotationSpeed>()
                         .WithEntityAccess())
            {
                transform.ValueRW.Position += movement;
                if (transform.ValueRO.Position.y < 0)
                {
                    // 进行结构性更改将使我们正在迭代的 query 无效，
                    // 因此，我们记录一个命令来稍后销毁 entity。
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}
