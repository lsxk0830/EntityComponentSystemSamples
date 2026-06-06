using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.Samples.Common;
using Unity.Transforms;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    // 每帧采样按键输入并将它们添加到输入 component
    // 稍后处理。
    [UpdateInGroup(typeof(HelloNetcodeInputSystemGroup))]
    [AlwaysSynchronizeSystem]
    public partial class GatherAutoCommandsSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnableSpawnPlayer>();
            RequireForUpdate<PlayerInput>();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            bool left = UnityEngine.Input.GetKey("left") || TouchInput.GetKey(TouchInput.KeyCode.Left);
            bool right = UnityEngine.Input.GetKey("right") || TouchInput.GetKey(TouchInput.KeyCode.Right);
            bool down = UnityEngine.Input.GetKey("down") || TouchInput.GetKey(TouchInput.KeyCode.Down);
            bool up = UnityEngine.Input.GetKey("up") || TouchInput.GetKey(TouchInput.KeyCode.Up);
            bool jump = UnityEngine.Input.GetKeyDown("space");

            // 当产生多个玩家时，此输入收集步骤可能全部
            // 因为它们有 PlayerInput，所以这将 query 限制为
            // 仅拥有 ghost 所有者 component 且 ID 与
            // 本地连接。为此，我们使用添加的 GhostOwnerIsLocal 标签
            // 自动分配给用户拥有的所有 entities。
            Dependency = new GatherAutoCommandJob()
            {
                left = left,
                right = right,
                down = down,
                up = up,
                jump = jump,
            }.ScheduleParallel(Dependency);
        }

        [WithAll(typeof(GhostOwnerIsLocal))]
        partial struct GatherAutoCommandJob : IJobEntity
        {
            public bool left;
            public bool right;
            public bool down;
            public bool up;
            public bool jump;

            public void Execute(ref PlayerInput inputData)
            {
                inputData = default;

                if (jump)
                    inputData.Jump.Set();
                if (left)
                    inputData.Horizontal -= 1;
                if (right)
                    inputData.Horizontal += 1;
                if (down)
                    inputData.Vertical -= 1;
                if (up)
                    inputData.Vertical += 1;
            }
        }
    }

    // 将存储在输入 component 中的输入应用于所有玩家 entities
    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    public partial class ProcessAutoCommandsSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnableSpawnPlayer>();
            RequireForUpdate<PlayerInput>();
        }

        protected override void OnUpdate()
        {
            var movementSpeed = SystemAPI.Time.DeltaTime * 3;
            SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();

            // 使跳跃弧看起来相同，无论模拟滴答率如何
            var velocityDecrementStep = 60 / tickRate.SimulationTickRate;

            foreach (var (input, transRef, movementRef) in SystemAPI.Query<PlayerInput, RefRW<LocalTransform>, RefRW<PlayerMovement>>())
            {
                ref var movement = ref movementRef.ValueRW;
                ref var trans = ref transRef.ValueRW;
                if (input.Jump.IsSet)
                    movement.JumpVelocity = 10;

                // 简单的跳跃机制，当设置跳跃事件时跳跃速度设置为 10
                // 然后在每个刻度上它都会递减。它导致输入值被设置
                // 向上或向下（就像向左/向右移动一样）。
                var verticalMovement = 0f;
                if (movement.JumpVelocity > 0)
                {
                    movement.JumpVelocity -= velocityDecrementStep;
                    verticalMovement = 1;
                }
                else
                {
                    if (trans.Position.y > 0)
                        verticalMovement = -1;
                }

                var moveInput = new float3(input.Horizontal, verticalMovement, input.Vertical);
                moveInput = math.normalizesafe(moveInput) * movementSpeed;

                // 确保着陆时我们不会穿过地面（并在接近时坚持下去）

                if (movement.JumpVelocity <= 0 && (trans.Position.y + moveInput.y < 0 || trans.Position.y + moveInput.y < 0.05))
                    moveInput.y = trans.Position.y = 0;
                trans.Position += new float3(moveInput.x, moveInput.y, moveInput.z);
            }
        }
    }
}
