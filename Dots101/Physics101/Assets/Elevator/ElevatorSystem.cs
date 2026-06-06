using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Elevator
{
    public partial struct ElevatorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Elevator>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (elevator, trans, velocity) in
                     SystemAPI.Query<RefRW<Elevator>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>>())
            {
                // 如果不动的话。..
                if (velocity.ValueRW.Linear.y == 0)
                {
                    // 上
                    velocity.ValueRW.Linear.y = elevator.ValueRO.Speed;
                }
                // 如果往上走。..
                else if (velocity.ValueRW.Linear.y > 0)
                {
                    // 如果打到顶部。..
                    if (trans.ValueRO.Position.y > elevator.ValueRO.MaxHeight)
                    {
                        // 下去
                        velocity.ValueRW.Linear.y = -elevator.ValueRO.Speed;
                    }
                }
                // 如果往下。..
                else
                {
                    // 如果触底。..
                    if (trans.ValueRO.Position.y < elevator.ValueRO.MinHeight)
                    {
                        // 上
                        velocity.ValueRW.Linear.y = elevator.ValueRO.Speed;
                    }
                }
            }
        }
    }
}