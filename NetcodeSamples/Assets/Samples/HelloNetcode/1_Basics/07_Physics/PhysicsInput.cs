using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.NetCode;
using Unity.NetCode.Samples.Common;

namespace Samples.HelloNetcode
{
    [UpdateInGroup(typeof(HelloNetcodeInputSystemGroup))]
    public partial class SamplePhysicsInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnablePhysics>();
            RequireForUpdate<PhysicsPlayerInput>();
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<PhysicsPlayerInput>(out var localInputEntity))
                return;

            var input = default(PhysicsPlayerInput);

            // Note client 当前正在模拟的勾号，它附加到
            // 命令并发送到 server，当
            // 命令已部署。
            input.Tick = SystemAPI.GetSingleton<NetworkTime>().InputTargetTick;

            if (UnityEngine.Input.GetKey("left") || TouchInput.GetKey(TouchInput.KeyCode.Left))
                input.Horizontal -= 1;
            if (UnityEngine.Input.GetKey("right") || TouchInput.GetKey(TouchInput.KeyCode.Right))
                input.Horizontal += 1;
            if (UnityEngine.Input.GetKey("down") || TouchInput.GetKey(TouchInput.KeyCode.Down))
                input.Vertical -= 1;
            if (UnityEngine.Input.GetKey("up") || TouchInput.GetKey(TouchInput.KeyCode.Up))
                input.Vertical += 1;

            // 即使没有需要按下的按键，命令也需要在采样的每一帧发送
            // 发送到 server（所有值均为 0）。这些命令确实获取 ghost snapshot 信息
            // 嵌入到它们中，这就是为什么当没有输入要发送时不能跳过它们。
            var inputBuffer = EntityManager.GetBuffer<PhysicsPlayerInput>(localInputEntity);
            inputBuffer.AddCommandData(input);
        }
    }

    // 输入处理不过是 run 中的 PredictedPhysicsSystemGroup 而不是
    // PredictionSystemGroup 像平常一样。这确保模拟正确
    // 当 prediction 运行时，为每个刻度构建并步进。
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial class PhysicsInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnablePhysics>();
            RequireForUpdate<PhysicsPlayerInput>();
        }

        protected override void OnUpdate()
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            // 允许物理 entity 移动的速度会影响它的外观
            // 与其他物理 entities 发生碰撞。还取决于物理步骤
            // 帧率频率。
            float speed = 3f;
            foreach(var (vel, inputBuffer) in SystemAPI.Query<
                        RefRW<PhysicsVelocity>, DynamicBuffer<PhysicsPlayerInput>>().WithAll<Simulate>())
            {
                inputBuffer.GetDataAtTick(tick, out var input);
                float3 dir = default;
                if (input.Horizontal > 0)
                    dir.x += 1;
                if (input.Horizontal < 0)
                    dir.x -= 1;
                if (input.Vertical > 0)
                    dir.z += 1;
                if (input.Vertical < 0)
                    dir.z -= 1;
                if (math.lengthsq(dir) > 0.5)
                {
                    dir = math.normalize(dir);
                    dir *= speed;
                }

                vel.ValueRW.Linear.x = dir.x;
                vel.ValueRW.Linear.z = dir.z;
            }
        }
    }
}
