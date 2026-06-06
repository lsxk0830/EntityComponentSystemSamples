// 该脚本用于 5g1。更改碰撞过滤器 - 盒子演示。该脚本实例化了一个
// 9 个 prefab 盒子的 3x3 网格，带有独特的 colliders。从 PhysicsShape 创建 3 个盒子，其中
// 启用强制独特切换，使用强制独特切换从 PhysicsShape 创建 3 个盒子
// 添加了强制唯一 Component 后禁用，最后 3 个框是从 BoxCollider 创建的
// 添加了 Force Unique Component。
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.Physics
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ColliderGridCreationSystem : SystemBase
    {
        private EntityQuery m_ColliderQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<CreateColliderGridComponent>();
            m_ColliderQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(CreateColliderGridComponent),
                },
            });
        }

        protected override void OnStartRunning()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entityManager = World.EntityManager;

            var entity = m_ColliderQuery.ToEntityArray(Allocator.TempJob);

            // 从烘焙的 authoring component 中抓取数据
            var data = entityManager.GetComponentData<CreateColliderGridComponent>(entity[0]);
            var prefabBuiltIn = data.BuiltInEntity;
            var prefabPhysicsShapeToggle = data.PhysicsShapeToggleEntity;
            var prefabPhysicsShapeComponent = data.PhysicsShapeComponentEntity;
            var startingPosition = data.SpawningPosition;

            var entitiesPhysicsShapeComponent = new NativeArray<Entity>(3, Allocator.Temp);
            var entitiesPhysicsShapeToggle = new NativeArray<Entity>(3, Allocator.Temp);
            var entitiesBuiltIn = new NativeArray<Entity>(3, Allocator.Temp);

            entityManager.Instantiate(prefabPhysicsShapeComponent, entitiesPhysicsShapeComponent);
            entityManager.Instantiate(prefabPhysicsShapeToggle, entitiesPhysicsShapeToggle);
            entityManager.Instantiate(prefabBuiltIn, entitiesBuiltIn);

            // 使用 Physics 形状和 Force Unique Component 实例化 colliders
            var verticalOffset = 5f;
            var position = startingPosition + new float3(0f, verticalOffset, -5f);
            foreach (var e in entitiesPhysicsShapeComponent)
            {
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = position,
                    Scale = 1,
                    Rotation = quaternion.identity
                });
                position += new float3(0f, 0f, 5f);
            }

            // 使用 Physics 形状实例化 colliders 并启用 Force Unique 切换
            position = startingPosition + new float3(-3.53f, verticalOffset, -5f);
            foreach (var e in entitiesPhysicsShapeToggle)
            {
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = position,
                    Scale = 1,
                    Rotation = quaternion.identity
                });
                position += new float3(0f, 0f, 5f);
            }

            // 使用内置 Collider 和 Force Unique Component 实例化 colliders
            position = startingPosition + new float3(3.53f, verticalOffset, -5f);
            foreach (var e in entitiesBuiltIn)
            {
                ecb.SetComponent(e, new LocalTransform
                {
                    Position = position,
                    Scale = 1,
                    Rotation = quaternion.identity
                });
                position += new float3(0f, 0f, 5f);
            }

            entitiesPhysicsShapeComponent.Dispose();
            entitiesPhysicsShapeToggle.Dispose();
            entitiesBuiltIn.Dispose();
            entity.Dispose();

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        protected override void OnUpdate() {}

        protected override void OnDestroy() {}
    }
}
