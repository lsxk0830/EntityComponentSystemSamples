// 当 DeadTree.EnableColourChange 为 true 时，此 system 将树顶的颜色从绿色更改为橙​​色。
// 此 system 基于 TreeFlag 值迭代 entities，其中标志值 = TriggerChangeTreeColor（这是
// 在 TreeDeathSystem 中设置）。如果此 system 不是 run，则不是问题，并且标志值未更新。在此之后
// 状态更改时，TreeTop 和 TreeTrunk 中的标志值在 entity 被销毁之前不会用于任何用途。
// 颜色变化将在 LifeCycleStates.IsDead 状态期间发生。
// 目标：一个纯粹的视觉工具，用于显示树木何时被杀死并且是动态的
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Rendering;

namespace Unity.Physics
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateAfter(typeof(TreeDeathSystem))]
    public partial struct TreeColourChangeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TreeSpawnerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spawner = SystemAPI.GetSingleton<TreeSpawnerComponent>();
            if (!spawner.EnableColourChange)
            {
                return;
            }

            // 更改树顶的颜色：
            state.Dependency = new ChangeTreeColourJob
            {
                DeadTreeMaterialIndex = spawner.DeadTreeMaterialIndex
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        [WithAll(typeof(TreeState), typeof(PhysicsCollider), typeof(TreeTopTag))]
        partial struct ChangeTreeColourJob : IJobEntity
        {
            public int DeadTreeMaterialIndex;

            void Execute(ref TreeState treeState, ref MaterialMeshInfo materialMeshInfo)
            {
                if (treeState.Value == TreeState.States.TriggerChangeTreeColor)
                {
                    materialMeshInfo.Material = DeadTreeMaterialIndex;
                    treeState.Value = TreeState.States.TransitionToDeadDone;
                }
            }
        }
    }
}
