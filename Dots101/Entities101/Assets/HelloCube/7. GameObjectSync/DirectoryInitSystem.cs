using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Unity.Burst;

namespace HelloCube.GameObjectSync
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public partial struct DirectoryInitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 在更新之前我们需要等待 scene 加载，所以我们必须在 RequireForUpdate
            // 从 scene 加载至少一种 component 类型。

            state.RequireForUpdate<ExecuteGameObjectSync>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            var directoryObject = GameObject.Find("Directory");
            if (directoryObject == null)
            {
                throw new Exception("GameObject 'Directory' not found.");
            }

            var directory = directoryObject.GetComponent<Directory>();

            var directoryManaged = new DirectoryManaged();
            directoryManaged.RotatorPrefab = directory.RotatorPrefab;
            directoryManaged.RotationToggle = directory.RotationToggle;

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, directoryManaged);
        }
    }

    public class DirectoryManaged : IComponentData
    {
        public GameObject RotatorPrefab;
        public Toggle RotationToggle;

        // 每个 IComponentData 类都必须有一个无参数构造函数。
        public DirectoryManaged() { }
    }
#endif
}
