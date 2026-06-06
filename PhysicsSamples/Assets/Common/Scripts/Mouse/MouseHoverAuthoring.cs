using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.GraphicsIntegration;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using static Unity.Physics.Extensions.PhysicsSamplesExtensions;

namespace Unity.Physics.Extensions
{
    public class MouseHover : IComponentData
    {
        public bool IgnoreTriggers;
        public bool IgnoreStatic;
        public Entity PreviousEntity;
        public Entity CurrentEntity;
        public Entity HoverEntity;
        public MaterialMeshInfo OriginalMeshInfo;
        public RenderMeshArray OriginalRenderMeshes;
    }

    [DisallowMultipleComponent]
    public class MouseHoverAuthoring : MonoBehaviour
    {
        public GameObject HoverPrefab;
        public bool IgnoreTriggers = true;
        public bool IgnoreStatic = true;

        // Note: 覆盖 OnEnable 以便能够在编辑器中禁用 component
        protected void OnEnable() {}
    }

    class MouseHoverBaker : Baker<MouseHoverAuthoring>
    {
        public override void Bake(MouseHoverAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponentObject(entity, new MouseHover()
            {
                PreviousEntity = Entity.Null,
                CurrentEntity = Entity.Null,
                IgnoreTriggers = authoring.IgnoreTriggers,
                IgnoreStatic = authoring.IgnoreStatic,
                HoverEntity = GetEntity(authoring.HoverPrefab, TransformUsageFlags.None),
            });
        }
    }

