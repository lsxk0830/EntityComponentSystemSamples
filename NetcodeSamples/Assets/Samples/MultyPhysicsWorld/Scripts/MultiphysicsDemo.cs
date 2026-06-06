using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Systems;

namespace Samples.MultyPhysicsWorld
{
    // RPC 从 client 向 server 请求游戏进入“游戏中”并发送 snapshots / 输入
    public struct GoInGameRequest : IRpcCommand
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class VisualizationPhysicsSystemGroup : CustomPhysicsSystemGroup
    {
        public VisualizationPhysicsSystemGroup() : base(1, true)
        {}

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<PhysicsSpawner>();
        }
    }

    // 当 client 与网络 id 连接时，进入游戏并告诉 server 也进入游戏
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class GoInGameClientSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsSpawner>();
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<NetworkId>(), ComponentType.Exclude<NetworkStreamInGame>()));
        }

        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach(var (_, ent) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(ent);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<GoInGameRequest>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = ent });
            };
            commandBuffer.Playback(EntityManager);
        }
    }

    // 当 server 收到进入游戏请求时，进入游戏并删除请求
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class GoInGameServerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsSpawner>();
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<GoInGameRequest>(), ComponentType.ReadOnly<ReceiveRpcCommandRequest>()));
        }

        protected override void OnUpdate()
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var networkIdFromEntity = GetComponentLookup<NetworkId>(true);
            var spawner = SystemAPI.GetSingleton<PhysicsSpawner>();
            foreach(var (reqSrc, reqEnt) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                        .WithAll<GoInGameRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
                commandBuffer.DestroyEntity(reqEnt);

                var entity = commandBuffer.Instantiate(spawner.prefab);
                //将 entity 添加到连接链接的 entity 组中，这样当连接被破坏时，ghost 也会被删除
                var linkedEntityGroups = commandBuffer.AddBuffer<LinkedEntityGroup>(reqSrc.ValueRO.SourceConnection);
                linkedEntityGroups.Add(entity);

                commandBuffer.AddBuffer<PlayerInput>(entity);

                commandBuffer.SetComponent(entity, LocalTransform.FromPosition(new float3(0.0f, 1.0f, 0.0f)));

                commandBuffer.SetComponent(entity, new GhostOwner { NetworkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection].Value });
                commandBuffer.SetComponent(reqSrc.ValueRO.SourceConnection, new CommandTarget { targetEntity = entity });
            };
            commandBuffer.Playback(EntityManager);
        }
    }

    public struct PlayerInput : ICommandData
    {
        public NetworkTick Tick {get; set;}
        public int horizontal;
        public int vertical;
        public int rotation;

        public FixedString512Bytes ToFixedString() => $"h:{horizontal},v:{vertical},rot:{rotation}";
    }

    // 当 server 收到进入游戏请求时，进入游戏并删除请求
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class ImputSampling : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsSpawner>();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnUpdate()
        {
            var localInput = SystemAPI.GetSingleton<CommandTarget>().targetEntity;
            if (localInput == Entity.Null)
            {
                var localPlayerId = SystemAPI.GetSingleton<NetworkId>().Value;
                var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
                var connection = SystemAPI.GetSingletonEntity<CommandTarget>();
                foreach(var (ghostOwner, ent) in SystemAPI.Query<RefRO<GhostOwner>>()
                            .WithNone<PlayerInput>()
                            .WithEntityAccess())
                {
                    if (ghostOwner.ValueRO.NetworkId == localPlayerId)
                    {
                        commandBuffer.AddBuffer<PlayerInput>(ent);
                        commandBuffer.SetComponent(connection, new CommandTarget {targetEntity = ent});
                    }
                }
                commandBuffer.Playback(EntityManager);
                return;
            }
            var input = default(PlayerInput);
            input.Tick = SystemAPI.GetSingleton<NetworkTime>().InputTargetTick;
            if (Input.GetKey(KeyCode.LeftArrow))
                input.horizontal -= 1;
            if (Input.GetKey(KeyCode.RightArrow))
                input.horizontal += 1;
            if (Input.GetKey(KeyCode.DownArrow))
                input.vertical -= 1;
            if (Input.GetKey(KeyCode.UpArrow))
                input.vertical += 1;
            if (Input.GetKey(KeyCode.Space))
                input.rotation = 1;
            var inputBuffer = EntityManager.GetBuffer<PlayerInput>(localInput);
            inputBuffer.AddCommandData(input);
        }
    }

    // 当 server 收到进入游戏请求时，进入游戏并删除请求
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial class CubeFlySystem : SystemBase
    {
        [WithAll(typeof(Simulate))]
        partial struct CubeFlyJob : IJobEntity
        {
            public NetworkTick predictTick;
            public float deltaTime;

            void Execute(ref PhysicsVelocity physicsVelocity,
                ref LocalTransform transform,
                in PhysicsMass physicsMass,
                in DynamicBuffer<PlayerInput> playerInputs)
            {
                if(!playerInputs.GetDataAtTick(predictTick, out var input))
                    return;
                var force = new float3
                {
                    x = input.horizontal,
                    y = 0f,
                    z = input.vertical,
                };
                force = math.normalizesafe(force);
                force *= 10.0f;
                var impulse = force * deltaTime;
                var angularInpulse = new float3(0f, input.rotation, 0f) * deltaTime;
                physicsVelocity.ApplyLinearImpulse(physicsMass, impulse);
                physicsVelocity.ApplyAngularImpulse(physicsMass, angularInpulse);
                //迫使立方体距离地面 1 米
                //translation.Value.y = 0.7f + 0.1f*math.sin((float)(math.PI * predictTick * 1.0f/60f));
            }
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsSpawner>();
            RequireForUpdate<GhostCollection>();
        }

        protected override void OnUpdate()
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var predictTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            new CubeFlyJob() {
                predictTick = predictTick,
                deltaTime = deltaTime
            }.Schedule();
        }
    }

    public struct ParticleAge : IComponentData
    {
        public double deadTime;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ParticleEmitterSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem m_Barrier;
        private ComponentLookup<LocalTransform> m_TransformLookup;
        protected override void OnCreate()
        {
            m_Barrier = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            m_TransformLookup = GetComponentLookup<LocalTransform>(true);
            RequireForUpdate<PhysicsSpawner>();
            RequireForUpdate<GhostCollection>();
            RequireForUpdate<ParticleEmitter>();
        }

        partial struct UpdateAge : IJobEntity
        {
            void Execute()
            {

            }
        }

        partial struct UpdateParticles : IJobEntity
        {
            public double elapsedTime;
            public EntityCommandBuffer.ParallelWriter commandBuffer;
            [NativeSetThreadIndex] public int nativeThreadIndex;

            void Execute(Entity entity, ref ParticleAge age)
            {
                if (age.deadTime < elapsedTime)
                {
                    commandBuffer.DestroyEntity(nativeThreadIndex, entity);
                }
            }
        }

        partial struct EmitParticles : IJobEntity
        {
            public float deltaTime;
            public double elapsedTime;
            public EntityCommandBuffer.ParallelWriter commandBuffer;
            [ReadOnly] public ComponentLookup<LocalTransform> transformLookup;
            [NativeSetThreadIndex] public int nativeThreadIndex;

            void Execute(ref ParticleEmitter emitter, in LocalTransform transform)
            {
                emitter.accumulator += deltaTime * emitter.emissionRate + 0.5f;
                int particles = (int) emitter.accumulator;
                if (particles == 0)
                    return;
                emitter.accumulator -= particles;
                if(emitter.random.state == 0)
                    emitter.random.InitState(1023932191);
                while (--particles >= 0)
                {
                    // 创建第一个粒子，然后根据其值实例化其余粒子
                    var particle = commandBuffer.Instantiate(nativeThreadIndex, emitter.prefab);
                    float3 randomSpread = emitter.random.NextFloat3();
                    var originalScale = transformLookup[emitter.prefab];

                    var pos = new float3(transform.Position.x + randomSpread.x*3f, transform.Position.y, transform.Position.z + randomSpread.z);
                    commandBuffer.AddComponent(nativeThreadIndex, particle, new ParticleAge{deadTime = elapsedTime + emitter.particleAge});
                    commandBuffer.SetComponent(nativeThreadIndex, particle, LocalTransform.FromPositionRotationScale(
                        pos, transform.Rotation, originalScale.Scale));

                }
            }
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var commandBuffer = m_Barrier.CreateCommandBuffer().AsParallelWriter();
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            m_TransformLookup.Update(this);
            new UpdateParticles
            {
                elapsedTime = elapsedTime,
                commandBuffer = commandBuffer,
            }.ScheduleParallel();
            new EmitParticles()
            {
                deltaTime = deltaTime,
                elapsedTime = elapsedTime,
                commandBuffer = commandBuffer,
                transformLookup = m_TransformLookup
            }.Schedule();
            m_Barrier.AddJobHandleForProducer(Dependency);
        }
    }
}
