using NUnit.Framework;
using System.Collections;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Physics.Tests
{
#if HAVOK_PHYSICS_EXISTS && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_PS5 || UNITY_SWITCH || UNITY_GAMECORE)

    [TestFixture]
    class HavokPhysicsSamplesTestMT : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(UnityPhysicsSamplesTest.GetScenes))] string scenePath)
        {
#if UNITY_GAMECORE
            // 我们使用 HavokPhysics 跳过测试
            if (scenePath.Contains("/Modify - Surface Velocity.unity") ||
                scenePath.Contains("/Modify - Contact Jacobians.unity") ||
                scenePath.Contains("/JacobianModifiersUT.unity") ||
                scenePath.Contains("/Animation/Animation.unity"))
            {
                Debug.Log("Skipping " + scenePath);
                LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                yield break;
            }
#endif

#if UNITY_IOS
            // 我们使用 HavokPhysics 跳过测试
            if (scenePath.Contains("/Joints - Ragdolls.unity") ||
                scenePath.Contains("/ChangeGroundFilter.unity") ||
                scenePath.Contains("/ChangeGroundFilterChangeCollider.unity") ||
                scenePath.Contains("/ChangeGroundFilterChangeMotionType.unity") ||
                scenePath.Contains("/ChangeGroundFilterNewCollider.unity") ||
                scenePath.Contains("/ChangeGroundFilterRemove.unity") ||
                scenePath.Contains("/ChangeGroundFilterTeleport.unity") ||
                scenePath.Contains("/CollisionResponse.None.unity") ||
                scenePath.Contains("/ChangeCompoundFilter.unity") ||
                scenePath.Contains("/Compound.unity") ||
                scenePath.Contains("/FixedAngleGrid.unity") ||
                scenePath.Contains("/InvalidJoint.unity") ||
                scenePath.Contains("/RagdollGrid.unity") ||
                scenePath.Contains("/SoftJoint.unity") ||
                scenePath.Contains("/SingleThreadedRagdoll.unity") ||
                scenePath.Contains("/Terrain_Triangles.unity") ||
                scenePath.Contains("/Terrain_VertexSamples.unity"))
            {
                Debug.Log("Skipping " + scenePath);
                LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                yield break;
            }
#endif

#if UNITY_STANDALONE_LINUX
             // 我们使用 HavokPhysics 跳过测试
             if (scenePath.Contains("/VehicleOverTerrain.unity"))
             {
                 Debug.Log("Skipping " + scenePath);
                 LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                 yield break;
             }
#endif

            // 不要创建有关剩余试用天数的日志消息
            PlayerPrefs.SetInt("Havok.Auth.SuppressDialogs", 1);

            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            // 启用多线程 Havok 模拟
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.HavokPhysics, true);

            yield return LoadSceneAndSimulate(scenePath);

            PlayerPrefs.DeleteKey("Havok.Auth.SuppressDialogs");
        }
    }

    [TestFixture]
    class HavokPhysicsSamplesTestST : UnityPhysicsSamplesTest
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator LoadScenes([ValueSource(nameof(UnityPhysicsSamplesTest.GetScenes))] string scenePath)
        {
#if UNITY_GAMECORE
            // 我们使用 HavokPhysics 跳过测试
            if (scenePath.Contains("/Modify - Surface Velocity.unity") ||
                scenePath.Contains("/Modify - Contact Jacobians.unity") ||
                scenePath.Contains("/JacobianModifiersUT.unity") ||
                scenePath.Contains("/Animation/Animation.unity"))
            {
                Debug.Log("Skipping " + scenePath);
                LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                yield break;
            }
#endif

#if UNITY_IOS
            // 我们使用 HavokPhysics 跳过测试
            if (scenePath.Contains("/Joints - Ragdolls.unity") ||
                scenePath.Contains("/ChangeGroundFilter.unity") ||
                scenePath.Contains("/ChangeGroundFilterChangeCollider.unity") ||
                scenePath.Contains("/ChangeGroundFilterChangeMotionType.unity") ||
                scenePath.Contains("/ChangeGroundFilterNewCollider.unity") ||
                scenePath.Contains("/ChangeGroundFilterRemove.unity") ||
                scenePath.Contains("/ChangeGroundFilterTeleport.unity") ||
                scenePath.Contains("/CollisionResponse.None.unity") ||
                scenePath.Contains("/ChangeCompoundFilter.unity") ||
                scenePath.Contains("/Compound.unity") ||
                scenePath.Contains("/FixedAngleGrid.unity") ||
                scenePath.Contains("/InvalidJoint.unity") ||
                scenePath.Contains("/RagdollGrid.unity") ||
                scenePath.Contains("/SoftJoint.unity") ||
                scenePath.Contains("/SingleThreadedRagdoll.unity") ||
                scenePath.Contains("/Terrain_Triangles.unity") ||
                scenePath.Contains("/Terrain_VertexSamples.unity"))
            {
                Debug.Log("Skipping " + scenePath);
                LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                yield break;
            }
#endif

#if UNITY_STANDALONE_LINUX
             // 我们使用 HavokPhysics 跳过测试
             if (scenePath.Contains("/VehicleOverTerrain.unity"))
             {
                 Debug.Log("Skipping " + scenePath);
                 LogAssert.Expect(LogType.Log, "Skipping " + scenePath);
                 yield break;
             }
#endif

            // 不要创建有关剩余试用天数的日志消息
            PlayerPrefs.SetInt("Havok.Auth.SuppressDialogs", 1);

            // 记录 scene 名称，以防 Unity 崩溃并且测试结果未写出。
            Debug.Log("Loading " + scenePath);
            LogAssert.Expect(LogType.Log, "Loading " + scenePath);

            // 启用单线程 Havok 模拟
            ConfigureSimulation(World.DefaultGameObjectInjectionWorld, SimulationType.HavokPhysics, false);

            yield return LoadSceneAndSimulate(scenePath);

            PlayerPrefs.DeleteKey("Havok.Auth.SuppressDialogs");
        }
    }
#endif
}
