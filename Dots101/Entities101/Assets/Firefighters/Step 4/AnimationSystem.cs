using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Tutorials.Firefighters
{
    public partial struct AnimationSystem : ISystem
    {
        private bool isInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<Bot>();
            state.RequireForUpdate<ExecuteAnimation>();
        }

        // 由于此更新访问托管对象，因此无法进行 Burst 编译，
        // 所以我们不添加 [BurstCompile] 属性。
        public void OnUpdate(ref SystemState state)
        {
            if (!isInitialized)
            {
                isInitialized = true;

                var configEntity = SystemAPI.GetSingletonEntity<Config>();
                var configManaged = state.EntityManager.GetComponentObject<ConfigManaged>(configEntity);

                var ecb = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (transform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Bot>()
                             .WithEntityAccess())
                {
                    var botAnimation = new BotAnimation();
                    var go = GameObject.Instantiate(configManaged.BotAnimatedPrefabGO);
                    botAnimation.AnimatedGO = go;
                    go.transform.localPosition = (Vector3)transform.ValueRO.Position;
                    ecb.AddComponent(entity, botAnimation);

                    // 禁用渲染
                    ecb.RemoveComponent<MaterialMeshInfo>(entity);
                }

                ecb.Playback(state.EntityManager);
            }

            var isMovingId = Animator.StringToHash("IsMoving");

            foreach (var (bot, transform, botAnimation) in
                     SystemAPI.Query<RefRO<Bot>, RefRO<LocalTransform>, BotAnimation>())
            {
                var pos = (Vector3)transform.ValueRO.Position;
                pos.y = 0;
                botAnimation.AnimatedGO.transform.localPosition = pos;
                botAnimation.AnimatedGO.transform.localRotation = (Quaternion)transform.ValueRO.Rotation;

                var animator = botAnimation.AnimatedGO.GetComponent<Animator>();
                animator.SetBool(isMovingId, bot.ValueRO.IsMoving());
            }
        }
    }
}