using System;
using System.Diagnostics;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using Unity.NetCode.Samples.Common;

namespace Asteroids.Client
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class InputSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_Barrier;

        protected override void OnCreate()
        {
            m_Barrier = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate<NetworkStreamInGame>();
            RequireForUpdate<NetworkId>();
            // 只是为了确保这个 system 不会在其他 scenes 中出现 run
            RequireForUpdate<LevelComponent>();
        }

        struct InputJob : IJob
        {
            public byte left, right, thrust, shoot;
            public EntityCommandBuffer commandBuffer;
            public BufferLookup<ShipCommandData> inputFromEntity;
            public NetworkTick inputTargetTick;
            public Entity targetEntity;

            public void Execute()
            {
                if (targetEntity == Entity.Null)
                {
                    if (shoot != 0)
                    {
                        var req = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent<PlayerSpawnRequest>(req);
                        commandBuffer.AddComponent(req, new SendRpcCommandRequest());
                    }
                }
                else
                {
                    // 如果发货，则将命令存储在网络命令缓冲区中
                    if (inputFromEntity.HasBuffer(targetEntity))
                    {
                        var input = inputFromEntity[targetEntity];
                        input.AddCommandData(new ShipCommandData{Tick = inputTargetTick, left = left, right = right, thrust = thrust, shoot = shoot});
                    }
                }
            }
        }

        protected override void OnUpdate()
        {
            byte left, right, thrust, shoot;
            left = right = thrust = shoot = 0;

            if (Input.GetKey("left") || TouchInput.GetKey(TouchInput.KeyCode.Left))
                left = 1;
            if (Input.GetKey("right") || TouchInput.GetKey(TouchInput.KeyCode.Right))
                right = 1;
            if (Input.GetKey("up") || TouchInput.GetKey(TouchInput.KeyCode.Up))
                thrust = 1;
            if (Input.GetKey("space") || TouchInput.GetKey(TouchInput.KeyCode.Space))
                shoot = 1;

            var commandBuffer = m_Barrier.CreateCommandBuffer();
            var inputFromEntity = GetBufferLookup<ShipCommandData>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var inputTargetTick = networkTime.InputTargetTick;

            // 单例和启用不能很好地混合。
            // https://jira.unity3d.com/browse/DOTS-9695
            // https://unity.slack.com/archives/CE7DZN2H1/p1699385984519549
            // SystemAPI.TryGetSingletonEntity<GhostOwnerIsLocal>(out var targetEntity); // <-- doesn't work, enableable not supported for singletons.
            Entity targetEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<GhostOwnerIsLocal>>().WithAll<ShipCommandData>().WithEntityAccess())
            {
                if (targetEntity != Entity.Null) throw new Exception("Sanity check failed! More than once instance!");
                targetEntity = entity;
            }
            // var targetEntity = SystemAPI.QueryBuilder().WithAll<ShipCommandData, GhostOwnerIsLocal>().Build().GetSingletonEntity(); // <-- can't work
            // SystemAPI.TryGetSingletonEntity<ShipCommandData>(out var targetEntity); // could do this in binary world mode, but can't on single world since now client systems execute in a server world which contains all ship commands components.
            Dependency = new InputJob()
            {
                left = left,
                right = right,
                thrust = thrust,
                shoot = shoot,
                commandBuffer = commandBuffer,
                inputFromEntity = inputFromEntity,
                inputTargetTick = inputTargetTick,
                targetEntity = targetEntity,
            }.Schedule(Dependency);
            m_Barrier.AddJobHandleForProducer(Dependency);
        }
    }
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class ThinInputSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_Barrier;
        private int m_FrameCount;

        protected override void OnCreate()
        {
            m_Barrier = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate<NetworkStreamInGame>();
            // 只是为了确保这个 system 不会在其他 scenes 中出现 run
            RequireForUpdate<LevelComponent>();

            // 给每一个瘦 client 一些随机性。
            var rand = Unity.Mathematics.Random.CreateFromIndex((uint) Stopwatch.GetTimestamp());
            m_FrameCount = rand.NextInt(100);
        }

        struct ThinInputJob : IJob
        {
            public byte left, right, thrust, shoot;
            public EntityCommandBuffer commandBuffer;
            public BufferLookup<ShipCommandData> inputFromEntity;
            public NetworkTick inputTargetTick;
            public Entity targetEntity;
            public void Execute()
            {
                if (shoot != 0)
                {
                    // 对薄 clients 进行特殊处理，因为我们无法判断船舶是否已生成
                    var req = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<PlayerSpawnRequest>(req);
                    commandBuffer.AddComponent(req, new SendRpcCommandRequest());
                }
                // 如果发货，则将命令存储在网络命令缓冲区中
                if (inputFromEntity.HasBuffer(targetEntity))
                {
                    var input = inputFromEntity[targetEntity];
                    input.AddCommandData(new ShipCommandData{Tick = inputTargetTick, left = left, right = right, thrust = thrust, shoot = shoot});
                }
            }
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingleton<CommandTarget>(out var commandTarget))
            {
                if (commandTarget.targetEntity == Entity.Null)
                {
                    // 没有生成 ghosts，因此我们需要创建一个占位符输入 component 来存储命令。
                    // 如果瘦 client 超时并重新连接，我们需要确保尚未创建它。
                    if (!SystemAPI.TryGetSingletonEntity<ShipCommandData>(out var ent))
                    {
                        ent = EntityManager.CreateEntity();
                        EntityManager.AddBuffer<ShipCommandData>(ent);
                    }
                    SystemAPI.SetSingleton(new CommandTarget{targetEntity = ent});
                }
            }

            byte left, right, thrust, shoot;
            left = right = thrust = shoot = 0;

            // 生成并生成一些随机输入
            var state = (int) SystemAPI.Time.ElapsedTime % 3;
            if (state == 0)
                left = 1;
            else
                thrust = 1;
            ++m_FrameCount;
            if (m_FrameCount % 100 == 0)
            {
                shoot = 1;
                m_FrameCount = 0;
            }

            var commandBuffer = m_Barrier.CreateCommandBuffer();
            var inputFromEntity = GetBufferLookup<ShipCommandData>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var inputTargetTick = networkTime.ServerTick;
            SystemAPI.TryGetSingletonEntity<ShipCommandData>(out var targetEntity);
            Dependency = new ThinInputJob()
            {
                left = left,
                right = right,
                thrust = thrust,
                shoot = shoot,
                commandBuffer = commandBuffer,
                inputFromEntity = inputFromEntity,
                inputTargetTick = inputTargetTick,
                targetEntity = targetEntity,
            }.Schedule(Dependency);
            m_Barrier.AddJobHandleForProducer(Dependency);
            m_Barrier.AddJobHandleForProducer(Dependency);
        }
    }
}
