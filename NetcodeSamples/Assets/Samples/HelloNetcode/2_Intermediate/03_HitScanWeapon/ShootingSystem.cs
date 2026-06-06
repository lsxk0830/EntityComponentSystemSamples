using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ShootingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<Hit>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var collisionHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>();
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var ghostComponentFromEntity = SystemAPI.GetComponentLookup<GhostInstance>();
            var localToWorldFromEntity = SystemAPI.GetComponentLookup<LocalToWorld>();
            var lagCompensationEnabledFromEntity = SystemAPI.GetComponentLookup<LagCompensationEnabled>();
            var predictingTick = networkTime.ServerTick;
            // 回滚时不执行 hit-scan，仅在模拟最新 tick 时执行
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            foreach (var (character, interpolationDelay, hitComponent)
                     in SystemAPI.Query<CharacterAspect, RefRO<CommandDataInterpolationDelay>, RefRW<Hit>>().WithAll<Simulate>())
            {
                if (character.Input.SecondaryFire.IsSet)
                {
                    hitComponent.ValueRW.Victim = character.Self;
                    hitComponent.ValueRW.Tick = predictingTick;
                    continue;
                }
                if (!character.Input.PrimaryFire.IsSet)
                {
                    continue;
                }

                // 当我们获取 ServerTick T 的 CollisionWorld 时，我们需要考虑到用户
                // 在上一个刻度的某个时间（技术上，渲染帧）引发了此输入。
                const int additionalRenderDelay = 1;

                // 时间细分：
                // - 在 client 上，预测 ServerTick：100（例如）
                // - InterpolationDelay：2 个刻度
                // - 渲染延迟（假设）：1 个刻度（可能超过 1，因为：双/三缓冲、管道、监视器刷新和绘制延迟）
                // - Client 视觉上看到 97（-1 表示渲染延迟，-2 表示延迟补偿）
                // - CommandDataInterpolationTick.Delay 是 CurrentCommand.Tick 与 InterpolationTick 之间的增量，因此为 -2。
                //   I.e。InterpolationDelay 已计入。
                // - 在 server 上，我们在 ServerTick:100 上处理此输入。
                // - CommandDataInterpolationTick.Delay:-2 = 98 (-2)
                // - 因此，server 还需要减去渲染延迟，以与 client 看到的内容和查询的内容保持一致 (97)。
                var delay = lagCompensationEnabledFromEntity.HasComponent(character.Self)
                    ? interpolationDelay.ValueRO.Delay + additionalRenderDelay
                    : additionalRenderDelay;

                collisionHistory.GetCollisionWorldFromTick(predictingTick, delay, ref physicsWorld, out var collWorld, out var expectedTick, out var returnedTick);
                var didClamp = expectedTick != returnedTick; // 调用 GetCollisionWorldFromTick 时，ClientWorld 不应被钳位！
                if(state.WorldUnmanaged.IsClient()) UnityEngine.Debug.Assert(!didClamp);


                var cameraRotation = math.mul(quaternion.RotateY(character.Input.Yaw), quaternion.RotateX(-character.Input.Pitch));
                var offset = math.rotate(cameraRotation, CharacterControllerCameraSystem.k_CameraOffset);
                var cameraPosition = character.Transform.ValueRO.Position + offset;
                var forward = math.mul(cameraRotation, math.forward());
                var rayInput = new RaycastInput
                {
                    Start = cameraPosition + forward,
                    End = cameraPosition + forward * 1000,
                    Filter = CollisionFilter.Default
                };
                bool hit = collWorld.CastRay(rayInput, out var closestHit);

                if (!hit)
                {
                    continue;
                }

                var hitEntity = Entity.Null;
                var hitPoint = closestHit.Position;
                if (ghostComponentFromEntity.HasComponent(closestHit.Entity))
                {
                    hitEntity = closestHit.Entity;

                    var localToWorld = localToWorldFromEntity[hitEntity].Value;
                    hitPoint = math.mul(math.inverse(localToWorld), new float4(hitPoint, 1)).xyz;

                    if (netDebug.LogLevel == NetDebug.LogLevelType.Debug)
                    {
                        netDebug.DebugLog($"[{state.WorldUnmanaged.Name}] Logged HIT on {predictingTick.ToFixedString()} (expected:{expectedTick.ToFixedString()}, actual/returned:{returnedTick.ToFixedString()}) with victim at worldPos:{collWorld.Bodies[closestHit.RigidBodyIndex].WorldFromBody.pos}!");
                    }
                }

                hitComponent.ValueRW.Victim = hitEntity;
                hitComponent.ValueRW.HitPoint = hitPoint;
                hitComponent.ValueRW.Tick = predictingTick;
            }
        }
    }
}
