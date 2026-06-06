using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct UpdateConnectionPositionSystem : ISystem
    {
        private EntityQuery m_NetworkIdsWithoutGhostConnectionPositionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnableImportance>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .WithNone<GhostConnectionPosition>();
            m_NetworkIdsWithoutGhostConnectionPositionQuery = state.EntityManager.CreateEntityQuery(builder);
            // Note: CreateEntityQuery 确保我们 run 这个 system 即使我们没有任何 entities 匹配这个 query (i.e.clients)，
            // 这允许 EnableImportance 标志仍然有效。
        }

        public void OnUpdate(ref SystemState state)
        {
            // Note: 这是在 OnUpdate 中处理的，因为我们不能保证 EnableImportance 单例在 OnCreate 期间存在，因为它是通过子 scene 启用的。
            var enableImportance = SystemAPI.GetSingleton<EnableImportance>();
            var hasEnabledImportanceScaling = SystemAPI.TryGetSingletonEntity<GhostImportance>(out var existingImportanceSingletonEntity);
            if (enableImportance.Enabled != hasEnabledImportanceScaling)
            {
                if (enableImportance.Enabled)
                {
                    var grid = state.EntityManager.CreateSingleton(enableImportance.TilingConfiguration);
                    state.EntityManager.AddComponentData(grid, new GhostImportance
                    {
                        BatchScaleImportanceFunction = enableImportance.UseBatchedImportanceFunction ? GhostDistanceImportance.BatchScaleFunctionPointer : default,
                        GhostConnectionComponentType = ComponentType.ReadOnly<GhostConnectionPosition>(),
                        GhostImportanceDataType = ComponentType.ReadOnly<GhostDistanceData>(),
                        GhostImportancePerChunkDataType = ComponentType.ReadOnly<GhostDistancePartitionShared>(),
                    });

                    // Note: 如果您 ALWAYS 希望启用“距离重要性缩放”功能：
                    // - 将上述代码移至 OnCreate 中是安全的。
                    // - 但是，您必须删除 EnableImportance component （和 Authoring），因为您无法通过 OnCreate 对其进行采样
                    // （因为子 scene 在构建中尚未加载）。

                    // 禁用自动添加重要性共享 component。
                    GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionSharedComponent = false;
                }
                else
                {
                    state.EntityManager.DestroyEntity(existingImportanceSingletonEntity);
                    GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionSharedComponent = true;
                }
            }

            if (enableImportance.Enabled)
            {
                state.EntityManager.AddComponent<GhostConnectionPosition>(m_NetworkIdsWithoutGhostConnectionPositionQuery);
                // Note: 在真实游戏中（假设您的 clients 角色控制器移动和/或旋转），
                // 您需要每帧更新 GhostConnectionPosition 值。
                // 在此示例中，角色控制器位于固定位置，恰好是默认位置（float3）。
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionSharedComponent = true;
        }
    }
}
