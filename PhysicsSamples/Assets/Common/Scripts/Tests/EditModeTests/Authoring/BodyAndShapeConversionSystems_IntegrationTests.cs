using System;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    class BodyAndShapeConversionSystems_IntegrationTests : BaseHierarchyConversionTest
    {
        [Test]
        public void ConversionSystems_WhenGOHasPhysicsBodyAndRigidbody_EntityUsesRigidbodyMass()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().Mass = 100f;
            Root.GetComponent<Rigidbody>().mass = 50f;

            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(0.02f)));
        }

        [Test]
        public void ConversionSystems_WhenGOHasPhysicsBodyAndRigidbody_EntityUsesRigidbodyDamping()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().LinearDamping = 1f;
            Root.GetComponent<Rigidbody>().linearDamping = 0.5f;

            TestConvertedData<PhysicsDamping>(damping => Assert.That(damping.Linear, Is.EqualTo(0.5f)));
        }

        [Test]
        public void ConversionSystems_WhenGOHasDynamicPhysicsBodyWithCustomGravity_AndKinematicRigidbody_EntityUsesRigidbodyKinematic()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Dynamic;
            Root.GetComponent<PhysicsBodyAuthoring>().GravityFactor = 2f;
            Root.GetComponent<Rigidbody>().isKinematic = true;

            TestConvertedData<PhysicsGravityFactor>(gravity => Assert.That(gravity.Value, Is.EqualTo(0.0f)));
        }

        [Test]
        public void ConversionSystems_WhenGOHasKinematicPhysicsBody_AndDynamicRigidbody_EntityHasNoGravityFactor()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Kinematic;
            Root.GetComponent<Rigidbody>().isKinematic = false;

            VerifyNoDataProduced<PhysicsGravityFactor>();
        }

        [Test]
        public void ConversionSystems_WhenGOHasDynamicPhysicsBodyWithDefaultGravity_AndDynamicRigidbodyWithCustomGravity_EntityHasNoGravityFactor()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Dynamic;
            Root.GetComponent<Rigidbody>().isKinematic = false;

            VerifyNoDataProduced<PhysicsGravityFactor>();
        }

        [Test]
        public void ConversionSystems_WhenGOHasDynamicPhysicsBodyWithNoPhysicsShape_AndDynamicRigidbodyWithNoCollider_EntityHasNoPhysicsCollider()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Dynamic;
            Root.GetComponent<PhysicsBodyAuthoring>().Mass = 100f;
            Root.GetComponent<Rigidbody>().isKinematic = false;
            Root.GetComponent<Rigidbody>().mass = 50f;

            VerifyNoDataProduced<PhysicsCollider>();
        }

        [Test]
        public void ConversionSystems_WhenGOHasDynamicPhysicsBody_AndKinematicRigidbody_EntityUsesRigidbodyMass()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Dynamic;
            Root.GetComponent<PhysicsBodyAuthoring>().Mass = 100f;
            Root.GetComponent<Rigidbody>().isKinematic = true;
            Root.GetComponent<Rigidbody>().mass = 50f;

            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(0.0f)));
        }

        [Test]
        public void ConversionSystems_WhenGOHasKinematicPhysicsBody_AndDynamicRigidbody_EntityUsesRigidbodyMass()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Kinematic;
            Root.GetComponent<PhysicsBodyAuthoring>().Mass = 100f;
            Root.GetComponent<Rigidbody>().isKinematic = false;
            Root.GetComponent<Rigidbody>().mass = 50f;

            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(0.02f)));
        }

        [Test]
        public void ConversionSystems_WhenGOHasStaticPhysicsBody_AndDynamicRigidbody_EntityHasNoGravityFactor()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Static;
            Root.GetComponent<Rigidbody>().isKinematic = false;

            VerifyNoDataProduced<PhysicsGravityFactor>();
        }

        [Test]
        public void ConversionSystems_WhenGOHasBody_GOIsActive_BodyIsConverted(
            [Values(
                typeof(Rigidbody),
                typeof(PhysicsBodyAuthoring)
             )]
            Type bodyType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { bodyType });

            // 默认条件下假定转换创建 PhysicsVelocity
            TestConvertedData<PhysicsVelocity>(v => Assert.That(v, Is.EqualTo(default(PhysicsVelocity))));
        }

        [Test, Timeout(300000)]
        public void ConversionSystems_WhenGOHasBody_AuthoringComponentDisabled_AuthoringDataNotConverted()
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(PhysicsBodyAuthoring) });
            Child.GetComponent<PhysicsBodyAuthoring>().enabled = false;

            // 默认条件下假定转换创建 PhysicsVelocity
            // 相应测试覆盖 ConversionSystems_WhenGOHasBody_GOIsActive_BodyIsConverted
            VerifyNoDataProduced<PhysicsVelocity>();
        }

        [Test]
        public void ConversionSystems_WhenGOHasBody_GOIsInactive_BodyIsNotConverted(
            [Values] Node inactiveNode,
            [Values(
                typeof(Rigidbody),
                typeof(PhysicsBodyAuthoring)
             )]
            Type bodyType
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { bodyType });
            GetNode(inactiveNode).SetActive(false);
            var numInactiveNodes = Root.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.activeSelf);
            Assume.That(numInactiveNodes, Is.EqualTo(2));

            // 默认条件下假定转换创建 PhysicsVelocity
            // 相应测试覆盖 ConversionSystems_WhenGOHasBody_GOIsActive_BodyIsConverted
            VerifyNoDataProduced<PhysicsVelocity>();
        }

        static Vector3[] GetDifferentScales()
        {
            return new[]
            {
                new Vector3(1.0f, 1.0f, 1.0f),
                new Vector3(0.542f, 0.542f, 0.542f),
                new Vector3(0.42f, 1.1f, 2.1f),
            };
        }

        // 确保我们在 baking 和物理体模拟中获得用户指定的质量属性
        // 在编辑时缩放游戏对象时。
        [Test]
        public void ConversionSystems_WithDifferentScales_EditTimeMassIsPreserved([Values] bool massOverride, [Values] bool withCollider, [ValueSource(nameof(GetDifferentScales))] Vector3 scale)
        {
            CreateHierarchy(new[] { typeof(PhysicsBodyAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            var rb = Root.GetComponent<PhysicsBodyAuthoring>();

            rb.MotionType = BodyMotionType.Dynamic;

            const float expectedMass = 42f;
            var expectedCOM = new Vector3(1f, 2f, 3f);
            var expectedInertia = new Vector3(2f, 3f, 4f);
            var expectedInertiaRot = Quaternion.Euler(10f, 20f, 30f);

            rb.Mass = expectedMass;
            MassProperties automaticMassProperties;

            if (withCollider)
            {
                var boxCollider = Root.AddComponent<PhysicsShapeAuthoring>();
                var boxColliderSize = new float3(3, 4, 5);
                boxCollider.SetBox(new BoxGeometry { Size = boxColliderSize, Orientation = quaternion.identity});

                // 我们期望质量属性与基于提供的比例的盒子的缩放版本相对应。
                automaticMassProperties = MassProperties.CreateBox(boxColliderSize * scale);
            }
            else
            {
                // 我们期望质量属性对应于默认单位球体质量属性的缩放版本。

                // 特殊情况：没有 collider，我们使用默认质量属性。在这种情况下，当尺度不均匀时
                // 目前，我们不将其烘焙到 collider 中，因此也不缩放质量属性。
                var radius = 1f;
                if (!float4x4.Scale(scale).HasNonUniformScale())
                {
                    radius *= scale[0];
                }
                automaticMassProperties = MassProperties.CreateSphere(radius);
            }

            if (massOverride)
            {
                rb.CustomMassDistribution = new MassDistribution
                {
                    Transform = new RigidTransform(expectedInertiaRot, expectedCOM),
                    InertiaTensor = expectedInertia
                };
            }
            else
            {
                expectedCOM = automaticMassProperties.MassDistribution.Transform.pos;
                expectedInertia = automaticMassProperties.MassDistribution.InertiaTensor;
                expectedInertiaRot = automaticMassProperties.MassDistribution.Transform.rot;
            }

            // 缩放对象
            Root.transform.localScale = scale;

            TestExpectedMass(expectedMass, expectedCOM, expectedInertia, expectedInertiaRot);
        }
    }
}
