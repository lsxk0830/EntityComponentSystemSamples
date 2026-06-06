using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct TeleportObjectSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TeleportObject>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new TeleportObjectJob().Schedule(state.Dependency);
    }

    public void OnDestroy(ref SystemState state) {}

    // 当掉落的球体到达 EndingPosition 时，job 将其传送回 StartingPosition。
    // 线速度重置为零，以便球体不会下落得太快
    [BurstCompile]
    private partial struct TeleportObjectJob : IJobEntity
    {
        private void Execute(ref LocalTransform localTransform, ref TeleportObject teleport, ref PhysicsVelocity velocity)
        {
            if (localTransform.Position.y < teleport.EndingPosition.y)
            {
                localTransform.Position = teleport.StartingPosition + new float3(0, 7, 0);
                velocity.Linear = float3.zero;
                velocity.Angular = float3.zero;
            }
        }
    }
}
