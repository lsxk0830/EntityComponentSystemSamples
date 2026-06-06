using Tutorials.Kickball.Execute;
using Tutorials.Kickball.Step1;
using Tutorials.Kickball.Step2;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Tutorials.Kickball.Step3
{
    // 这个 UpdateBefore 对于确保球被渲染是必要的
    // 它们生成的帧的正确位置。
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BallSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BallSpawner>();
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            if (!Input.GetKeyDown(KeyCode.Return))
            {
                return;
            }

            var rand = new Random(123);

            // 对于每个玩家，生成一个球，将其放置在玩家的位置，并赋予它随机速度。
            foreach (var transform in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Player>())
            {
                var ball = state.EntityManager.Instantiate(config.BallPrefab);
                state.EntityManager.SetComponentData(ball, new LocalTransform
                {
                    Position = transform.ValueRO.Position,
                    Rotation = quaternion.identity,
                    Scale = 1
                });
                state.EntityManager.SetComponentData(ball, new Velocity
                {
                    // NextFloat2Direction() returns a random 2d unit vector.
                    Value = rand.NextFloat2Direction() * config.BallStartVelocity
                });
            }
        }
    }
}
