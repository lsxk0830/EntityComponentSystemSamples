using System;
using System.Collections.Generic;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using UnityEngine;

public struct UserWorldSingleton : IComponentData {}

public class UserWorldCreator : MonoBehaviour
{
    class UserWorldCreatorBaker : Baker<UserWorldCreator>
    {
        public override void Bake(UserWorldCreator authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<UserWorldSingleton>(entity);
        }
    }
}

// 我们在这里检查非默认索引 worlds 中的事件是：
// 1. 适当举起
// 2.不干扰默认的 world 事件（反之亦然）
// 这样做是为了确保在使用 CustomPhysicsSystemGroup API 时正确保存和恢复模拟
// Note: 我们仅使用 trigger 事件，因为 CollisionEvents 不会从 Havok 上停用的机构中引发，因此无法进行测试。
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class UserPhysicsGroup : CustomPhysicsSystemGroup
{
    public UserPhysicsGroup() : base(1, true) {}

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<UserWorldSingleton>();
    }

    protected override void AddExistingSystemsToUpdate(List<Type> systems)
    {
        systems.Add(typeof(TriggerEventsCountSystem_InPhysicsGroup));
    }
}

// 代表 systems 更新顺序的枚举，我们试图在此 scene 中检查它。用于出现问题时的调试目的。
public enum SystemUpdateOrderEnum
{
    BeforePhysicsGroup,
    InPhysicsGroup,
    AfterPhysicsGroup
}

public partial struct CheckEventCountJob : IJob
{
    public NativeReference<int> EventCount;

    public int WorldIndex;
    public SystemUpdateOrderEnum UpdateOrder;

    public void Execute()
    {
        // 在尸体落地之前，事件为零。
        // 6 和 12 表示 scene 中每个 world 动态主体的数量，我们预计每个主体一个事件。
        if (WorldIndex == 0)
        {
            Assert.IsTrue(EventCount.Value == 6 || EventCount.Value == 0, $"In {UpdateOrder} the event count is not matching! Expected 0 or 6, but got {EventCount.Value}. World Index 0.");
        }

        if (WorldIndex == 1)
        {
            Assert.IsTrue(EventCount.Value == 12 || EventCount.Value == 0, $"In {UpdateOrder} the event count is not matching! Expected 0 or 12, but got {EventCount.Value}. World Index 1.");
        }
    }
}

public partial struct CountTriggerEventsJob : ITriggerEventsJob
{
    public NativeReference<int> EventCount;

    public void Execute(TriggerEvent triggerEvent)
    {
        EventCount.Value++;
    }
}

#region Trigger event systems

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct TriggerEventsCountSystem_BeforePhysicsGroup : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UserWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        NativeReference<int> eventCount = new NativeReference<int>(0, Allocator.TempJob);

        state.Dependency = new CountTriggerEventsJob { EventCount = eventCount }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        state.Dependency = new CheckEventCountJob { EventCount = eventCount, UpdateOrder = SystemUpdateOrderEnum.BeforePhysicsGroup, WorldIndex = (int)SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex.Value }.Schedule(state.Dependency);
        state.Dependency = eventCount.Dispose(state.Dependency);
    }
}

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[UpdateBefore(typeof(ExportPhysicsWorld))]
public partial struct TriggerEventsCountSystem_InPhysicsGroup : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UserWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        NativeReference<int> eventCount = new NativeReference<int>(0, Allocator.TempJob);

        state.Dependency = new CountTriggerEventsJob { EventCount = eventCount }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        state.Dependency = new CheckEventCountJob { EventCount = eventCount, UpdateOrder = SystemUpdateOrderEnum.InPhysicsGroup, WorldIndex = (int)SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex.Value }.Schedule(state.Dependency);
        state.Dependency = eventCount.Dispose(state.Dependency);
    }
}

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct TriggerEventsCountSystem_AfterPhysicsGroup : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<UserWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        NativeReference<int> eventCount = new NativeReference<int>(0, Allocator.TempJob);

        state.Dependency = new CountTriggerEventsJob { EventCount = eventCount }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        state.Dependency = new CheckEventCountJob { EventCount = eventCount, UpdateOrder = SystemUpdateOrderEnum.AfterPhysicsGroup, WorldIndex = (int)SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex.Value }.Schedule(state.Dependency);
        state.Dependency = eventCount.Dispose(state.Dependency);
    }
}

#endregion
