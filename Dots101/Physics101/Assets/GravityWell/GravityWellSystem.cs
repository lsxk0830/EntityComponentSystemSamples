using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace GravityWell
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct GravityWellSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();
            var dt = SystemAPI.Time.DeltaTime;

            // 移动重力井
            foreach (var (wellTransform, well) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<GravityWell>>())
            {
                // 以原点为中心绕一圈
                well.ValueRW.OrbitPos += config.WellOrbitSpeed * dt;
                math.sincos(well.ValueRW.OrbitPos, out var s, out var c);
                wellTransform.ValueRW.Position = new float3(c, 0, s) * config.WellOrbitRadius;
            }

            var wellQuery = SystemAPI.QueryBuilder().WithAll<GravityWell, LocalTransform>().Build();
            var wellTransforms = wellQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            foreach (var (velocity, collider,
                         mass, ballTransform) in
                     SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<PhysicsCollider>,
                         RefRO<PhysicsMass>, RefRO<LocalTransform>>())
            {
                for (int i = 0; i < wellTransforms.Length; i++)
                {
                    var wellTransform = wellTransforms[i];

                    velocity.ValueRW.ApplyExplosionForce(
                        mass.ValueRO,
                        collider.ValueRO,
                        ballTransform.ValueRO.Position,  // 身体的位置
                        ballTransform.ValueRO.Rotation,    // 身体的旋转
                        -config.WellStrength, // 负面力量使这成为内爆
                        wellTransform.Position,   // 爆炸位置
                        // 爆炸半径为 0 意味着影响范围是无限的
                        // 并且力量不会随着距离而减弱
                        0,
                        dt,
                        math.up());
                }
            }
        }
    }
}