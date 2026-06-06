using Tutorials.Kickball.Execute;
using Tutorials.Kickball.Step1;
using Tutorials.Kickball.Step2;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tutorials.Kickball.Step3
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BallMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BallMovement>();
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            // world 或其 SystemGroups 有时可能会覆盖当前时间或增量时间，
            // 由于各种原因，所以您应该使用 SystemAPI.Time 而不是 UnityEngine.Time。
            var dt = SystemAPI.Time.DeltaTime;

            var decayFactor = config.BallVelocityDecay * dt;
            var minDist = config.ObstacleRadius + 0.5f; // 球半径为 0.5f
            var minDistSQ = minDist * minDist;

            // 对于每个球 entity，我们需要读取并修改它的 LocalTransform 和 Velocity。
            foreach (var (ballTransform, velocity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<Velocity>>()
                         .WithAll<Ball>()
                         .WithDisabled<Carry>())  // 与第 5 步相关
            {
                // 如果球不动，就跳过它！
                if (velocity.ValueRO.Value.Equals(float2.zero))
                {
                    continue;
                }

                var magnitude = math.length(velocity.ValueRO.Value);
                var newPosition = ballTransform.ValueRW.Position +
                             new float3(velocity.ValueRO.Value.x, 0, velocity.ValueRO.Value.y) * dt;

                // 检查球是否与障碍物相交。如果是这样，则反映球的速度矢量。
                foreach (var obstacleTransform in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Obstacle>())
                {
                    if (math.distancesq(newPosition, obstacleTransform.ValueRO.Position) <= minDistSQ)
                    {
                        newPosition = DeflectBall(ballTransform.ValueRO.Position, obstacleTransform.ValueRO.Position,
                            velocity, magnitude, dt);

                        // 只要障碍物间隔开，就不可能
                        // 对于一个球在一个帧中击中两个障碍物，所以我们可以
                        // 检测到与一个障碍物发生碰撞后断开。
                        break;
                    }
                }

                ballTransform.ValueRW.Position = newPosition;

                // 速度衰减。
                var newMagnitude = math.max(magnitude - decayFactor, 0);
                velocity.ValueRW.Value = math.normalizesafe(velocity.ValueRO.Value) * newMagnitude;
            }
        }

        private float3 DeflectBall(float3 ballPos, float3 obstaclePos, RefRW<Velocity> velocity, float magnitude, float dt)
        {
            var obstacleToBallVector = math.normalize((ballPos - obstaclePos).xz);
            velocity.ValueRW.Value = math.reflect(math.normalize(velocity.ValueRO.Value), obstacleToBallVector) * magnitude;
            return ballPos + new float3(velocity.ValueRO.Value.x, 0, velocity.ValueRO.Value.y) * dt;
        }
    }
}
