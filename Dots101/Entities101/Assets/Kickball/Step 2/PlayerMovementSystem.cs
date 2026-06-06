using Tutorials.Kickball.Execute;
using Tutorials.Kickball.Step1;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Tutorials.Kickball.Step2
{
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovement>();
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            // 获取定向输入。（大多数 UnityEngine.Input 与 Burst 兼容，但并非全部都是。
            // 如果 OnUpdate、OnCreate 或 OnDestroy 方法需要访问托管对象或调用方法
            // 与 Burst 不兼容的，可以省略 [BurstCompile] 属性。
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");
            var input = new float3(horizontal, 0, vertical) * SystemAPI.Time.DeltaTime * config.PlayerSpeed;

            // 如果这一帧没有方向输入，我们就不需要移动玩家。
            if (input.Equals(float3.zero))
            {
                return;
            }

            var minDist = config.ObstacleRadius + 0.5f; // 玩家胶囊半径为 0.5f
            var minDistSQ = minDist * minDist;

            // 对于每个具有 LocalTransform 和播放器 component 的 entity，对
            // LocalTransform 被分配给“playerTransform”。
            foreach (var playerTransform in
                     SystemAPI.Query<RefRW<LocalTransform>>()
                         .WithAll<Player>())
            {
                var newPos = playerTransform.ValueRO.Position + input;

                // 一个 foreach query 嵌套在另一个 foreach query 中。
                // 对于每个具有 LocalTransform 和障碍物 component 的 entity，只读引用
                // LocalTransform 被分配给“obstacleTransform”。
                foreach (var obstacleTransform in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Obstacle>())
                {
                    // 如果新位置与玩家相交，则不要移动玩家。
                    if (math.distancesq(newPos, obstacleTransform.ValueRO.Position) <= minDistSQ)
                    {
                        newPos = playerTransform.ValueRO.Position;
                        break;
                    }
                }

                playerTransform.ValueRW.Position = newPos;
            }
        }
    }
}
