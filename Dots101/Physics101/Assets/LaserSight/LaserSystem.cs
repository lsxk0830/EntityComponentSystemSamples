using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace LaserSight
{
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial struct LaserSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaserSight.Config>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        // 我们无法 Burst 编译此更新，因为它访问托管对象
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            // 移动玩家
            {
                float3 input = new float3(Input.GetAxis($"Horizontal"), 0, Input.GetAxis($"Vertical"));
                var speed = config.PlayerMoveSpeed * SystemAPI.Time.DeltaTime;
                foreach (var playerTransform in
                         SystemAPI.Query<RefRW<LocalTransform>>()
                             .WithAll<Player>())
                {
                    playerTransform.ValueRW.Position += input * speed;
                }
            }

            float laserLength = 0;

            // raycast 确定激光长度
            {
                // 要执行光线投射或其他碰撞查询，我们需要碰撞 world
                var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

                foreach (var playerTransform in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Player>())
                {
                    var raycast = new RaycastInput
                    {
                        // 指定 raycast 的起点和终点（它们一起表示方向）
                        Start = playerTransform.ValueRO.Position,
                        End = playerTransform.ValueRO.Position + new float3(0, 0, config.MaxLaserLength),
                        // 别忘了设置过滤器，否则你将得不到任何点击！
                        Filter = CollisionFilter.Default
                    };

                    if (collisionWorld.CastRay(raycast, out var closestHit))
                    {
                        // 将激光长度设置为最近命中的距离
                        laserLength = math.distance(playerTransform.ValueRO.Position, closestHit.Position);
                    }
                    else
                    {
                        // 未检测到命中，因此只需将激光设置为最大长度
                        laserLength = config.MaxLaserLength;
                    }
                }
            }

            // 设置激光端点
            {
                foreach (var (playerTransform, player) in
                         SystemAPI.Query<RefRW<LocalTransform>, RefRW<Player>>())
                {
                    // 初始化激光参考
                    if (!player.ValueRW.Laser.IsValid())
                    {
                        player.ValueRW.Laser = GameObject.FindFirstObjectByType<LineRenderer>();
                    }

                    var laser = player.ValueRO.Laser.Value;
                    laser.SetPosition(0, playerTransform.ValueRO.Position);
                    laser.SetPosition(1, playerTransform.ValueRO.Position + new float3(0, 0, laserLength));
                }
            }
        }
    }
}