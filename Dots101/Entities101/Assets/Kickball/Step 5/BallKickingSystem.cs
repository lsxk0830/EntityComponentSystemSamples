using Tutorials.Kickball.Execute;
using Tutorials.Kickball.Step1;
using Tutorials.Kickball.Step2;
using Tutorials.Kickball.Step3;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Tutorials.Kickball.Step5
{
    // UpdateBefore BallMovementSystem 使得球的运动受到同一帧中踢球的影响。
    [UpdateBefore(typeof(BallMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BallKickingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BallKicking>();
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            if (!Input.GetKeyDown(KeyCode.Space))
            {
                return;
            }

            // 对于每个球员，为踢球范围内的每个球添加撞击速度。
            foreach (var playerTransform in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Player>())
            {
                foreach (var (ballTransform, velocity) in
                         SystemAPI.Query<RefRO<LocalTransform>, RefRW<Velocity>>()
                             .WithAll<Ball>())
                {
                    float distSQ = math.distancesq(playerTransform.ValueRO.Position, ballTransform.ValueRO.Position);

                    if (distSQ <= config.BallKickingRangeSQ)
                    {
                        var playerToBall = ballTransform.ValueRO.Position.xz - playerTransform.ValueRO.Position.xz;
                        // Use normalizesafe() in case the ball and player are exactly on top of each other
                        // （这不太可能，但并非不可能）。
                        velocity.ValueRW.Value += math.normalizesafe(playerToBall) * config.BallKickForce;
                    }
                }
            }
        }
    }
}