    // 将任何鼠标弹簧应用为 entity 运动 component 的速度变化
    // 限制：仅当 scene 中的物理对象来自与 MouseHoverAuthoring 相同的 subscene 时才有效
    // 如果有 Unity.Rendering API 可以让您获取 entity 用于渲染的 UnityEngine.Mesh，则可以修复。
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MouseHoverSystem : SystemBase
    {
        [BurstCompile]
        public struct WorldRaycastJob : IJob
        {
            public RaycastInput RayInput;
            [ReadOnly] public CollisionWorld CollisionWorld;
            [ReadOnly] public bool IgnoreTriggers;
            [ReadOnly] public bool IgnoreStatic;

            public NativeReference<RaycastHit> RaycastHitRef;

            public void Execute()
            {
                var mousePickCollector = new MousePickCollector(CollisionWorld.NumDynamicBodies)
                {
                    IgnoreTriggers = IgnoreTriggers,
                    IgnoreStatic = IgnoreStatic
                };

                if (CollisionWorld.CastRay(RayInput, ref mousePickCollector))
                {
                    RaycastHitRef.Value = mousePickCollector.Hit;
                }
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<MouseHover>();
        }

        // 查找包含 Physics 形状的图形表示的 Entity。
        // Physics 和 Graphics 表示可能位于同一 Entity 上。
        public Entity FindGraphicsEntityFromPhysics(Entity bodyEntity) => FindGraphicsEntityFromPhysics(bodyEntity, ColliderKey.Empty);
        public Entity FindGraphicsEntityFromPhysics(Entity bodyEntity, ColliderKey leafColliderKey)
        {
            if (bodyEntity.Equals(Entity.Null))
            {
                // 没有 Physics 所以没有 Graphics
                return Entity.Null;
            }

            // 将 Graphics Entity 设置为提供的 Physics Entity
            var renderEntity = bodyEntity;

            // 检查我们是否命中了叶节点
            if (!leafColliderKey.Equals(ColliderKey.Empty))
            {
                // 获取 Physics Collider
                var rootCollider = EntityManager.GetComponentData<PhysicsCollider>(bodyEntity).Value;

                // 如果我们命中 CompoundCollider，我们需要找到关联的原始 Entity
                // 被击中的实际叶子 Collider。
                if (rootCollider.Value.Type == ColliderType.Compound)
                {
                    #region Find a Leaf Entity and ColliderKey
                    var leafEntity = Entity.Null;
                    unsafe
                    {
                        var rootColliderPtr = rootCollider.AsPtr();

                        // 获取叶子 Collider 并检查我们击中的是否是 PolygonCollider（i.e。三角形或四边形）
                        rootColliderPtr->GetLeaf(leafColliderKey, out var childCollider);
                        leafEntity = childCollider.Entity;

                        // PolygonColliders 可能没有与其关联的原始 Entity
                        // 因此，如果我们有一个多边形并且它没有 Entity 那么我们确实需要检查
                        // 而是使用更高级别的网格或地形 Collider。
                        var childColliderType = childCollider.Collider->Type;
                        var childColliderIsPolygon = childColliderType == ColliderType.Triangle || childColliderType == ColliderType.Quad;
                        if (childColliderIsPolygon && childCollider.Entity.Equals(Entity.Null))
                        {
                            // 获取多边形父级的 ColliderKey
                            if (TryGetParentColliderKey(rootColliderPtr, leafColliderKey, out leafColliderKey))
                            {
                                // 获取多边形的网格或地形 Collider
                                TryGetChildInHierarchy(rootColliderPtr, leafColliderKey, out childCollider);
                                leafEntity = childCollider.Entity;
                            }
                        }
                    }
                    #endregion

                    // CompoundCollider 的叶子中记录的 Entities 可能是正确的
                    // 在转换时。但是，如果 Collider blob 已共享或出现
                    // 通过子 scene，我们不能假设烘焙的 Entities 在
                    // CompoundCollider 仍然有效。

                    // 使用 CompoundCollider 转换 Entities 时添加了额外的动态缓冲区
                    // 它包含 Entity/ColliderKey 对的列表。这个缓冲区应该被修补
                    // 自动并且对每个实例都有效，至少在你开始搞乱之前
                    // 与 Entity 层次结构自己 e.g。通过删除 Entities。

                    #region Check the Leaf Entity is valid
                    // 如果 leafEntity 从未被分配过
                    // 查找任何缓冲区是没有意义的。
                    if (!leafEntity.Equals(Entity.Null))
                    {
                        // 首先检查 Key/Entity 对缓冲区。
                        // 如果调用 Physics 转换管道，则该值应该存在。
                        var colliderKeyEntityPairBuffers = GetBufferLookup<PhysicsColliderKeyEntityPair>(true);
                        if (colliderKeyEntityPairBuffers.HasBuffer(bodyEntity))
                        {
                            var colliderKeyEntityBuffer = colliderKeyEntityPairBuffers[bodyEntity];
                            for (int i = 0; i < colliderKeyEntityBuffer.Length; i++)
                            {
                                var bufferColliderKey = colliderKeyEntityBuffer[i].Key;
                                if (leafColliderKey.Equals(bufferColliderKey))
                                {
                                    renderEntity = colliderKeyEntityBuffer[i].Entity;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // 我们还没有找到 Key/Entity 对缓冲区，因此复合 collider
                            // 可能是在代码中创建的。

                            // 我们假设 CompoundCollider 中的 Entity 有效
                            renderEntity = leafEntity;

                            // 如果此 CompoundCollider 是从 prefab 实例化的，则 entities
                            // 在复合子级中，实际上会引用原始的 prefab 层次结构。
                            var rootEntityFromLeaf = leafEntity;
                            while (SystemAPI.HasComponent<Parent>(rootEntityFromLeaf))
                            {
                                rootEntityFromLeaf = SystemAPI.GetComponent<Parent>(rootEntityFromLeaf).Value;
                            }

                            // 如果从叶子中找到的根 Entity 与主体 Entity 不匹配
                            // 然后我们使用相同的 CompoundCollider 命中了一个实例。
                            // 这意味着我们可以尝试将叶子 Entity 重新映射到新的层次结构。
                            if (!rootEntityFromLeaf.Equals(bodyEntity))
                            {
                                // 这假设原始和实例 Entity 上有一个 LinkedEntityGroup 缓冲区。
                                // 毫无疑问，有一种更优化的方法可以更具体地进行重新映射
                                // 了解最终应用。
                                var linkedEntityGroupBuffers = GetBufferLookup<LinkedEntityGroup>(true);

                                // 仅当缓冲区存在、已创建且长度相等时才重新映射。
                                bool hasBufferRootEntity = linkedEntityGroupBuffers.HasBuffer(rootEntityFromLeaf);
                                bool hasBufferBodyEntity = linkedEntityGroupBuffers.HasBuffer(bodyEntity);
                                if (hasBufferRootEntity && hasBufferBodyEntity)
                                {
                                    var prefabEntityGroupBuffer = linkedEntityGroupBuffers[rootEntityFromLeaf];
                                    var instanceEntityGroupBuffer = linkedEntityGroupBuffers[bodyEntity];

                                    if (prefabEntityGroupBuffer.IsCreated && instanceEntityGroupBuffer.IsCreated
                                        && (prefabEntityGroupBuffer.Length == instanceEntityGroupBuffer.Length))
                                    {
                                        var prefabEntityGroup = prefabEntityGroupBuffer.AsNativeArray();
                                        var instanceEntityGroup = instanceEntityGroupBuffer.AsNativeArray();

                                        for (int i = 0; i < prefabEntityGroup.Length; i++)
                                        {
                                            // 如果我们在 prefab 层次结构中找到了 renderEntity 索引，
                                            // 将 renderEntity 设置为实例中等效的 Entity
                                            if (prefabEntityGroup[i].Value.Equals(renderEntity))
                                            {
                                                renderEntity = instanceEntityGroup[i].Value;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
            }

            // 最后检查形状 Entity 上是否有图形重定向。
            if (SystemAPI.HasComponent<PhysicsRenderEntity>(renderEntity))
            {
                renderEntity = SystemAPI.GetComponent<PhysicsRenderEntity>(renderEntity).Entity;
            }

            // 如果在定位的渲染 entity 上找不到渲染信息，我们会尝试查找具有渲染信息的任何子级
            if (renderEntity != Entity.Null && !EntityManager.HasComponent<MaterialMeshInfo>(renderEntity))
            {
                // 此 entity 上没有渲染信息。尝试在层次结构中找到实际渲染 entity。
                if (EntityManager.HasBuffer<Child>(renderEntity))
                {
                    var children = EntityManager.GetBuffer<Child>(renderEntity);
                    foreach (var childElement in children)
                    {
                        // 找到第一个具有渲染信息的子项
                        if (EntityManager.HasComponent<MaterialMeshInfo>(childElement.Value))
                        {
                            renderEntity = childElement.Value;
                        }
                    }
                }
            }

            return renderEntity;
        }

        protected override void OnUpdate()
        {
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            Vector2 mousePosition = Input.mousePosition;
            UnityEngine.Ray unityRay = Camera.main.ScreenPointToRay(mousePosition);
            var rayInput = new RaycastInput
            {
                Start = unityRay.origin,
                End = unityRay.origin + unityRay.direction * MousePickSystem.k_MaxDistance,
                Filter = CollisionFilter.Default,
            };

            var mouseHover = SystemAPI.ManagedAPI.GetSingleton<MouseHover>();

            // 优雅地处理没有提供悬停 entity 来交换渲染材质的情况
            if (mouseHover.HoverEntity == Entity.Null)
                return;

            RaycastHit hit;
            using (var raycastHitRef = new NativeReference<RaycastHit>(Allocator.TempJob))
            {
                var rcj = new WorldRaycastJob()
                {
                    CollisionWorld = collisionWorld,
                    RayInput = rayInput,
                    IgnoreTriggers = mouseHover.IgnoreTriggers,
                    IgnoreStatic = mouseHover.IgnoreStatic,
                    RaycastHitRef = raycastHitRef
                };
                rcj.Run();
                hit = raycastHitRef.Value;
            }

            var graphicsEntity = FindGraphicsEntityFromPhysics(hit.Entity, hit.ColliderKey);

            // 如果仍然悬停在相同的 entity 上，则不执行任何操作。
            if (mouseHover.CurrentEntity.Equals(graphicsEntity)) return;

            mouseHover.PreviousEntity = mouseHover.CurrentEntity;
            mouseHover.CurrentEntity = graphicsEntity;

            bool hasPreviousEntity = !mouseHover.PreviousEntity.Equals(Entity.Null);
            bool hasCurrentEntity = !mouseHover.CurrentEntity.Equals(Entity.Null);

            if (hasPreviousEntity && EntityManager.HasComponent<MaterialMeshInfo>(mouseHover.PreviousEntity))
            {
                // 将渲染信息恢复为我们悬停在最后一个 entity 中的原始信息
                EntityManager.SetComponentData(mouseHover.PreviousEntity, mouseHover.OriginalMeshInfo);
                EntityManager.SetSharedComponentManaged(mouseHover.PreviousEntity, mouseHover.OriginalRenderMeshes);
            }

            if (hasCurrentEntity && EntityManager.HasComponent<MaterialMeshInfo>(mouseHover.CurrentEntity) && EntityManager.HasComponent<RenderMeshArray>(mouseHover.CurrentEntity))
            {
                mouseHover.PreviousEntity = mouseHover.CurrentEntity;
                mouseHover.CurrentEntity = graphicsEntity;
                mouseHover.OriginalMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(mouseHover.CurrentEntity);
                mouseHover.OriginalRenderMeshes = EntityManager.GetSharedComponentManaged<RenderMeshArray>(mouseHover.CurrentEntity);

                // 从悬停中获取渲染信息 entity
                var hoverMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(mouseHover.HoverEntity);
                var hoverRenderMeshes = EntityManager.GetSharedComponentManaged<RenderMeshArray>(mouseHover.HoverEntity);

                // 为我们悬停在其上的当前 entity 创建新的渲染信息：

                // 使用悬停 entity 中的材质，但使用当前 entity 中的网格
                var newRenderMeshes = new RenderMeshArray(hoverRenderMeshes.MaterialReferences, mouseHover.OriginalRenderMeshes.MeshReferences);

                // 使用悬停 entity 中的材质 id，但使用当前 entity 中的网格 id
                var newMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(hoverMeshInfo.Material, mouseHover.OriginalMeshInfo.Mesh);

                // 将新的渲染信息应用到当前的 entity
                EntityManager.SetComponentData(mouseHover.CurrentEntity, newMeshInfo);
                EntityManager.SetSharedComponentManaged(mouseHover.CurrentEntity, newRenderMeshes);
            }
        }
    }
}
