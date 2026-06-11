using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HelloCube.GameObjectSync
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct RotatorInitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DirectoryManaged>();
            state.RequireForUpdate<ExecuteGameObjectSync>();
        }

        // 这个 OnUpdate 访问托管对象，因此无法进行突发编译。
        public void OnUpdate(ref SystemState state)
        {
            var directory = SystemAPI.ManagedAPI.GetSingleton<DirectoryManaged>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 从 prefab 实例化关联的 GameObject。
            foreach (var (rotationSpeed, entity) in SystemAPI.Query<RefRO<RotationSpeed>>()
                         .WithNone<RotatorGO>()
                         .WithEntityAccess())
            {
                var go = GameObject.Instantiate(directory.RotatorPrefab);
                go.name = "自定义CubeName";

                // 当我们迭代它们时，我们无法将 components 添加到 entities，因此我们使用 ECB 推迟更改。
                ecb.AddComponent(entity, new RotatorGO(go));
            }

            ecb.Playback(state.EntityManager);
        }
    }

    public class RotatorGO : IComponentData
    {
        public GameObject Value;

        public RotatorGO(GameObject value)
        {
            Value = value;
        }

        // 每个 IComponentData 类都必须有一个无参数构造函数。
        public RotatorGO()
        {
        }
    }
#endif
}

