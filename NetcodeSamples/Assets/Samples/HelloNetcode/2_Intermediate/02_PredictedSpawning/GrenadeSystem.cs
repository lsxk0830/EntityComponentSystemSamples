using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Samples.HelloNetcode
{
    /// <summary>Denotes 和 entity 爆炸 ParticleSystem.</summary>
    public struct ExplosionParticleSystem : IComponentData
    {
    }

    /// <summary>Handles 手榴弹行为，当计时器耗尽时销毁并推近物理对象 away.</summary>
    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    [UpdateAfter(typeof(GrenadeLauncherSystem))]
    [BurstCompile]
    public partial struct GrenadeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GrenadeSpawner>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<GrenadeConfig>();
            state.RequireForUpdate<EnablePredictedSpawning>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var time = state.WorldUnmanaged.Time;
            var config = SystemAPI.GetSingleton<GrenadeConfig>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var explosionPrefab = SystemAPI.GetSingleton<GrenadeSpawner>().Explosion;
            var isServer = state.WorldUnmanaged.IsServer();

            foreach (var (data, grenadeTransform, grenade) in SystemAPI.Query<RefRO<GrenadeData>, RefRO<LocalTransform>>().WithAll<Simulate>().WithNone<DisableRendering>().WithEntityAccess())
            {
                // 当手榴弹到达计时器结束时销毁它
                if (time.ElapsedTime > data.ValueRO.DestroyTimer)
                {
                    // 计算哪些物体在手榴弹的爆炸半径内并应用爆炸效果
                    // 他们根据距离。手榴弹距离越远，受到爆炸的影响越小。
                    foreach (var (velocity, grenadeData, transform) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<GrenadeData>, RefRO<LocalTransform>>().WithAll<Simulate>())
                    {
                        var diff = transform.ValueRO.Position - grenadeTransform.ValueRO.Position;
                        var distanceSqrt = math.lengthsq(diff);
                        if (distanceSqrt < config.BlastRadius && distanceSqrt != 0)
                        {
                            var scaledPower = 1.0f - distanceSqrt / config.BlastRadius;
                            // 为爆炸添加一些垂直度，偏向 world.up。
                            diff.y = diff.y >= -0.05f ? math.max(config.BlastPowerClampY, diff.y) : math.min(-config.BlastPowerClampY, diff.y);
                            velocity.ValueRW.Linear = config.BlastPower * scaledPower * (diff / math.sqrt(distanceSqrt));

                            // 还会让它们发生“连锁反应”，因为它看起来很酷（但不是立即发生，除非它已经发生了）：
                            grenadeData.ValueRW.DestroyTimer = math.min(grenadeData.ValueRW.DestroyTimer, (float)time.ElapsedTime + config.ChainReactionForceExplodeDurationSeconds);
                        }
                    }

                    if (isServer)
                    {
                        // 销毁 server 上的手榴弹：
                        commandBuffer.DestroyEntity(grenade);
                    }
                    else
                    {
                        // 在 client 上生成爆炸 VFX：
                        if (networkTime.IsFirstTimeFullyPredictingTick)
                        {
                            var explosion = commandBuffer.Instantiate(explosionPrefab);
                            commandBuffer.SetComponent(explosion, LocalTransform.FromPosition(grenadeTransform.ValueRO.Position));
                            commandBuffer.AddComponent<ExplosionParticleSystem>(explosion);
                            // 隐藏它，这样做可以防止重新触发（请参阅上面的 query 过滤器）。
                            commandBuffer.AddComponent<DisableRendering>(grenade);
                        }
                    }
                }
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }

    /// <summary>Destroy 过期爆炸颗粒 systems.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct ExplosionSystem : ISystem
    {
        private EntityQuery m_ParticleSystemQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnablePredictedSpawning>();
            m_ParticleSystemQuery = state.GetEntityQuery(typeof(ParticleSystem), typeof(ExplosionParticleSystem));
            state.RequireForUpdate(m_ParticleSystemQuery);
        }

        // 不兼容突发，因为迭代 ParticleSystems！
        public void OnUpdate(ref SystemState state)
        {
            var time = state.WorldUnmanaged.Time;
            var particleSystemEntities = m_ParticleSystemQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in particleSystemEntities)
            {
                // Hack: 仅需要，因为 Entities 上的 ParticleSystem 不会自动自毁。
                var ps = state.EntityManager.GetComponentObject<ParticleSystem>(entity);
                Debug.Assert(ps.main.stopAction == ParticleSystemStopAction.Destroy);
                if (ps.time + (time.DeltaTime * 2) > ps.main.duration)
                    state.EntityManager.DestroyEntity(entity);
            }
        }
    }

    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    // 处理榴弹发射器的旋转（上/下），根据需要在 client 和 server 上运行
    // 找出手榴弹的生成点
    [BurstCompile]
    public partial struct GrenadeLauncherSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnablePredictedSpawning>();
            state.RequireForUpdate<NetworkId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (character, anchorPoint) in SystemAPI.Query<CharacterAspect, RefRO<AnchorPoint>>().WithAll<Simulate>())
            {
                // 这是武器槽和旋转，将使发射器正确移动（它固定在末端）
                var grenadeLauncher = anchorPoint.ValueRO.WeaponSlot;
                var followCameraRotation = quaternion.RotateX(-character.Input.Pitch);

                var transform = state.EntityManager.GetComponentData<LocalTransform>(grenadeLauncher);
                commandBuffer.SetComponent(grenadeLauncher, transform.WithRotation(followCameraRotation));

            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}
