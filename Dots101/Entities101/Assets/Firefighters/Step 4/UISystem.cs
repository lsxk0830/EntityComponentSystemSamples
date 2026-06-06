using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Tutorials.Firefighters
{
    public partial struct UISystem : ISystem
    {
        private bool initialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ExecuteUI>();
        }

        // 由于此更新访问托管对象，因此无法进行 Burst 编译，
        // 所以我们不添加 [BurstCompiled] 属性。
        public void OnUpdate(ref SystemState state)
        {
            var configEntity = SystemAPI.GetSingletonEntity<Config>();
            var configManaged = state.EntityManager.GetComponentObject<ConfigManaged>(configEntity);

            if (!initialized)
            {
                initialized = true;

                configManaged.UIController = GameObject.FindFirstObjectByType<UIController>();
            }

            var shouldReposition = configManaged.UIController.ShouldReposition();
            var totalFiresDoused = 0;

            foreach (var (team, entity) in
                     SystemAPI.Query<RefRO<Team>>()
                         .WithEntityAccess())
            {
                totalFiresDoused += team.ValueRO.NumFiresDoused;

                if (shouldReposition)
                {
                    SystemAPI.SetComponentEnabled<RepositionLine>(entity, true);
                }
            }

            configManaged.UIController.SetNumFiresDoused(totalFiresDoused);
        }
    }
}