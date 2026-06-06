using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;

namespace ActivationPlates
{
    // system 在碰撞检测和求解器之后运行
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct ActivationSystem : ISystem
    {
        public ulong physicsUpdateCount; // 每次物理更新都会增加

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<ActivationPlates.Config>();
            physicsUpdateCount = 1; // 从 1 开始，以防止在第一次更新时生成错误的退出区域状态
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();
            var elapsedTime = SystemAPI.Time.ElapsedTime;

            physicsUpdateCount++;

            // 对于触发此更新事件的区域，将其状态设置为“内部”或“进入”
            {
                // 获取 trigger 事件
                var sim = SystemAPI.GetSingleton<SimulationSingleton>().AsSimulation();
                sim.FinalJobHandle.Complete();

                foreach (var triggerEvent in sim.TriggerEvents)
                {
                    Entity playerEntity;
                    Entity zoneEntity;

                    // 确定哪个身体是玩家，哪个是区域
                    if (SystemAPI.HasComponent<Player>(triggerEvent.EntityA) &&
                        SystemAPI.HasComponent<Zone>(triggerEvent.EntityB))
                    {
                        playerEntity = triggerEvent.EntityA;
                        zoneEntity = triggerEvent.EntityB;
                    }
                    else if (SystemAPI.HasComponent<Player>(triggerEvent.EntityB) &&
                             SystemAPI.HasComponent<Zone>(triggerEvent.EntityA))
                    {
                        playerEntity = triggerEvent.EntityB;
                        zoneEntity = triggerEvent.EntityA;
                    }
                    else
                    {
                        // 跳过，因为此事件不适合玩家和区域
                        continue;
                    }

                    var zone = SystemAPI.GetComponentRW<Zone>(zoneEntity);
                    zone.ValueRW.LastPhysicsUpdateCount = physicsUpdateCount;  // 跟踪上次进入该区域的时间

                    if (zone.ValueRO.State == ZoneState.Enter)
                    {
                        // 是 Enter，所以现在应该是 Inside
                        zone.ValueRW.State = ZoneState.Inside;
                    }
                    else if (zone.ValueRO.State == ZoneState.Exit ||
                             zone.ValueRO.State == ZoneState.Outside)
                    {
                        // 之前是 Exit 或 Outside，所以现在应该是 Enter
                        zone.ValueRW.State = ZoneState.Enter;
                    }
                }
            }

            // 对于在此更新中执行 NOT trigger 事件的区域，将其状态设置为“退出”或“外部”
            {
                foreach (var zone in
                         SystemAPI.Query<RefRW<Zone>>())
                {
                    if (zone.ValueRO.LastPhysicsUpdateCount == physicsUpdateCount)
                    {
                        // 跳过，因为此更新此区域生成了 trigger 事件
                        continue;
                    }

                    if (physicsUpdateCount - zone.ValueRO.LastPhysicsUpdateCount == 1)
                    {
                        // 在之前的更新中触发了事件，但在本次更新中未触发
                        zone.ValueRW.State = ZoneState.Exit;
                    }
                    else
                    {
                        zone.ValueRW.State = ZoneState.Outside;
                    }
                }
            }

            // 将区域颜色设置为进入时为绿色，退出时为红色
            {
                foreach (var (zone, color) in
                         SystemAPI.Query<RefRW<Zone>, RefRW<URPMaterialPropertyBaseColor>>())
                {
                    if (zone.ValueRO.State == ZoneState.Enter)
                    {
                        color.ValueRW.Value = config.ActiveColor;
                    }
                    else if (zone.ValueRO.State == ZoneState.Exit)
                    {
                        color.ValueRW.Value = config.InactiveColor;
                    }
                }
            }

            // 可能会产生一个盒子（取决于区域状态和区域类型）
            {
                var spawnBox = false;

                foreach (var zone in
                         SystemAPI.Query<RefRW<Zone>>())
                {
                    var type = zone.ValueRO.Type;
                    var zoneState = zone.ValueRO.State;

                    if (type == ZoneType.OneTime && zoneState == ZoneState.Enter)
                    {
                        // 如果之前没有输入过
                        if (zone.ValueRO.LastTriggerTime == 0)
                        {
                            spawnBox = true;
                            zone.ValueRW.LastTriggerTime = (float)elapsedTime;
                        }
                    }
                    else if (type == ZoneType.Continuous && zoneState == ZoneState.Inside)
                    {
                        // 如果自上次 trigger 以来已经过去了足够的时间
                        if (elapsedTime - zone.ValueRO.LastTriggerTime > config.ContinuousRepetitionInterval)
                        {
                            spawnBox = true;
                            zone.ValueRW.LastTriggerTime = (float)elapsedTime;
                        }
                    }
                    else if (type == ZoneType.Reenterable && zoneState == ZoneState.Enter)
                    {
                        spawnBox = true;
                    }
                    else if (type == ZoneType.OnExit && zoneState == ZoneState.Exit)
                    {
                        spawnBox = true;
                    }
                }

                if (spawnBox)
                {
                    state.EntityManager.Instantiate(config.SpawnPrefab);
                }
            }
        }
    }
}