using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Blender
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct BuoyancyZoneSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<BuoyancyZone>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 禁用所有立方体的浮力
            var buoyancyQuery = SystemAPI.QueryBuilder().WithAll<Buoyancy>().Build();
            state.EntityManager.SetComponentEnabled<Buoyancy>(buoyancyQuery, false);

            // 对于浮力区内的浮力立方体，复制浮力属性并启用浮力
            {
                // 获取 trigger 事件
                var sim = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();
                sim.FinalJobHandle.Complete();

                foreach (var triggerEvent in sim.TriggerEvents)
                {
                    Entity cubeEntity;
                    Entity zoneEntity;

                    // 确定哪个物体是有浮力的立方体，哪个是区域
                    if (SystemAPI.HasComponent<Buoyancy>(triggerEvent.EntityA) &&
                        SystemAPI.HasComponent<BuoyancyZone>(triggerEvent.EntityB))
                    {
                        cubeEntity = triggerEvent.EntityA;
                        zoneEntity = triggerEvent.EntityB;
                    }
                    else if (SystemAPI.HasComponent<Buoyancy>(triggerEvent.EntityB) &&
                             SystemAPI.HasComponent<BuoyancyZone>(triggerEvent.EntityA))
                    {
                        cubeEntity = triggerEvent.EntityB;
                        zoneEntity = triggerEvent.EntityA;
                    }
                    else
                    {
                        // 跳过，因为此事件不适用于立方体和区域
                        continue;
                    }

                    var zone = SystemAPI.GetComponentRW<BuoyancyZone>(zoneEntity);
                    var cubeBuoyancy = SystemAPI.GetComponentRW<Buoyancy>(cubeEntity);

                    // 将区域的浮力数据复制到立方体
                    cubeBuoyancy.ValueRW = zone.ValueRO.Buoyancy;

                    // 启用立方体的浮力，使其通过 BuoyancySystem 漂浮
                    SystemAPI.SetComponentEnabled<Buoyancy>(cubeEntity, true);
                }
            }
        }
    }
}