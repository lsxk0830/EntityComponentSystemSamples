using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics
{
    public class TreeSpawnerAuthoring : MonoBehaviour
    {
        public GameObject TreePrefab;
        public UnityEngine.Material DeadTreeMaterial;
        public float TreeDensity = 1.0f;
        public float TreeGrowProbability = 0.25f;
        public float MaxGrowTime = 15;
        public float MaxDeadTime = 15;
        public float ReGrowDelay = 5;
        public float GroundSize = 15;
        public bool EnableColourChange = false;

        class TreeSpawnerBaker : Baker<TreeSpawnerAuthoring>
        {
            public override void Bake(TreeSpawnerAuthoring authoring)
            {
                DependsOn(authoring.TreePrefab);
                if (authoring.TreePrefab == null) return;
                var prefabTreeEntity = GetEntity(authoring.TreePrefab, TransformUsageFlags.Dynamic);

                // 添加到 prefab
                var createComponent = new TreeSpawnerComponent
                {
                    TreeEntity = prefabTreeEntity,
                    DeadTreeMaterial = new UnityObjectRef<UnityEngine.Material> { Value = authoring.DeadTreeMaterial },
                    MaxGrowTime = authoring.MaxGrowTime,
                    MaxDeadTime = authoring.MaxDeadTime,
                    ReGrowDelay = authoring.ReGrowDelay,
                    TreeDensity = authoring.TreeDensity,
                    TreeGrowProbability = authoring.TreeGrowProbability,
                    GroundSize = authoring.GroundSize,
                    EnableColourChange = authoring.EnableColourChange
                };
                var treeSpawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(treeSpawnerEntity, createComponent);
            }
        }
    }

    // 这个 component 使用一次来初始化树生成器，然后删除
    public struct TreeSpawnerComponent : IComponentData
    {
        public Entity TreeEntity;
        public UnityObjectRef<UnityEngine.Material> DeadTreeMaterial;
        public int DeadTreeMaterialIndex;
        public float TreeDensity;
        public float TreeGrowProbability;
        public float MaxGrowTime;
        public float MaxDeadTime;
        public float ReGrowDelay;
        public float GroundSize;
        public bool EnableColourChange;
    }

    // A component 放置在树 prefab entity （又名：树根）上，用于跟踪树的生命周期
    public struct TreeComponent : IComponentData
    {
        public float3 SpawningPosition;
        public float GrowTime;
        public float DeadTime;

        public int GrowTimer;
        public int DeathTimer;
        public int RegrowTimer;
        public LifeCycleStates LifeCycleTracker;
    }

    // 用于跟踪树的生命周期，而不是添加标签和进行结构更改
    // 对于名称以“Is_”开头的状态：这些状态都会减少各种计时器。
    // 对于名称以“TransitionTo_”开头的状态：这些状态用作向外部 systems 发出信号的标志
    public enum LifeCycleStates
    {
        IsGrowing,                  // 倒计时状态递减 GrowTimer
        TransitionToDead,           // TreeDeathSystem 的标志：将树变成橙色，将树干和顶部从静态主体过渡到动态主体
        IsDead,                     // 倒计时状态递减 DeathTimer
        TransitionToDelete,         // TreeDeletionSystem 的标志：删除树顶和树干 entities
        IsRegrown,                  // 倒计时状态递减 RegrowTimer
        TransitionToInsert          // TreeRegrowSystem 的标志：重生树
    }

    // 跟踪添加到树的每个部分（根、顶部、树干）的状态，以便为 TreeLifetimeSystem 外部的 systems 识别它
    public struct TreeState : IComponentData
    {
        public enum States : byte
        {
            Default,                    // 继续
            TriggerTreeGrowthSystem,    // 套装：TreeGrowthSystem，已使用：TreeGrowthSystem，生命周期：IsGrowing
            TriggerWholeTreeToDynamic,  // 套装：TreeLifecycleSystem，已使用：TreeDeathSystem，生命周期：TransitionToDead
            TriggerChangeTreeColor,     // 设置：TransitionToDead 期间的 TreeLifecycleSystem，已使用：TreeDeathSystem，生命周期：TransitionToDead
            TransitionToDeadDone,       // 套装：TreeDeathSystem，已使用：TreeDeathSystem，生命周期：TransitionToDead
            TriggerDeleteTrunkAndTop,   //套装：TreeLifecycleSystem，已使用：TreeDeletionSystem，生命周期：TransitionToDelete
        }

        public static TreeState Default => new TreeState { Value = States.Default };

        public States Value;
    }

    // TreeRegrowSystem 中使用的标签，用于识别 entities 需要第二次重生的内容
    public struct TempIntermediateTreeSpawningTag : IComponentData {}
}
