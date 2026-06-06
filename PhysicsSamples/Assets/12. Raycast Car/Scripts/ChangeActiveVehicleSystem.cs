using Unity.Collections;
using Unity.Entities;
using UnityEngine;

struct ActiveVehicle : IComponentData {}

[RequireMatchingQueriesForUpdate]
partial struct ChangeActiveVehicleSystem : ISystem
{
    struct AvailableVehicle : ICleanupComponentData {}

    EntityQuery m_ActiveVehicleQuery;
    EntityQuery m_VehicleInputQuery;

    EntityQuery m_NewVehicleQuery;
    EntityQuery m_ExistingVehicleQuery;
    EntityQuery m_DeletedVehicleQuery;

    NativeList<Entity> m_AllVehicles;

    public void OnCreate(ref SystemState state)
    {
        m_ActiveVehicleQuery = state.GetEntityQuery(typeof(ActiveVehicle), typeof(Vehicle));
        m_VehicleInputQuery = state.GetEntityQuery(typeof(VehicleInput));
        m_NewVehicleQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<Vehicle>() },
            None = new[] { ComponentType.ReadOnly<AvailableVehicle>() }
        });
        m_ExistingVehicleQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<Vehicle>(), ComponentType.ReadOnly<AvailableVehicle>() }
        });
        m_DeletedVehicleQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<AvailableVehicle>() },
            None = new[] { ComponentType.ReadOnly<Vehicle>() }
        });

        m_AllVehicles = new NativeList<Entity>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        m_AllVehicles.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        // 如果车辆发生变化，则更新稳定的车辆列表
        if (m_NewVehicleQuery.CalculateEntityCount() > 0 || m_DeletedVehicleQuery.CalculateEntityCount() > 0)
        {
            state.EntityManager.AddComponent(m_NewVehicleQuery, typeof(AvailableVehicle));
            state.EntityManager.RemoveComponent<AvailableVehicle>(m_DeletedVehicleQuery);

            m_AllVehicles.Clear();
            using (var allVehicles = m_ExistingVehicleQuery.ToEntityArray(Allocator.TempJob))
                m_AllVehicles.AddRange(allVehicles);
        }

        // 如果没有车辆，则不执行任何操作
        if (m_AllVehicles.Length == 0)
            return;

        // 验证主动车辆单例
        var activeVehicle = Entity.Null;
        if (m_ActiveVehicleQuery.CalculateEntityCount() == 1)
            activeVehicle = m_ActiveVehicleQuery.GetSingletonEntity();
        else
        {
            using (var activeVehicles = m_ActiveVehicleQuery.ToEntityArray(Allocator.TempJob))
            {
                Debug.LogWarning(
                    $"Expected exactly one {nameof(VehicleAuthoring)} component in the scene to be marked {nameof(VehicleAuthoring.ActiveAtStart)}. " +
                    "First available vehicle is being set to active."
                );

                // 更喜欢第一辆被标记为活跃的车辆
                if (activeVehicles.Length > 0)
                {
                    activeVehicle = activeVehicles[0];
                    state.EntityManager.RemoveComponent<ActiveVehicle>(m_AllVehicles.AsArray());
                }
                // 否则使用找到的第一辆车
                else
                    activeVehicle = m_AllVehicles[0];

                state.EntityManager.AddComponent(activeVehicle, typeof(ActiveVehicle));
            }
        }

        // 如果没有要更换的车辆或没有输入要更换的车辆，则不执行任何其他操作
        if (m_AllVehicles.Length < 2)
            return;

        var input = m_VehicleInputQuery.GetSingleton<VehicleInput>();

        if (input.Change == 0)
            return;

        // 查找当前活动车辆的索引
        var activeVehicleIndex = 0;
        for (int i = 0, count = m_AllVehicles.Length; i < count; ++i)
        {
            if (m_AllVehicles[i] == activeVehicle)
                activeVehicleIndex = i;
        }

        // 如果活动车辆索引实际上已更改，则将活动车辆标签移至新车辆
        var numVehicles = m_AllVehicles.Length;
        var newVehicleIndex = ((activeVehicleIndex + input.Change) % numVehicles + numVehicles) % numVehicles;
        if (newVehicleIndex == activeVehicleIndex)
            return;

        state.EntityManager.RemoveComponent<ActiveVehicle>(m_AllVehicles.AsArray());
        state.EntityManager.AddComponent(m_AllVehicles[newVehicleIndex], typeof(ActiveVehicle));
    }
}
