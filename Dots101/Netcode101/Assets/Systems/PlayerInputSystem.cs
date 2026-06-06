using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KickBall
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var moveAction = InputSystem.actions.FindAction("Move");
            moveAction.Enable();
        }

        public void OnUpdate(ref SystemState state)
        {
            var moveAction = InputSystem.actions.FindAction("Move");
            var moveValue = moveAction.ReadValue<Vector2>();

            // WithAll<GhostOwnerIsLocal> 这样我们只修改本地 client 的输入缓冲区，而不修改其他 clients
            // （clients 接收彼此输入缓冲区的副本是可能的，有时也是有用的，但即使在
            // 在这些情况下，我们不想修改其他玩家输入缓冲区的副本）
            foreach (var input in SystemAPI.Query<RefRW<PlayerInput>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                input.ValueRW = default;

                // var moveAction = InputSystem.actions.FindAction("Move");
                // var moveValue = moveAction.ReadValue<Vector2>();
                //
                // Debug.Log(moveValue);

                input.ValueRW.Horizontal = moveValue.x;
                input.ValueRW.Vertical = moveValue.y;

                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.spaceKey.wasPressedThisFrame)
                    {
                        input.ValueRW.KickBall.Set();
                    }
                    if (keyboard.enterKey.wasPressedThisFrame)
                    {
                        input.ValueRW.SpawnBall.Set();
                    }
                }
            }
        }
    }
}
