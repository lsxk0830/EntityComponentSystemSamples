using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace StickmanDrop
{
    // system 在碰撞检测和求解器的每次迭代后运行
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct StickmanSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<Breakable>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 获取脉冲事件
            var sim = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();

            // 要访问主线程上的脉冲事件，我们必须同步任何出色的物理模拟 jobs
            sim.FinalJobHandle.Complete();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 当脉冲超过某个值时，就会生成脉冲事件
            // joint 的断裂力或断裂扭矩值。

            foreach (var impulseEvent in sim.ImpulseEvents)
            {
                // 为 joint 连接的两个主体生成脉冲事件。
                // 所以 DestroyEntity 将为每个 joint 调用两次，但这不是问题
                // 因为在单个 ECB 中对同一 entity 执行多个销毁命令不是错误。
                ecb.DestroyEntity(impulseEvent.JointEntity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}