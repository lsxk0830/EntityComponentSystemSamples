// 此代码的目的是构建一个图块网格（来自prefab）。每个图块都有一个 TileTriggerCounter component。
// 球体 GameObject 可以沿着网格移动。墙壁可以防止球体滚出网格。Trigger 事件
// 当球体与瓷砖碰撞时，记录在 TileTriggerCounter component 中。对 trigger 事件的反应
// 在 SpawnColliderFromTriggerSystem 中处理。
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    internal partial struct CreateTileGridSystem : ISystem
    {
        private EntityQuery wallQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CreateTileGridSpawnerComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entityManager = state.World.EntityManager;

            foreach (var(creator, creatorEntity) in
                     SystemAPI.Query<RefRW<CreateTileGridSpawnerComponent>>()
                         .WithEntityAccess())
            {
                var initialTransform = entityManager.GetComponentData<LocalTransform>(creator.ValueRO.GridEntity);

                int gridSize = 14; // 创建 14x14 网格
                var positions = ComputeGridPositions(gridSize, creator.ValueRO.SpawningPosition);

                var spawnedEntities = new NativeArray<Entity>(gridSize * gridSize, Allocator.Temp);

                ecb.Instantiate(creator.ValueRO.GridEntity, spawnedEntities);

                var i = 0;
                foreach (var s in spawnedEntities)
                {
                    ecb.SetComponent(s, new LocalTransform
                    {
                        Position = positions[i],
                        Scale = initialTransform.Scale,
                        Rotation = initialTransform.Rotation
                    });
                    i++;
                }

                // 实例化墙 entity。请注意，此 prefab 包含子 entities，因此我们不能
                // 更新本次传递的位置。需要单独的通行证才能执行此操作
                var wallPrefabInstance = ecb.Instantiate(creator.ValueRO.WallEntity);
                ecb.SetComponent(wallPrefabInstance, new LocalTransform
                {
                    Position = creator.ValueRO.SpawningPosition,
                    Scale = initialTransform.Scale,
                    Rotation = initialTransform.Rotation
                });
                ecb.AddComponent(wallPrefabInstance, new WallsTagComponent()); // 在墙上贴上标签，以便轻松找到下一个通行证 entity

                spawnedEntities.Dispose();
                positions.Dispose();
            }

            ecb.Playback(entityManager);
            ecb.Dispose();

            // 执行第二遍以更新墙 entity 的位置。需要使用第一个的输出
            // ECB 在这里播放。
            wallQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<WallsTagComponent>()
                },
            });

            var wallArray = wallQuery.ToEntityArray(Allocator.Temp);
            foreach (var wall in wallArray)
            {
                if (entityManager.HasBuffer<LinkedEntityGroup>(wall))
                {
                    var leg = entityManager.GetBuffer<LinkedEntityGroup>(wall);

                    if (leg.Length > 1)
                    {
                        for (var j = 1; j < leg.Length; j++)
                        {
                            var childEntity = leg[j].Value;
                            var childPosition = entityManager.GetComponentData<LocalTransform>(childEntity);
                            var wallPosition = entityManager.GetComponentData<LocalTransform>(wall);
                            entityManager.SetComponentData(childEntity, new LocalTransform
                            {
                                Position = wallPosition.Position + childPosition.Position + new float3(0, 1.5f, 0),
                                Scale = childPosition.Scale,
                                Rotation = childPosition.Rotation
                            });
                        }
                    }
                }
            }
            wallArray.Dispose();

            entityManager.DestroyEntity(SystemAPI.QueryBuilder().WithAll<CreateTileGridSpawnerComponent>().Build());
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        // 创建一个瓷砖网格。startingPoint 标记网格的中间
        internal static NativeList<float3> ComputeGridPositions(int gridSize, float3 startingPosition)
        {
            var arrayPositions = new NativeList<float3>(gridSize * gridSize, Allocator.Temp);
            int gridRadius = 1;
            var startingOffset = startingPosition -
                new float3(0.5f * gridSize * gridRadius, 0, 0.5f * gridSize * gridRadius) + 0.5f;

            for (int i = 0; i < gridSize; ++i)
            {
                for (int j = 0; j < gridSize; ++j)
                {
                    arrayPositions.Add(startingOffset + new float3(
                        i * gridRadius, 0, j * gridRadius));

                    if (arrayPositions.Length >= gridSize * gridSize) break;
                }
            }

            return arrayPositions;
        }
    }
}
