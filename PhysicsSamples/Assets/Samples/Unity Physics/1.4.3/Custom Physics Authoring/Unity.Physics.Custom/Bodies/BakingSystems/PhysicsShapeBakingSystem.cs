using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Unity.Physics.Authoring
{
    class PhysicsShapeBaker : BaseColliderBaker<PhysicsShapeAuthoring>
    {
        public static List<PhysicsShapeAuthoring> physicsShapeComponents = new List<PhysicsShapeAuthoring>();
        public static List<UnityEngine.Collider> colliderComponents = new List<UnityEngine.Collider>();

        bool ShouldConvertShape(PhysicsShapeAuthoring authoring)
        {
            return authoring.enabled;
        }

        private GameObject GetPrimaryBody(GameObject shape, out bool hasBodyComponent, out bool isStaticBody)
        {
            var pb = FindFirstEnabledAncestor(shape, PhysicsShapeExtensions_NonBursted.s_PhysicsBodiesBuffer);
            var rb = FindFirstEnabledAncestor(shape, PhysicsShapeExtensions_NonBursted.s_RigidbodiesBuffer);
            hasBodyComponent = (pb != null || rb != null);
            isStaticBody = false;

            if (pb != null)
            {
                return rb == null ? pb.gameObject :
                    pb.transform.IsChildOf(rb.transform) ? pb.gameObject : rb.gameObject;
            }

            if (rb != null)
                return rb.gameObject;

            // 对于隐式静态形状，首先查看它是否是静态优化层次结构的一部分
            isStaticBody = FindTopmostStaticEnabledAncestor(shape, out var topStatic);
            if (topStatic != null)
                return topStatic;

            // 否则，查找最顶层启用的 Collider 或 PhysicsShapeAuthoring
            var topCollider = FindTopmostEnabledAncestor(shape, PhysicsShapeExtensions_NonBursted.s_CollidersBuffer);
            var topShape = FindTopmostEnabledAncestor(shape, PhysicsShapeExtensions_NonBursted.s_ShapesBuffer);

            return topCollider == null
                ? topShape == null ? shape.gameObject : topShape
                : topShape == null
                ? topCollider
                : topShape.transform.IsChildOf(topCollider.transform)
                ? topCollider
                : topShape;
        }

        ShapeComputationDataBaking GetInputDataFromAuthoringComponent(PhysicsShapeAuthoring shape, Entity colliderEntity)
        {
            GameObject shapeGameObject = shape.gameObject;
            var body = GetPrimaryBody(shapeGameObject, out bool hasBodyComponent, out bool isStaticBody);
            var child = shapeGameObject;
            var shapeInstanceID = shape.GetInstanceID();

            var bodyEntity = GetEntity(body, TransformUsageFlags.Dynamic);

            // 准备静态根
            if (isStaticBody)
            {
                var staticRootMarker = CreateAdditionalEntity(TransformUsageFlags.Dynamic, true, "StaticRootBakeMarker");
                AddComponent(staticRootMarker, new BakeStaticRoot() { Body = bodyEntity, ConvertedBodyInstanceID = body.transform.GetInstanceID() });
            }

            // 跟踪转换的依赖关系
            Transform shapeTransform = GetComponent<Transform>(shape);
            Transform bodyTransform = GetComponent<Transform>(body);
            var instance = new ColliderInstanceBaking
            {
                AuthoringComponentId = shapeInstanceID,
                BodyEntity = bodyEntity,
                ShapeEntity = GetEntity(shapeGameObject, TransformUsageFlags.Dynamic),
                ChildEntity = GetEntity(child, TransformUsageFlags.Dynamic),
                BodyFromShape = ColliderInstanceBaking.GetCompoundFromChild(shapeTransform, bodyTransform),
            };

            ForceUniqueColliderAuthoring forceUniqueComponent = body.GetComponent<ForceUniqueColliderAuthoring>();
            bool isForceUniqueComponentPresent = forceUniqueComponent != null && forceUniqueComponent.enabled;

            var data = GenerateComputationData(shape, bodyTransform, instance, colliderEntity, isForceUniqueComponentPresent);

            data.Instance.ConvertedAuthoringInstanceID = shapeInstanceID;
            data.Instance.ConvertedBodyInstanceID = bodyTransform.GetInstanceID();

            var rb = FindFirstEnabledAncestor(shapeGameObject, PhysicsShapeExtensions_NonBursted.s_RigidbodiesBuffer);
            var pb = FindFirstEnabledAncestor(shapeGameObject, PhysicsShapeExtensions_NonBursted.s_PhysicsBodiesBuffer);
            // 刚体无法了解 Physics 形状 Component。我们需要承担 baking 和 collider 的责任。
            if (rb || (!rb && !pb) && body == shapeGameObject)
            {
                GetComponents(physicsShapeComponents);
                GetComponents(colliderComponents);
                // 我们需要检查同一对象中是否有其他 colliders，如果有，则只有第一个应该这样做，否则会有 2 个 bakers 将其添加到 entity
                // trigger BuildCompoundColliderBakingSystem 需要此信息
                // 如果它们是同一对象中的旧版 Colliders 和 PhysicsShapeAuthoring，则 PhysicsShapeAuthoring 将添加此
                if (colliderComponents.Count == 0 && physicsShapeComponents.Count > 0 && physicsShapeComponents[0].GetInstanceID() == shapeInstanceID)
                {
                    var entity = GetEntity(TransformUsageFlags.Dynamic);

                    // // 刚体烘焙始终添加 PhysicsWorldIndex component 并处理变换
                    if (!hasBodyComponent)
                    {
                        AddSharedComponent(entity, new PhysicsWorldIndex());
                        PostProcessTransform(bodyTransform);
                    }

                    AddComponent(entity, new PhysicsCompoundData()
                    {
                        AssociateBlobToBody = false,
                        ConvertedBodyInstanceID = shapeInstanceID,
                        Hash = default,
                    });
                    AddComponent<PhysicsRootBaked>(entity);
                    AddComponent<PhysicsCollider>(entity);
                }
            }

            return data;
        }

        Material ProduceMaterial(PhysicsShapeAuthoring shape)
        {
            var materialTemplate = shape.MaterialTemplate;
            if (materialTemplate != null)
                DependsOn(materialTemplate);
            return shape.GetMaterial();
        }

        CollisionFilter ProduceCollisionFilter(PhysicsShapeAuthoring shape)
        {
            return shape.GetFilter();
        }

        UnityEngine.Mesh GetMesh(PhysicsShapeAuthoring shape, out float4x4 childToShape)
        {
            var mesh = shape.CustomMesh;
            childToShape = float4x4.identity;

            if (mesh == null)
            {
                // 尝试在孩子们中建立网格
                var filter = GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    mesh = filter.sharedMesh;
                    var childTransform = GetComponent<Transform>(filter);
                    childToShape = math.mul(shape.transform.worldToLocalMatrix, childTransform.localToWorldMatrix);;
                }
            }

            if (mesh == null)
            {
                throw new InvalidOperationException(
                    $"No {nameof(PhysicsShapeAuthoring.CustomMesh)} assigned on {shape.name}."
                );
            }
            DependsOn(mesh);
            return mesh;
        }

        bool GetMeshes(PhysicsShapeAuthoring shape, out List<UnityEngine.Mesh> meshes, out List<float4x4> childrenToShape)
        {
            meshes = new List<UnityEngine.Mesh>();
            childrenToShape = new List<float4x4>();

            if (shape.CustomMesh != null)
            {
                meshes.Add(shape.CustomMesh);
                childrenToShape.Add(float4x4.identity);
            }
            else
            {
                // 尝试获取子级中的所有网格
                var meshFilters = GetComponentsInChildren<MeshFilter>();

                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        var shapeAuthoring = GetComponent<PhysicsShapeAuthoring>(meshFilter);
                        if (shapeAuthoring != null && shapeAuthoring != shape)
                        {
                            // 跳过此案例，因为它将被单独处理
                            continue;
                        }

                        meshes.Add(meshFilter.sharedMesh);

                        // 如果不需要，请勿计算子项形状，以避免可能阻止共享 collider 的近似值
                        if (shape.transform.localToWorldMatrix.Equals(meshFilter.transform.localToWorldMatrix))
                            childrenToShape.Add(float4x4.identity);
                        else
                        {
                            var transform = math.mul(shape.transform.worldToLocalMatrix,
                                meshFilter.transform.localToWorldMatrix);
                            childrenToShape.Add(transform);
                        }

                        DependsOn(meshes.Last());
                    }
                }
            }

            return meshes.Count > 0;
        }

        UnityEngine.Mesh CombineMeshes(PhysicsShapeAuthoring shape, List<UnityEngine.Mesh> meshes, List<float4x4> childrenToShape)
        {
            var instances = new List<CombineInstance>();
            var numVertices = 0;
            for (var i = 0; i < meshes.Count; ++i)
            {
                var currentMesh = meshes[i];
                var currentChildToShape = childrenToShape[i];
                if (!currentMesh.IsValidForConversion(shape.gameObject))
                {
                    throw new InvalidOperationException(
                        $"Mesh '{currentMesh}' assigned on {shape.name} is not readable. Ensure that you have enabled Read/Write on its import settings."
                    );
                }

                // 手动组合子网格
                numVertices += meshes[i].vertexCount;
                var combinedSubmeshes = new UnityEngine.Mesh();
                combinedSubmeshes.vertices = currentMesh.vertices;

                var combinedIndices = new List<int>();
                for (int indexSubMesh = 0; indexSubMesh < meshes[i].subMeshCount; ++indexSubMesh)
                {
                    combinedIndices.AddRange(currentMesh.GetIndices(indexSubMesh));
                }

                combinedSubmeshes.SetIndices(combinedIndices, MeshTopology.Triangles, 0);
                combinedSubmeshes.RecalculateNormals();
                var instance = new CombineInstance
                {
                    mesh = combinedSubmeshes,
                    transform = currentChildToShape,
                };
                instances.Add(instance);
            }

            var mesh = new UnityEngine.Mesh();
            mesh.indexFormat = numVertices > UInt16.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.CombineMeshes(instances.ToArray());
            mesh.RecalculateBounds();

            return mesh;
        }

        private ShapeComputationDataBaking GenerateComputationData(PhysicsShapeAuthoring shape, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool isForceUniqueComponentPresent)
        {
            bool isUnique = isForceUniqueComponentPresent || shape.ForceUnique;
            var res = new ShapeComputationDataBaking
            {
                Instance = colliderInstance,
                Material = ProduceMaterial(shape),
                CollisionFilter = ProduceCollisionFilter(shape),
                ForceUniqueIdentifier = isUnique ? shape.ForceUniqueID : 0u
            };

            var shapeTransform = shape.transform;
            var localToWorld = (float4x4)shapeTransform.localToWorldMatrix;
            var bodyLocalToWorld = (float4x4)bodyTransform.transform.localToWorldMatrix;

            // 我们不会将纯均匀尺度烘焙到 colliders 中，因为编辑时均匀尺度
            // 被烘焙到 entity 的 LocalTransform.Scale 属性中，除非形状具有非同一比例
            // 相对于它所包含的身体。在这种情况下，我们需要将所有比例烘焙到 collider 几何体中。
            var relativeTransform = math.mul(math.inverse(bodyLocalToWorld), localToWorld);

            var hasNonIdentityScaleRelativeToBody = relativeTransform.HasNonIdentityScale();
            var hasShearRelativeToBody = relativeTransform.HasShear();
            var bakeUniformScale = hasNonIdentityScaleRelativeToBody || hasShearRelativeToBody;

            // 如果身体变换具有纯粹均匀的比例，并且身体和形状之间存在任何比例或剪切，
            // 那么我们需要在 baking 之前从形状变换中提取统一的身体比例
            // 防止形状被身体的统一尺度缩放两次。这是因为纯粹的顶级体质均匀的鳞片
            // 不会烘焙到 collider 几何体中，而是由主体 entity 的 LocalTransform.Scale 属性表示。
            if (bakeUniformScale)
            {
                var bodyHasShear = bodyLocalToWorld.HasShear();
                var bodyHasNonUniformScale = bodyLocalToWorld.HasNonUniformScale();
                if (!bodyHasShear && !bodyHasNonUniformScale)
                {
                    // 提取身体的均匀尺度并将其从形状变换中删除
                    var bodyScale = bodyLocalToWorld.DecomposeScale();
                    var bodyScaleInverse = 1 / bodyScale;
                    localToWorld = math.mul(localToWorld, float4x4.Scale(bodyScaleInverse));
                }
            }

            // 仅在需要时烘焙均匀比例（参见上文），并始终将剪切和非均匀比例烘焙到 collider 几何体中
            var colliderBakeMatrix = float4x4.identity;
            if (bakeUniformScale || localToWorld.HasShear() || localToWorld.HasNonUniformScale())
            {
                var rigidBodyTransform = Math.DecomposeRigidBodyTransform(localToWorld);
                colliderBakeMatrix = math.mul(math.inverse(new float4x4(rigidBodyTransform)), localToWorld);
                // 确保我们有一个有效的变换矩阵
                colliderBakeMatrix.c0[3] = 0;
                colliderBakeMatrix.c1[3] = 0;
                colliderBakeMatrix.c2[3] = 0;
                colliderBakeMatrix.c3[3] = 1;
            }

            var shapeToWorld = shape.GetShapeToWorldMatrix();
            EulerAngles orientation;

            res.ShapeType = shape.ShapeType;
            switch (shape.ShapeType)
            {
                case ShapeType.Box:
                {
                    res.BoxProperties = shape.GetBoxProperties(out orientation)
                        .BakeToBodySpace(localToWorld, shapeToWorld, orientation, bakeUniformScale);
                    break;
                }
                case ShapeType.Capsule:
                {
                    res.CapsuleProperties = shape.GetCapsuleProperties()
                        .BakeToBodySpace(localToWorld, shapeToWorld, bakeUniformScale)
                        .ToRuntime();
                    break;
                }
                case ShapeType.Sphere:
                {
                    res.SphereProperties = shape.GetSphereProperties(out orientation)
                        .BakeToBodySpace(localToWorld, shapeToWorld, ref orientation, bakeUniformScale);
                    break;
                }
                case ShapeType.Cylinder:
                {
                    res.CylinderProperties = shape.GetCylinderProperties(out orientation)
                        .BakeToBodySpace(localToWorld, shapeToWorld, orientation, bakeUniformScale);
                    break;
                }
                case ShapeType.Plane:
                {
                    shape.GetPlaneProperties(out var center, out var size, out orientation);
                    PhysicsShapeExtensions.BakeToBodySpace(
                        center, size, orientation, colliderBakeMatrix,
                        out res.PlaneVertices.c0, out res.PlaneVertices.c1, out res.PlaneVertices.c2, out res.PlaneVertices.c3
                    );
                    break;
                }
                case ShapeType.ConvexHull:
                {
                    res.ConvexHullProperties.Filter = res.CollisionFilter;
                    res.ConvexHullProperties.Material = res.Material;
                    res.ConvexHullProperties.GenerationParameters = shape.ConvexHullGenerationParameters.ToRunTime();

                    CreateMeshAuthoringData(shape, colliderBakeMatrix, colliderEntity);
                    break;
                }
                case ShapeType.Mesh:
                {
                    res.MeshProperties.Filter = res.CollisionFilter;
                    res.MeshProperties.Material = res.Material;

                    CreateMeshAuthoringData(shape, colliderBakeMatrix, colliderEntity);
                    break;
                }
            }

            return res;
        }

        private void CreateMeshAuthoringData(PhysicsShapeAuthoring shape, float4x4 colliderBakeMatrix, Entity colliderEntity)
        {
            if (GetMeshes(shape, out var meshes, out var childrenToShape))
            {
                // 将所有检测到的网格合并为一个网格
                var mesh = CombineMeshes(shape, meshes, childrenToShape);
                if (!mesh.IsValidForConversion(shape.gameObject))
                {
                    throw new InvalidOperationException(
                        $"Mesh '{mesh}' assigned on {shape.name} is not readable. Ensure that you have enabled Read/Write on its import settings."
                    );
                }

                var meshBakingData = new PhysicsMeshAuthoringData()
                {
                    Convex = shape.ShapeType == ShapeType.ConvexHull,
                    Mesh = mesh,
                    BakeFromShape = colliderBakeMatrix,
                    MeshBounds = mesh.bounds,
                    ChildToShape = float4x4.identity
                };
                AddComponent(colliderEntity, meshBakingData);
            }
            else
            {
                throw new InvalidOperationException(
                    $"No {nameof(PhysicsShapeAuthoring.CustomMesh)} or {nameof(MeshFilter.sharedMesh)} assigned on {shape.name}."
                );
            }
        }

        public override void Bake(PhysicsShapeAuthoring authoring)
        {
            var shapeBakingData = new PhysicsColliderAuthoringData();

            // 第一关
            Profiler.BeginSample("Collect Inputs from Authoring Components");

            if (ShouldConvertShape(authoring))
            {
                // 我们可以在同一个游戏对象上拥有多个相同类型的 Colliders，因此不要将 components 添加到 baking entity
                // 我们将 components 添加到附加 entity。这些新的 entities 将由 baking system 进行处理
                var colliderEntity = CreateAdditionalEntity(TransformUsageFlags.None, true);
                shapeBakingData.ShapeComputationalData = GetInputDataFromAuthoringComponent(authoring, colliderEntity);
                AddComponent(colliderEntity, shapeBakingData);

                // 数据将由 BaseShapeBakingSystem 填充，但我们将其添加到此处，以便在删除 collider component 时从 entity 恢复数据
                AddComponent(colliderEntity, new PhysicsColliderBakedData()
                {
                    BodyEntity = shapeBakingData.ShapeComputationalData.Instance.BodyEntity,
                    BodyFromShape = shapeBakingData.ShapeComputationalData.Instance.BodyFromShape,
                    ChildEntity = shapeBakingData.ShapeComputationalData.Instance.ChildEntity,
                    // 如果 Shape Entity 等于 Body Entity 则为叶子
                    IsLeafEntityBody = (shapeBakingData.ShapeComputationalData.Instance.ShapeEntity.Equals(shapeBakingData.ShapeComputationalData.Instance.BodyEntity))
                });
            }

            Profiler.EndSample();
        }
    }
}
