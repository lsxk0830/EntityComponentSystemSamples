using System;
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Physics.Tests.Authoring
{
    // Physics 子 scene 工作流程的形状转换测试
    class PhysicsShape_SubScene_IntegrationTests
        : ConversionSystem_SubScene_IntegrationTestsFixture
    {
        // 创建一个子 scene，填充并加载它。
        // 然后，执行验证操作，进入播放模式并再次执行验证操作。
        IEnumerator BaseColliderSubSceneTest(Action createSubSceneObjects, Action validation)
        {
            // 创建一个子 scene，填充并加载它。
            Assert.IsNull(SubSceneManaged);
            Assert.AreEqual(Entity.Null, SubSceneEntity);

            // 创建子 scene
            CreateAndLoadSubScene(createSubSceneObjects);
            Assert.IsNotNull(SubSceneManaged);

            // 等待子 scene 通过跳帧加载
            while (!SceneSystem.IsSceneLoaded(World.DefaultGameObjectInjectionWorld.Unmanaged, SubSceneEntity))
            {
                yield return null;
            }

            // 启用子 scene 进行编辑
            Scenes.Editor.SubSceneUtility.EditScene(SubSceneManaged);

            // 第一阶段：
            // 确保我们处于编辑模式并验证
            Assume.That(Application.isPlaying, Is.False);

            // 调用验证函数
            validation();

            // 第二阶段：
            // 进入播放模式并验证
            yield return new EnterPlayMode();

            // 在验证之前确保我们处于播放模式
            while (!Application.isPlaying)
            {
                yield return null;
            }

            // 调用验证函数
            validation();
        }

        // 测试物理 colliders 中的 collider 斑点是否相同（如果它们相同）
        [UnityTest]
        public IEnumerator TestSharedColliderBlobs()
        {
            PhysicsShapeAuthoring collider1, collider2;
            Action creation = () =>
            {
                collider1 = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<PhysicsShapeAuthoring>();
                collider2 = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<PhysicsShapeAuthoring>();

                // 我们不希望在此测试中发生实际碰撞
                collider1.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
                collider2.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;

                // 使用相同的 colliders
                collider1.SetBox(default);
                collider2.SetBox(default);

                // 确保相同的 colliders 可以通过禁用“强制唯一”设置来共享单个 collider Blob
                collider1.ForceUnique = false;
                collider2.ForceUnique = false;
            };

            Action validation = () =>
            {
                unsafe
                {
                    using (var group = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsCollider>()))
                    {
                        using var colliderComponents = group.ToComponentDataArray<PhysicsCollider>(Allocator.Temp);
                        Assume.That(colliderComponents, Has.Length.EqualTo(2));
                        var colliderComponent1 = colliderComponents[0];
                        var colliderComponent2 = colliderComponents[1];
                        // 确保两个 collider blob 是共享的，因此它们的指针是相同的
                        Assume.That((IntPtr)colliderComponent1.ColliderPtr, Is.EqualTo((IntPtr)colliderComponent2.ColliderPtr));

                        // 确保 colliders 指示它们不是唯一的。
                        foreach (var collider in colliderComponents)
                        {
                            Assume.That(collider.IsUnique, Is.False);
                        }
                    }
                }
            };

            return BaseColliderSubSceneTest(creation, validation);
        }

        // 测试物理中的 collider 斑点 colliders 是唯一的，尽管它们是相同的（如果它们被迫是唯一的）
        [UnityTest]
        public IEnumerator TestUniqueColliderBlobs()
        {
            PhysicsShapeAuthoring collider1, collider2;
            Action creation = () =>
            {
                collider1 = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<PhysicsShapeAuthoring>();
                collider2 = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<PhysicsShapeAuthoring>();

                // 我们不希望在此测试中发生实际碰撞
                collider1.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
                collider2.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;

                // 使用相同的 colliders
                collider1.SetBox(default);
                collider2.SetBox(default);

                // 强制 collider blob 在两个 PhysicsCollider components 中都是唯一的
                collider1.ForceUnique = true;
                collider2.ForceUnique = true;
            };

            Action validation = () =>
            {
                unsafe
                {
                    using (var group = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsCollider>()))
                    {
                        using var colliderComponents = group.ToComponentDataArray<PhysicsCollider>(Allocator.Temp);
                        Assume.That(colliderComponents, Has.Length.EqualTo(2));
                        var colliderComponent1 = colliderComponents[0];
                        var colliderComponent2 = colliderComponents[1];
                        // 确保两个 collider blob 不相同
                        Assume.That((IntPtr)colliderComponent1.ColliderPtr, Is.Not.EqualTo((IntPtr)colliderComponent2.ColliderPtr));

                        // 确保 colliders 指示它们是唯一的。
                        foreach (var collider in colliderComponents)
                        {
                            Assume.That(collider.IsUnique, Is.True);
                        }
                    }
                }
            };

            return BaseColliderSubSceneTest(creation, validation);
        }
    }
}
