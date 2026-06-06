using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;
#if !UNITY_EDITOR
using UnityEngine.TestTools;
#endif

namespace Unity.Physics.Tests.Authoring
{
    class PhysicsShapeConversionSystemIntegrationTests : BaseHierarchyConversionTest
    {
        private Mesh NonReadableMesh { get; set; }
        private Mesh ReadableMesh { get; set; }
        private Mesh MeshWithMultipleSubMeshes { get; set; }

        private Mesh TrivialMesh { get; set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            ReadableMesh = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
            Assume.That(ReadableMesh.isReadable, Is.True, $"{ReadableMesh} is not readable.");

            NonReadableMesh = Mesh.Instantiate(ReadableMesh);
            NonReadableMesh.UploadMeshData(true);
            Assume.That(NonReadableMesh.isReadable, Is.False, $"{NonReadableMesh} is readable.");

            MeshWithMultipleSubMeshes = new Mesh
            {
                name = nameof(MeshWithMultipleSubMeshes),
                vertices = new[]
                {
                    new Vector3(0f, 1f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 0f, 0f)
                },
                normals = new[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                    Vector3.back
                },
                subMeshCount = 2
            };
            MeshWithMultipleSubMeshes.SetTriangles(new[] { 0, 1, 2 }, 0);
            MeshWithMultipleSubMeshes.SetTriangles(new[] { 2, 3, 0 }, 1);
            Assume.That(MeshWithMultipleSubMeshes.isReadable, Is.True, $"{MeshWithMultipleSubMeshes} is not readable.");

            TrivialMesh = new Mesh()
            {
                vertices = new[]
                {
                    new Vector3(1f, 1f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 0f, 0f)
                },
                normals = new[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back
                },
                triangles = new[]
                {
                    0, 1, 2
                },
                subMeshCount = 1
            };
            Assume.That(TrivialMesh.isReadable, Is.True, $"{TrivialMesh} is not readable.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (NonReadableMesh != null)
                Mesh.DestroyImmediate(NonReadableMesh);
            if (MeshWithMultipleSubMeshes != null)
                Mesh.DestroyImmediate(MeshWithMultipleSubMeshes);
            if (TrivialMesh != null)
                Mesh.DestroyImmediate(TrivialMesh);
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenBodyHasOneSiblingShape_CreatesPrimitive()
        {
            CreateHierarchy(
                new[] { typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring) },
                Array.Empty<Type>(),
                Array.Empty<Type>()
            );
            Root.GetComponent<PhysicsShapeAuthoring>().SetBox(new BoxGeometry { Size = 1f, Orientation = quaternion.identity });

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(c => Assert.That(c.Value.Value.Type, Is.EqualTo(ColliderType.Box)), k_DefaultWorldIndex);
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenBodyHasOneDescendentShape_CreatesCompound()
        {
            CreateHierarchy(
                new[] { typeof(PhysicsBodyAuthoring) },
                new[] { typeof(PhysicsShapeAuthoring) },
                Array.Empty<Type>()
            );
            Parent.GetComponent<PhysicsShapeAuthoring>().SetBox(new BoxGeometry { Size = 1f, Orientation = quaternion.identity });

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(
                c =>
                {
                    Assert.That(c.Value.Value.Type, Is.EqualTo(ColliderType.Compound));
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)c.Value.GetUnsafePtr();
                        Assert.That(compoundCollider->Children, Has.Length.EqualTo(1));
                        Assert.That(compoundCollider->Children[0].Collider->Type, Is.EqualTo(ColliderType.Box));
                    }
                },
                k_DefaultWorldIndex
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenBodyHasOneDescendentShape_CreatesCompoundWithFiniteMass()
        {
            CreateHierarchy(
                new[] { typeof(PhysicsBodyAuthoring) },
                new[] { typeof(PhysicsShapeAuthoring) },
                Array.Empty<Type>()
            );
            Parent.GetComponent<PhysicsShapeAuthoring>().SetBox(new BoxGeometry { Size = 1f, Orientation = quaternion.identity });
            Parent.GetComponent<PhysicsShapeAuthoring>().CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(
                c =>
                {
                    Assert.That(c.Value.Value.Type, Is.EqualTo(ColliderType.Compound));
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)c.Value.GetUnsafePtr();
                        Assume.That(compoundCollider->Children, Has.Length.EqualTo(1));
                        Assume.That(compoundCollider->Children[0].Collider->Type, Is.EqualTo(ColliderType.Box));

                        // 确保正确计算化合物质量属性
                        Assert.That(compoundCollider->MassProperties.Volume > 0.0f);
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.Transform.pos)));
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.Transform.rot.value)));
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.InertiaTensor)));
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.InertiaMatrix.c0)));
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.InertiaMatrix.c1)));
                        Assert.That(math.all(math.isfinite(compoundCollider->MassProperties.MassDistribution.InertiaMatrix.c2)));
                    }
                },
                k_DefaultWorldIndex
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenBodyHasMultipleDescendentShapes_CreatesCompound()
        {
            CreateHierarchy(
                new[] { typeof(PhysicsBodyAuthoring) },
                new[] { typeof(PhysicsShapeAuthoring) },
                new[] { typeof(PhysicsShapeAuthoring) }
            );
            Parent.GetComponent<PhysicsShapeAuthoring>().SetBox(new BoxGeometry { Size = 1f, Orientation = quaternion.identity });
            Child.GetComponent<PhysicsShapeAuthoring>().SetSphere(new SphereGeometry { Radius = 1f }, quaternion.identity);

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(
                c =>
                {
                    Assert.That(c.Value.Value.Type, Is.EqualTo(ColliderType.Compound));
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)c.Value.GetUnsafePtr();

                        var childTypes = Enumerable.Range(0, compoundCollider->NumChildren)
                            .Select(i => compoundCollider->Children[i].Collider->Type)
                            .ToArray();
                        Assert.That(childTypes, Is.EquivalentTo(new[] { ColliderType.Box, ColliderType.Sphere }));
                    }
                },
                k_DefaultWorldIndex
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenShapeHasNonReadableConvex_ThrowsException()
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(PhysicsShapeAuthoring) });
            Child.GetComponent<PhysicsShapeAuthoring>().SetConvexHull(default, NonReadableMesh);

            VerifyLogsException<InvalidOperationException>(k_NonReadableMeshPattern);
        }

        [Test]
        public void PhysicsShapeConversionSystem_WhenShapeHasNonReadableMesh_ThrowsException()
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(PhysicsShapeAuthoring) });
            Child.GetComponent<PhysicsShapeAuthoring>().SetMesh(NonReadableMesh);

            VerifyLogsException<InvalidOperationException>(k_NonReadableMeshPattern);
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenMeshCollider_MultipleSubMeshes_AllSubMeshesIncluded(
            [Values(
                typeof(UnityEngine.MeshCollider),
                typeof(PhysicsShapeAuthoring)
             )]
            Type shapeType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { shapeType });
            if (Child.GetComponent(shapeType) is UnityEngine.MeshCollider meshCollider)
                meshCollider.sharedMesh = MeshWithMultipleSubMeshes;
            else
                Child.GetComponent<PhysicsShapeAuthoring>().SetMesh(MeshWithMultipleSubMeshes);

            TestMeshData(numExpectedMeshSections: 1, numExpectedPrimitivesPerSection: new[] {1}, quadPrimitiveExpectedFlags: new[] {new[] {true}});
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenGOHasShape_GOIsActive_AuthoringComponentEnabled_AuthoringDataConverted(
            [Values(
                typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.SphereCollider), typeof(UnityEngine.MeshCollider),
                typeof(PhysicsShapeAuthoring)
             )]
            Type shapeType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { shapeType });
            if (Child.GetComponent(shapeType) is UnityEngine.MeshCollider meshCollider)
                meshCollider.sharedMesh = ReadableMesh;

            // 假定在默认条件下创建有效的 PhysicsCollider 的转换
            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(c => Assert.That(c.IsValid, Is.True), k_DefaultWorldIndex);
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenGOHasShape_AuthoringComponentDisabled_AuthoringDataNotConverted(
            [Values(
                typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.SphereCollider), typeof(UnityEngine.MeshCollider),
                typeof(PhysicsShapeAuthoring)
             )]
            Type shapeType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { shapeType });
            if (Child.GetComponent(shapeType) is UnityEngine.MeshCollider meshCollider)
                meshCollider.sharedMesh = ReadableMesh;
            var c = Child.GetComponent(shapeType);
            if (c is UnityEngine.Collider collider)
                collider.enabled = false;
            else
                (c as PhysicsShapeAuthoring).enabled = false;

            // 假定在默认条件下创建有效的 PhysicsCollider 的转换
            // 相应测试 ConversionSystems_WhenGOHasShape_GOIsActive_AuthoringComponentEnabled_AuthoringDataConverted 涵盖
            VerifyNoDataProduced<PhysicsCollider>();
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenGOHasShape_GOIsInactive_BodyIsNotConverted(
            [Values] Node inactiveNode,
            [Values(
                typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.SphereCollider), typeof(UnityEngine.MeshCollider),
                typeof(PhysicsShapeAuthoring)
             )]
            Type shapeType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { shapeType });
            if (Child.GetComponent(shapeType) is UnityEngine.MeshCollider meshCollider)
                meshCollider.sharedMesh = ReadableMesh;

            GetNode(inactiveNode).SetActive(false);
            var numInactiveNodes = Root.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.activeSelf);
            Assume.That(numInactiveNodes, Is.EqualTo(2));

            // 假定在默认条件下创建有效的 PhysicsCollider 的转换
            // 相应测试 ConversionSystems_WhenGOHasShape_GOIsActive_AuthoringComponentEnabled_AuthoringDataConverted 涵盖
            VerifyNoDataProduced<PhysicsCollider>();
        }

        static void SetDefaultShape(PhysicsShapeAuthoring shape, ShapeType type)
        {
            switch (type)
            {
                case ShapeType.Box:
                    shape.SetBox(default);
                    break;
                case ShapeType.Capsule:
                    shape.SetCapsule(new CapsuleGeometryAuthoring { OrientationEuler = EulerAngles.Default });
                    break;
                case ShapeType.Sphere:
                    shape.SetSphere(default, quaternion.identity);
                    break;
                case ShapeType.Cylinder:
                    shape.SetCylinder(new CylinderGeometry { SideCount = CylinderGeometry.MaxSideCount });
                    break;
                case ShapeType.Plane:
                    shape.SetPlane(default, default, quaternion.identity);
                    break;
                case ShapeType.ConvexHull:
                    shape.SetConvexHull(ConvexHullGenerationParameters.Default);
                    break;
                case ShapeType.Mesh:
                    shape.SetMesh();
                    break;
            }

            shape.FitToEnabledRenderMeshes();
        }

        [Test]
        public unsafe void PhysicsShapeConversionSystems_WhenMultipleShapesShareInputs_CollidersShareTheSameData(
            [Values(ShapeType.ConvexHull, ShapeType.Mesh)] ShapeType shapeType
        )
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) },
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );
            foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>())
                meshFilter.sharedMesh = ReadableMesh;
            foreach (var shape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
            {
                SetDefaultShape(shape, shapeType);
                shape.ForceUnique = false;
            }
            Child.transform.localPosition = TransformConversionUtils.k_SharedDataChildTransformation.pos;
            Child.transform.localRotation = TransformConversionUtils.k_SharedDataChildTransformation.rot;

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(colliders =>
            {
                var uniqueColliders = new HashSet<int>();
                foreach (var c in colliders)
                    uniqueColliders.Add((int)c.ColliderPtr);
                var numUnique = uniqueColliders.Count;
                Assert.That(numUnique, Is.EqualTo(1), $"Expected colliders to reference the same data, but found {numUnique} different colliders.");
            }, 2, k_DefaultWorldIndex);
        }

        static readonly TestCaseData[] k_MultipleAuthoringComponentsTestCases =
        {
            new TestCaseData(
                new[] { typeof(Rigidbody), typeof(UnityEngine.BoxCollider), typeof(UnityEngine.BoxCollider) },
                Array.Empty<Type>(),
                new[] { ColliderType.Box, ColliderType.Box }
            ).SetName("PhysicsShapeConversionSystems_WhenRigidbodyHasMultipleBoxColliders_CreatesCompound"),
            new TestCaseData(
                new[] { typeof(PhysicsBodyAuthoring), typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.SphereCollider), typeof(PhysicsShapeAuthoring) },
                Array.Empty<Type>(),
                new[] { ColliderType.Box, ColliderType.Box, ColliderType.Capsule, ColliderType.Sphere }
            ).SetName("PhysicsShapeConversionSystems_WhenPhysicsBodyHasMixedColliders_CreatesCompound"),
            new TestCaseData(
                new[] { typeof(Rigidbody)},
                new[] { typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.SphereCollider) },
                new[] { ColliderType.Box, ColliderType.Capsule, ColliderType.Sphere }
            ).SetName("PhysicsShapeConversionSystems_WhenRigidbodyHasCollidersOnlyInDescendents_CreatesCompound"),
            new TestCaseData(
                new[] { typeof(Rigidbody), typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider)},
                new[] { typeof(UnityEngine.SphereCollider), typeof(UnityEngine.BoxCollider) },
                new[] { ColliderType.Box, ColliderType.Box, ColliderType.Capsule, ColliderType.Sphere }
            ).SetName("PhysicsShapeConversionSystems_WhenRigidbodyHasCollidersAlsoInDescendents_CreatesCompound"),
        };

        [TestCaseSource(nameof(k_MultipleAuthoringComponentsTestCases))]
        public void PhysicsShapeConversionSystems_CompoundColliderCreation(
            Type[] rootComponentTypes, Type[] parentComponentTypes, ColliderType[] expectedColliderTypes
        )
        {
            CreateHierarchy(rootComponentTypes, parentComponentTypes, Array.Empty<Type>());
            Root.GetComponent<PhysicsShapeAuthoring>()?.SetBox(new BoxGeometry { Size = 1f });

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(
                (w, e, c) =>
                {
                    Assert.That(c.Value.Value.Type, Is.EqualTo(ColliderType.Compound));
                    unsafe
                    {
                        var compoundCollider = (CompoundCollider*)c.Value.GetUnsafePtr();

                        var childTypes = Enumerable.Range(0, compoundCollider->NumChildren)
                            .Select(i => compoundCollider->Children[i].Collider->Type)
                            .ToArray();
                        Assert.That(childTypes, Is.EquivalentTo(expectedColliderTypes));

                        // 确保我们有一个大小合适的 collider 密钥 entity 对缓冲区
                        Assert.That(w.EntityManager.HasBuffer<PhysicsColliderKeyEntityPair>(e), Is.True);
                        var buffer = w.EntityManager.GetBuffer<PhysicsColliderKeyEntityPair>(e);
                        Assert.That(buffer.Length, Is.EqualTo(compoundCollider->NumChildren));

                        // 确保缓冲区的内容正确
                        for (int i = 0; i < buffer.Length; ++i)
                        {
                            var bufferElement = buffer[i];

                            // 确保引用的 entity 存在
                            Assert.That(w.EntityManager.Exists(bufferElement.Entity), Is.True);

                            // 确保 collider 密钥有效，也就是说，缓冲区中的每个密钥都有一个有效的子 collider
                            Assert.IsTrue(compoundCollider->GetChild(ref bufferElement.Key, out var childLookup));

                            // 确保 entity 正确。
                            // Note: 我们希望它在 collider blob 中设置为 Null，因为在 blob 中 entity
                            // 当其内部 ID 更改时（与出现在 component 或缓冲区中时不同），无法自动更新
                            // 例如 PhysicsColliderKeyEntityPair。它设置为 Entity.Null 以避免出现无效的 entity 引用。
                            // 它仍然可以用作用户创建的化合物 colliders 的用户数据。
                            var childInCompound = compoundCollider->Children[i];
                            Assert.That(Entity.Null, Is.EqualTo(childInCompound.Entity));
                            Assert.That(Entity.Null, Is.EqualTo(childLookup.Entity));
                        }
                    }
                },
                k_DefaultWorldIndex
            );
        }

        [Test]
        public unsafe void PhysicsShapeConversionSystems_WhenMultipleShapesShareMeshes_CollidersShareTheSameData(
            [Values(ShapeType.ConvexHull, ShapeType.Mesh)] ShapeType shapeType
        )
        {
            CreateHierarchy(
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring) },
                new[] { typeof(MeshFilter), typeof(MeshRenderer) },
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );

            foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>())
                meshFilter.sharedMesh = MeshWithMultipleSubMeshes;
            foreach (var shape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
            {
                SetDefaultShape(shape, shapeType);
                shape.ForceUnique = false;
            }

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(colliders =>
            {
                var uniqueColliders = new HashSet<int>();
                foreach (var c in colliders)
                    uniqueColliders.Add((int)c.ColliderPtr);
                var numUnique = uniqueColliders.Count;
                Assert.That(numUnique, Is.EqualTo(1), $"Expected colliders to reference unique data, but found {numUnique} different colliders.");
            }, 2, k_DefaultWorldIndex);
        }

        [Test]
        public unsafe void PhysicsShapeConversionSystems_WhenMultipleShapesShareMeshes_WithDifferentOffsets_CollidersDoNotShareTheSameData(
            [Values(ShapeType.ConvexHull, ShapeType.Mesh)] ShapeType shapeType
        )
        {
            CreateHierarchy(
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring) },
                new[] { typeof(MeshFilter), typeof(MeshRenderer) },
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );
            foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>())
                meshFilter.sharedMesh = ReadableMesh;
            foreach (var shape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
            {
                SetDefaultShape(shape, shapeType);
                shape.ForceUnique = false;
            }
            // 根将从父级获取网格（有偏移），子级将从自身获取网格（无偏移）
            Parent.transform.localPosition = TransformConversionUtils.k_SharedDataChildTransformation.pos;
            Parent.transform.localRotation = TransformConversionUtils.k_SharedDataChildTransformation.rot;

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(colliders =>
            {
                var uniqueColliders = new HashSet<int>();
                foreach (var c in colliders)
                    uniqueColliders.Add((int)c.ColliderPtr);
                var numUnique = uniqueColliders.Count;
                Assert.That(numUnique, Is.EqualTo(2), $"Expected colliders to reference unique data, but found {numUnique} different colliders.");
            }, 2, k_DefaultWorldIndex);
        }

        [Test]
        public unsafe void PhysicsShapeConversionSystems_WhenMultipleShapesShareMeshes_WithDifferentInheritedScale_CollidersDontShareTheSameData_IfNonUniformScale(
            [Values(ShapeType.ConvexHull, ShapeType.Mesh)] ShapeType shapeType, [Values] bool uniformScale
        )
        {
            CreateHierarchy(
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) },
                Array.Empty<Type>(),
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );
            foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>())
                meshFilter.sharedMesh = ReadableMesh;
            foreach (var shape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
            {
                SetDefaultShape(shape, shapeType);
                shape.ForceUnique = false;
            }

            // 修改 1 个 collider 的比例。请注意，如果比例统一，collider 几何体将不会受到影响。
            // 在这种情况下，我们期望 LocalTransform.Scale 包含提供的比例值。

            const float kScale = 2f;
            if (uniformScale)
            {
                Parent.transform.localScale = new float3(kScale);
            }
            else
            {
                Parent.transform.localScale = new float3(kScale, 1, 1);
            }

            var expectedUniqueColliders = uniformScale ? 1 : 2;
            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>((world, entities, colliders) =>
            {
                // 确保我们有预期数量的统一缩放的 colliders
                int foundUniformScaleCount = 0;
                foreach (var e in entities)
                {
                    // 期望 LocalTransform.Scale 设置正确
                    var localTransform = world.EntityManager.GetComponentData<LocalTransform>(e);
                    foundUniformScaleCount += math.abs(localTransform.Scale - kScale) < 1e-5 ? 1 : 0;
                }
                Assert.That(foundUniformScaleCount, Is.EqualTo(uniformScale ? 1 : 0));

                // 确保我们有预期数量的唯一 colliders
                var uniqueColliders = new HashSet<IntPtr>();
                foreach (var c in colliders)
                {
                    uniqueColliders.Add((IntPtr)c.ColliderPtr);
                }
                var numUnique = uniqueColliders.Count;

                Assert.That(numUnique, Is.EqualTo(expectedUniqueColliders), $"Expected {expectedUniqueColliders} unique colliders, but found {numUnique} unique colliders.");
            }, 2, k_DefaultWorldIndex);
        }

        [Test]
        public unsafe void PhysicsShapeConversionSystems_WhenMultipleShapesShareInputs_AndShapeIsForcedUnique_CollidersDoNotShareTheSameData(
            [Values] ShapeType shapeType
        )
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) },
                new[] { typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );
            foreach (var meshFilter in Root.GetComponentsInChildren<MeshFilter>())
                meshFilter.sharedMesh = ReadableMesh;
            foreach (var shape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
            {
                SetDefaultShape(shape, shapeType);
                shape.ForceUnique = true;
            }

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(colliders =>
            {
                var uniqueColliders = new HashSet<int>();
                foreach (var c in colliders)
                    uniqueColliders.Add((int)c.ColliderPtr);

                var numUnique = uniqueColliders.Count;
                Assert.That(numUnique, Is.EqualTo(2), $"Expected colliders to reference unique data, but found {numUnique} different colliders.");
            }, 2, k_DefaultWorldIndex);
        }

        struct TriangleCounter : ILeafColliderCollector
        {
            public int NumTriangles;

            public unsafe void AddLeaf(ColliderKey key, ref ChildCollider leaf)
            {
                var collider = leaf.Collider;
                if (collider->Type == ColliderType.Triangle)
                {
                    ++NumTriangles;
                }
            }

            public void PushCompositeCollider(ColliderKeyPath compositeKey, Math.MTransform parentFromComposite, out Math.MTransform worldFromParent)
            {
                worldFromParent = new Math.MTransform();

                // 什么都不做
            }

            public void PopCompositeCollider(uint numCompositeKeyBits, Math.MTransform worldFromParent)
            {
                // 什么都不做
            }
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenNoCustomMeshSpecified_ChildMeshesAreIncluded()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] {typeof(PhysicsShapeAuthoring), typeof(MeshFilter)},
                new[] {typeof(MeshFilter)}
            );
            var meshFilter = Parent.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = TrivialMesh;
            var childMeshFilter = Child.GetComponent<MeshFilter>();
            childMeshFilter.sharedMesh = TrivialMesh;
            Child.transform.position += new Vector3(42, 42, 42);

            var shape = Parent.GetComponent<PhysicsShapeAuthoring>();
            shape.SetMesh();

            var expectedTriangleCount = (TrivialMesh.triangles.Length / 3) * 2;

            TestConvertedData<PhysicsCollider>(collider =>
            {
                unsafe
                {
                    Assert.That(collider.Value.Value.Type, Is.EqualTo(ColliderType.Mesh));
                    var meshCollider = (MeshCollider*)collider.ColliderPtr;
                    var triangleCounter = new TriangleCounter();
                    meshCollider->GetLeaves(ref triangleCounter);

                    Assert.That(triangleCounter.NumTriangles, Is.EqualTo(expectedTriangleCount));
                }
            });
        }

        [Test]
        public void PhysicsShapeConversionSystems_WhenCustomMeshSpecified_ChildMeshesAreIgnored()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] {typeof(PhysicsShapeAuthoring), typeof(MeshFilter)},
                new[] {typeof(MeshFilter)}
            );
            var meshFilter = Parent.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = ReadableMesh;
            var childMeshFilter = Child.GetComponent<MeshFilter>();
            childMeshFilter.sharedMesh = ReadableMesh;
            Child.transform.position += new Vector3(42, 42, 42);

            Assert.That(ReadableMesh.triangles.Length / 3, Is.GreaterThan(0));

            var shape = Parent.GetComponent<PhysicsShapeAuthoring>();
            shape.SetMesh(TrivialMesh);
            var expectedTriangleCount = TrivialMesh.triangles.Length / 3;

            TestConvertedData<PhysicsCollider>(collider =>
            {
                unsafe
                {
                    Assert.That(collider.Value.Value.Type, Is.EqualTo(ColliderType.Mesh));
                    var meshCollider = (MeshCollider*)collider.ColliderPtr;
                    var triangleCounter = new TriangleCounter();
                    meshCollider->GetLeaves(ref triangleCounter);

                    Assert.That(triangleCounter.NumTriangles, Is.EqualTo(expectedTriangleCount));
                }
            });
        }

        void CreateHierarchyWithChildShape(ShapeType shapeType)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] { typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring), typeof(MeshFilter), typeof(MeshRenderer) }
            );

            // 在网格过滤器中设置网格，以便我们可以通过 SetDefaultShape() 中的自动拟合获得所有形状的默认尺寸
            Child.GetComponent<MeshFilter>().sharedMesh = ReadableMesh;

            var physicsShape = Child.GetComponent<PhysicsShapeAuthoring>();
            SetDefaultShape(physicsShape, shapeType);
        }

        /// <summary>
        /// 测试当游戏对象包含统一比例时，生成的 entity 的局部变换具有预期的比例和
        /// 烘焙后的 collider 几何形状不受比例影响。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystems_WhenGOIsUniformlyScaled_LocalTransformHasScale_ColliderIsNotScaled([Values] ShapeType shapeType)
        {
            CreateHierarchyWithChildShape(shapeType);

            // 统一变换子 collider
            const float k_UniformScale = 2f;
            Child.transform.localScale = new float3(k_UniformScale);

            var shape = Child.GetComponent<PhysicsShapeAuthoring>();
            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                Assert.That(transform.Scale, Is.PrettyCloseTo(k_UniformScale));

                // 确保烘焙的 collider 几何体不受统一比例的影响
                switch (shapeType)
                {
                    case ShapeType.Box:
                        {
                            // 将形状的盒子属性与烘焙的 BoxCollider 属性进行比较，并期望它们相同
                            var boxGeometry = shape.GetBoxProperties();

                            var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                            unsafe
                            {
                                var boxCollider = (BoxCollider*)physicsCollider.ColliderPtr;
                                // 确保 collider 类型符合预期
                                Assert.That(boxCollider->Type, Is.EqualTo(ColliderType.Box));

                                // 比较框属性
                                Assert.That(boxCollider->Size, Is.PrettyCloseTo(boxGeometry.Size));
                                Assert.That(boxCollider->Center, Is.PrettyCloseTo(boxGeometry.Center));
                                Assert.That(boxCollider->Orientation, Is.OrientedEquivalentTo(boxGeometry.Orientation));
                            }

                            break;
                        }
                    case ShapeType.Capsule:
                        {
                            // 将 shape 的胶囊属性与烘焙的 CapsuleCollider 属性进行比较，并期望它们相同
                            var capsuleGeometry = shape.GetCapsuleProperties();
                            unsafe
                            {
                                var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                                // 确保 collider 类型符合预期
                                Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Capsule));

                                // 比较胶囊特性
                                var capsuleCollider = (CapsuleCollider*)physicsCollider.ColliderPtr;
                                var actualCenter = 0.5f * (capsuleCollider->Vertex0 + capsuleCollider->Vertex1);
                                var actualHeight = math.distance(capsuleCollider->Vertex0, capsuleCollider->Vertex1) + 2 * capsuleCollider->Radius;
                                var expectedDirection = new float3x3(capsuleGeometry.Orientation).c2;
                                var actualDirection = math.normalize(capsuleCollider->Vertex0 - capsuleCollider->Vertex1);
                                Assert.That(math.dot(actualDirection, expectedDirection), Is.PrettyCloseTo(1));
                                Assert.That(actualCenter, Is.PrettyCloseTo(capsuleGeometry.Center));
                                Assert.That(capsuleCollider->Radius, Is.PrettyCloseTo(capsuleGeometry.Radius));
                                Assert.That(actualHeight, Is.PrettyCloseTo(capsuleGeometry.Height));
                            }
                            break;
                        }
                    case ShapeType.Cylinder:
                        {
                            // 将形状的圆柱体属性与烘焙的 CylinderCollider 属性进行比较，并期望它们相同
                            var cylinderGeometry = shape.GetCylinderProperties();
                            unsafe
                            {
                                var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                                // 确保 collider 类型符合预期
                                Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Cylinder));

                                // 比较气缸特性
                                var cylinderCollider = (CylinderCollider*)physicsCollider.ColliderPtr;
                                Assert.That(cylinderCollider->Radius, Is.PrettyCloseTo(cylinderGeometry.Radius));
                                Assert.That(cylinderCollider->Height, Is.PrettyCloseTo(cylinderGeometry.Height));
                                Assert.That(cylinderCollider->SideCount, Is.EqualTo(cylinderGeometry.SideCount));
                                Assert.That(cylinderCollider->Center, Is.PrettyCloseTo(cylinderGeometry.Center));
                                Assert.That(cylinderCollider->Orientation, Is.OrientedEquivalentTo(cylinderGeometry.Orientation));
                            }
                            break;
                        }
                    case ShapeType.Sphere:
                        {
                            // 将形状的球体属性与烘焙的 SphereCollider 属性进行比较，并期望它们相同
                            var sphereGeometry = shape.GetSphereProperties(out quaternion orientation);
                            unsafe
                            {
                                var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                                // 确保 collider 类型符合预期
                                Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Sphere));

                                // 比较球体属性
                                var sphereCollider = (SphereCollider*)physicsCollider.ColliderPtr;
                                Assert.That(sphereCollider->Radius, Is.PrettyCloseTo(sphereGeometry.Radius));
                                Assert.That(sphereCollider->Center, Is.PrettyCloseTo(sphereGeometry.Center));
                            }
                            break;
                        }
                    case ShapeType.Plane:
                        {
                            // 将形状的平面属性与烘焙的 PolygonCollider 属性进行比较，并期望它们相同
                            shape.GetPlaneProperties(out var center, out var size, out EulerAngles orientation);
                            PhysicsShapeExtensions.GetPlanePoints(center, size, orientation, out var vertex0, out var vertex1, out var vertex2, out var vertex3);

                            unsafe
                            {
                                var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                                // 确保 collider 类型符合预期
                                Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Quad));

                                // 比较 collider 属性
                                var polygonCollider = (PolygonCollider*)physicsCollider.ColliderPtr;
                                Assert.That(polygonCollider->IsQuad);

                                Assert.That(polygonCollider->Vertices[0], Is.PrettyCloseTo(vertex0));
                                Assert.That(polygonCollider->Vertices[1], Is.PrettyCloseTo(vertex1));
                                Assert.That(polygonCollider->Vertices[2], Is.PrettyCloseTo(vertex2));
                                Assert.That(polygonCollider->Vertices[3], Is.PrettyCloseTo(vertex3));
                            }

                            break;
                        }
                    case ShapeType.Mesh:
                        {
                            // 将形状的网格属性与烘焙的 MeshCollider 属性进行比较，并期望它们不受比例的影响。
                            // Note: 为简单起见，我们在这里使用网格边界进行比较。

                            var expectedBounds = ReadableMesh.bounds;
                            var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                            unsafe
                            {
                                var meshCollider = (MeshCollider*)physicsCollider.ColliderPtr;
                                // 确保 collider 类型符合预期
                                Assert.That(meshCollider->Type, Is.EqualTo(ColliderType.Mesh));

                                // 比较界限
                                var actualBounds = meshCollider->CalculateAabb();
                                Assert.That(actualBounds.Center, Is.PrettyCloseTo(expectedBounds.center));
                                Assert.That(actualBounds.Extents, Is.PrettyCloseTo(expectedBounds.size));
                            }

                            break;
                        }
                    case ShapeType.ConvexHull:
                        {
                            // 将形状的凸包属性与烘焙的 ConvexCollider 属性进行比较，并期望它们不受比例的影响。
                            // Note: 为简单起见，我们在这里使用网格边界进行比较。

                            var expectedBounds = ReadableMesh.bounds;
                            var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                            unsafe
                            {
                                var convexCollider = (ConvexCollider*)physicsCollider.ColliderPtr;
                                // 确保 collider 类型符合预期
                                Assert.That(convexCollider->Type, Is.EqualTo(ColliderType.Convex));

                                // 比较界限
                                var actualBounds = convexCollider->CalculateAabb();
                                Assert.That(actualBounds.Center, Is.PrettyCloseTo(expectedBounds.center));
                                Assert.That(actualBounds.Extents, Is.PrettyCloseTo(expectedBounds.size).Within(1e-2f));
                            }

                            break;
                        }
                }

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// 测试当游戏对象包含非均匀尺度时，生成的 entity 的局部变换具有恒等尺度，并且
        /// PostTransformMatrix 包含非均匀比例。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsNonUniformlyScaled_LocalTransformHasNoScale(
            [Values] ShapeType shapeType)
        {
            CreateHierarchyWithChildShape(shapeType);

            // 统一变换子 collider
            var k_NonUniformScale = new Vector3(1, 2, 3);
            Child.transform.localScale = k_NonUniformScale;

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // 期望局部变换尺度为恒等
                Assert.That(transform.Scale, Is.PrettyCloseTo(1));

                // 期望有一个 PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                // 期望 PostTransformMatrix 表示与局部变换相同的比例
                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                Assert.That(postTransformMatrix.Value, Is.PrettyCloseTo(float4x4.Scale(k_NonUniformScale)));

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// 测试当游戏对象在 world 空间中包含剪切时，所得 entity 的局部变换具有恒等尺度且
        /// PostTransformMatrix (containing the shear) and LocalTransform (containing the rigid body transform) components
        /// 一起表示与游戏对象相同的 world 变换。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsSheared_LocalTransformHasNoScale(
            [Values] ShapeType shapeType)
        {
            CreateHierarchyWithChildShape(shapeType);

            // 创建一个层次结构，导致子级 world 变换发生剪切
            Root.transform.localPosition = new Vector3(1f, 2f, 3f);
            Root.transform.localRotation = Quaternion.Euler(30f, 60f, 90f);
            Root.transform.localScale = new Vector3(3f, 5f, 7f);
            Parent.transform.localPosition = new Vector3(2f, 4f, 8f);
            Parent.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            Parent.transform.localScale = new Vector3(2f, 4f, 8f);
            Child.transform.localPosition = new Vector3(3f, 6f, 9f);
            Child.transform.localRotation = Quaternion.Euler(-30f, 20f, -10f);
            Child.transform.localScale = new Vector3(2f, 2f, 2f);

            var expectedColliderWorldTransform = (float4x4)Child.transform.localToWorldMatrix;
            Assert.That(expectedColliderWorldTransform.HasShear());

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // 期望局部变换尺度为恒等
                var localTransform = transform;
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // 期望有一个 PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // 期望后变换矩阵有剪切
                Assert.That(postTransformMatrix.Value.HasShear());

                // 检查 collider 的 world 变换是否符合预期
                var actualColliderWorldTransform = math.mul(localTransform.ToMatrix(), postTransformMatrix.Value);
                Assert.That(expectedColliderWorldTransform, Is.PrettyCloseTo(actualColliderWorldTransform));

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// 测试当游戏对象在 world 空间中包含非均匀缩放时，盒子或网格 collider
        /// 烘烤不均匀的比例。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsNonUniformlyScaled_ColliderHasBakedScale(
            [Values(ShapeType.Box, ShapeType.Mesh)] ShapeType shapeType)
        {
            TestNonUniformScaleOnCollider(new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)}, gameObjectToConvert =>
            {
                // 创建一个原始立方体，我们将其分配给测试中使用的游戏对象
                var cubeGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeMeshFilter = cubeGameObject.GetComponent<MeshFilter>();
                var cubeMeshRenderer = cubeGameObject.GetComponent<MeshRenderer>();
                Assert.That(cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null && cubeMeshRenderer != null);

                // 使用网格过滤器和立方体渲染器设置测试游戏对象
                var meshFilter = gameObjectToConvert.GetComponent<MeshFilter>();
                meshFilter.mesh = cubeMeshFilter.sharedMesh;
                var meshRenderer = gameObjectToConvert.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = cubeMeshRenderer.sharedMaterial;

                var shape = Child.GetComponent<PhysicsShapeAuthoring>();
                if (shapeType == ShapeType.Mesh)
                {
                    shape.SetMesh(cubeMeshFilter.sharedMesh);
                }
                else if (shapeType == ShapeType.Box)
                {
                    SetDefaultShape(shape, ShapeType.Box);
                }
                else
                {
                    throw new NotImplementedException();
                }

                UnityEngine.Object.DestroyImmediate(cubeGameObject);
            });
        }

        /// <summary>
        /// 测试当游戏对象在 world 空间中包含非均匀尺度时，凸 collider
        /// 烘焙了不均匀的比例。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsNonUniformlyScaled_ConvexColliderHasBakedScale()
        {
            TestNonUniformScaleOnCollider(new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)}, gameObjectToConvert =>
            {
                // 创建一个原始立方体，我们将其分配给测试中使用的游戏对象
                var cubeGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeMeshFilter = cubeGameObject.GetComponent<MeshFilter>();
                var cubeMeshRenderer = cubeGameObject.GetComponent<MeshRenderer>();
                Assert.That(cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null && cubeMeshRenderer != null);

                // 使用网格过滤器和立方体渲染器设置测试游戏对象
                var meshFilter = gameObjectToConvert.GetComponent<MeshFilter>();
                meshFilter.mesh = cubeMeshFilter.sharedMesh;
                var meshRenderer = gameObjectToConvert.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = cubeMeshRenderer.sharedMaterial;

                // 将网格分配给形状并使其成为凸形状
                var shape = Child.GetComponent<PhysicsShapeAuthoring>();
                Assert.That(shape != null);
                shape.SetConvexHull(ConvexHullGenerationParameters.Default, cubeMeshFilter.sharedMesh);

                UnityEngine.Object.DestroyImmediate(cubeGameObject);
            });
        }

        /// <summary>
        /// 测试当游戏对象在 world 空间中包含非均匀尺度时，球体 collider 具有非均匀尺度
        /// 规模烘烤。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsNonUniformlyScaled_SphereColliderHasBakedScale()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)}
            );

            // 导致儿童 collider 的尺度不均匀
            var nonUniformScale = new Vector3(1, 2, 3);
            Child.transform.localScale = nonUniformScale;

            var sphereShape = Child.GetComponent<PhysicsShapeAuthoring>();
            var unscaledRadius = 1.23f;
            sphereShape.SetSphere(new SphereGeometry { Radius = unscaledRadius });

            var expectedRadius = unscaledRadius * math.cmax(nonUniformScale);

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // 期望有一个具有身份比例的 LocalTransform component
                var entity = entities[0];
                Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
                var localTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // 期望有一个 PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // 期望后变换矩阵具有非均匀尺度但没有剪切
                Assert.That(postTransformMatrix.Value.HasNonUniformScale());
                Assert.That(postTransformMatrix.Value.HasShear(), Is.False);

                // 检查球体 collider 几何形状是否符合预期
                unsafe
                {
                    var sphereColliderPtr = (SphereCollider*)colliders[0].ColliderPtr;
                    Assert.That(sphereColliderPtr->Radius, Is.PrettyCloseTo(expectedRadius));
                }

                TestScaleChange(world, entity);
            }, 1);
        }

        /// <summary>
        /// 测试当游戏对象在 world 空间中包含非均匀尺度时，胶囊 collider 具有非均匀尺度
        /// 规模烘烤。
        /// </summary>
        [Test]
        public void PhysicsShapeConversionSystem_WhenGOIsNonUniformlyScaled_CapsuleColliderHasBakedScale()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)}
            );

            // 导致儿童 collider 的尺度不均匀
            var nonUniformScale = new Vector3(2, 3, 4);
            Child.transform.localScale = nonUniformScale;

            var capsuleShape = Child.GetComponent<PhysicsShapeAuthoring>();
            var unscaledRadius = 1.23f;
            var unscaledHeight = 4.2f;

            // 以 z 轴为中心轴的胶囊
            capsuleShape.SetCapsule(new CapsuleGeometryAuthoring() { Height = unscaledHeight, Radius = unscaledRadius, Orientation = quaternion.identity});
            int directionIndex = 2; // z 轴

            var expectedRadius = unscaledRadius * math.cmax(new float3(nonUniformScale) { [directionIndex] = 0f });
            var expectedHeight = unscaledHeight * nonUniformScale[directionIndex];

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // 期望有一个具有身份比例的 LocalTransform component
                var entity = entities[0];
                Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
                var localTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // 期望有一个 PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // 期望后变换矩阵具有非均匀尺度但没有剪切
                Assert.That(postTransformMatrix.Value.HasNonUniformScale());
                Assert.That(postTransformMatrix.Value.HasShear(), Is.False);

                // 检查球体 collider 几何形状是否符合预期
                unsafe
                {
                    var capsuleColliderPtr = (CapsuleCollider*)colliders[0].ColliderPtr;
                    Assert.That(capsuleColliderPtr->Radius, Is.PrettyCloseTo(expectedRadius));

                    var height = math.distance(capsuleColliderPtr->Vertex0, capsuleColliderPtr->Vertex1) + 2 * capsuleColliderPtr->Radius;
                    Assert.That(height, Is.PrettyCloseTo(expectedHeight));
                }

                TestScaleChange(world, entity);
            }, 1);
        }

        private static Vector3[] GetLocalScalesUniform()
        {
            return new[]
            {
                new Vector3(1, 1, 1),
                new Vector3(0.8f, 0.8f, 0.8f)
            };
        }

        /// <summary>
        /// 测试提供的 entities 中的 colliders 是否具有预期边界，假设它们都是从提供的形状类型烘焙的。
        /// </summary>
        void TestCollidersHaveExpectedBounds(ShapeType shapeType, World world, NativeArray<Entity> entities, NativeArray<PhysicsCollider> colliders,
            List<Tuple<Bounds, Transform>> expectedBounds)
        {
            // 期望 colliders 与网格边界具有相同的大小
            var foundIndices = new NativeHashSet<int>(entities.Length, Allocator.Temp);
            var manager = world.EntityManager;
            for (int i = 0; i < colliders.Length; i++)
            {
                var entity = entities[i];
                GetRigidBodyTransformationData(ref manager, entity, out var colliderWorldTransform,
                    out var colliderScale, out var colliderLocalToWorld);

                var matrixPrettyCloseTo = new MatrixPrettyCloseConstraint(colliderLocalToWorld.Value);
                // 通过比较 entity 的变换，找到与 collider 相对应的网格边界
                // 随着网格边界的变换
                var expectedBoundsIndex = expectedBounds.FindIndex(element =>
                    matrixPrettyCloseTo.ApplyTo((float4x4)element.Item2.localToWorldMatrix).IsSuccess);
                Assert.That(expectedBoundsIndex, Is.Not.EqualTo(-1));

                var notAlreadyPresent = foundIndices.Add(expectedBoundsIndex);
                Assert.That(notAlreadyPresent, NUnit.Framework.Is.True);

                var collider = colliders[i];
                var expectedBoundsElement = expectedBounds[expectedBoundsIndex];
                var colliderBounds = collider.Value.Value.CalculateAabb(colliderWorldTransform, colliderScale);
                var actualBoundsSize = colliderBounds.Extents;
                var expectedBoundsSize = expectedBoundsElement.Item1.size;
                var actualBoundsCenter = colliderBounds.Center;
                var expectedBoundsCenter = expectedBoundsElement.Item1.center;
                if (shapeType == ShapeType.Plane)
                {
                    // 忽略默认平面轴
                    actualBoundsSize[0] = expectedBoundsSize[0] = actualBoundsCenter[0] = expectedBoundsCenter[0] = 0;
                }

                Assert.That(actualBoundsSize, Is.PrettyCloseTo(expectedBoundsSize));
                Assert.That(actualBoundsCenter, Is.PrettyCloseTo(expectedBoundsCenter));
            }
        }

        /// <summary>
        /// 测试是否只有一种复合 collider 并且其边界对应于提供的边界的并集，假设它们都是从提供的形状类型烘焙的。
        /// </summary>
        protected void TestCompoundColliderHasExpectedUnionBounds(ShapeType shapeType, World world, NativeArray<Entity> entities, NativeArray<PhysicsCollider> colliders,
            List<Tuple<Bounds, Transform>> expectedBounds)
        {
            // 在这种情况下，预计只有一种化合物 collider
            Assert.That(colliders.Length, Is.EqualTo(1));

            ref var compoundCollider = ref colliders[0].Value.Value;
            Assert.That(compoundCollider.Type, Is.EqualTo(ColliderType.Compound));

            // 计算预期边界的并集以进行比较
            var expectedUnionBounds = new Bounds();
            foreach (var expectedBound in expectedBounds)
            {
                expectedUnionBounds.Encapsulate(expectedBound.Item1);
            }

            // 期望复合 collider 与网格边界的并集具有相同的大小
            var entity = entities[0];
            var manager = world.EntityManager;
            GetRigidBodyTransformationData(ref manager, entity, out var colliderWorldTransform,
                out var colliderScale, out var colliderLocalToWorld);

            var compoundColliderBounds = compoundCollider.CalculateAabb(colliderWorldTransform, colliderScale);
            var actualBoundsSize = compoundColliderBounds.Extents;
            var expectedBoundsSize = expectedUnionBounds.size;
            var actualBoundsCenter = compoundColliderBounds.Center;
            var expectedBoundsCenter = expectedUnionBounds.center;
            if (shapeType == ShapeType.Plane)
            {
                // 忽略默认平面轴
                actualBoundsSize[0] = expectedBoundsSize[0] = actualBoundsCenter[0] = expectedBoundsCenter[0] = 0;
            }

            Assert.That(actualBoundsSize, Is.PrettyCloseTo(expectedBoundsSize));
            Assert.That(actualBoundsCenter, Is.PrettyCloseTo(expectedBoundsCenter));
        }

        [Test]
        public void PhysicsShapeConversionSystem_NonStaticRigidbodyHierarchy_WithDifferentScales_CollidersHaveExpectedSize([Values(BodyMotionType.Kinematic, BodyMotionType.Dynamic)] BodyMotionType bodyMotionType, [Values] ShapeType shapeType, [Values] bool gameObjectIsStatic, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale)
        {
            TestCorrectColliderSizeInHierarchy(new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    foreach (var physicsShape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
                    {
                        SetDefaultShape(physicsShape, shapeType);
                    }

                    foreach (var rigidbody in Root.GetComponentsInChildren<PhysicsBodyAuthoring>())
                    {
                        rigidbody.MotionType = bodyMotionType;
                    }
                },
                3,
                (world, entities, colliders, expectedBounds) =>
                    TestCollidersHaveExpectedBounds(shapeType, world, entities, colliders, expectedBounds)
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_StaticRigidbodyHierarchy_WithIdentityScale_CollidersHaveExpectedSize([Values] ShapeType shapeType, [Values] bool gameObjectIsStatic)
        {
            TestCorrectColliderSizeInHierarchy(new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = new Vector3(1, 1, 1);

                    foreach (var physicsShape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
                    {
                        SetDefaultShape(physicsShape, shapeType);
                    }

                    foreach (var rigidbody in Root.GetComponentsInChildren<PhysicsBodyAuthoring>())
                    {
                        rigidbody.MotionType = BodyMotionType.Static;
                    }
                },
                3,
                (world, entities, colliders, expectedBounds) =>
                    TestCollidersHaveExpectedBounds(shapeType, world, entities, colliders, expectedBounds)
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_RigidbodyHierarchy_WithNonUniformScale_ColliderHasExpectedSize([Values] BodyMotionType bodyMotionType, [Values(ShapeType.Box, ShapeType.Mesh, ShapeType.ConvexHull)] ShapeType shapeType, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = false;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(PhysicsBodyAuthoring), typeof(PhysicsShapeAuthoring)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    foreach (var rigidbody in Root.GetComponentsInChildren<PhysicsBodyAuthoring>())
                    {
                        rigidbody.MotionType = bodyMotionType;
                    }

                    foreach (var physicsShape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
                    {
                        SetDefaultShape(physicsShape, shapeType);
                    }
                }, expectCompound
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_ColliderHierarchy_WithDifferentScales_CollidersHaveExpectedSize([Values] ShapeType shapeType, [Values] bool gameObjectIsStatic, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale)
        {
            TestCorrectColliderSizeInHierarchy(new[] {typeof(PhysicsShapeAuthoring)},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    foreach (var physicsShape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
                    {
                        SetDefaultShape(physicsShape, shapeType);
                    }
                },
                1,
                (world, entities, colliders, expectedBounds) =>
                    TestCompoundColliderHasExpectedUnionBounds(shapeType, world, entities, colliders, expectedBounds)
            );
        }

        [Test]
        public void PhysicsShapeConversionSystem_ColliderHierarchy_WithNonUniformScale_ColliderHasExpectedSize([Values(ShapeType.Box, ShapeType.Mesh, ShapeType.ConvexHull)] ShapeType shapeType, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = true;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(PhysicsShapeAuthoring)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    foreach (var physicsShape in Root.GetComponentsInChildren<PhysicsShapeAuthoring>())
                    {
                        SetDefaultShape(physicsShape, shapeType);
                    }
                }, expectCompound
            );
        }
    }
}
