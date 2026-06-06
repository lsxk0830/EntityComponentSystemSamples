using Unity.Entities;
using UnityEngine;

namespace ExampleCode.Bakers
{
    // 我们想要的示例 component
    // 定义 authoring component 和 baker。
    public struct EnergyShield : IComponentData
    {
        public int HitPoints;
        public int MaxHitPoints;
        public float RechargeDelay;
        public float RechargeRate;
    }

    // authoring component 用于 EnergyShield。
    // 就其本身而言，authoring component 只是普通的 MonoBehavior。
    public class EnergyShieldAuthoring : MonoBehaviour
    {
        // 请注意，authoring component 没有 HitPoints 字段。
        // 只要我们不需要设置 HitPoints 就可以了
        // 编辑器中的值。

        // （事实上​​，这些名称反映了字段
        // EnergyShield 不是必需的。）

        public int MaxHitPoints;
        public float RechargeDelay;
        public float RechargeRate;

        // baker 适合我们的 EnergyShield authoring component。
        // 对于 entity subscene 中的每个 GameObject，baking 创建一个
        // 对应 entity。这个 baker 是 run 每一次
        // 附加到任何 GameObject 的 EnergyShieldAuthoring 实例
        // entity subscene。
        public class Baker : Baker<EnergyShieldAuthoring>
        {
            public override void Bake(EnergyShieldAuthoring authoring)
            {
                // TransformUsageFlags 指定 components 的哪种变换
                // entity 应该有。“无”意味着它不需要任何。
                var entity = GetEntity(TransformUsageFlags.None);

                // 这个简单的 baker 仅向 entity 添加一个 component。
                AddComponent(entity, new EnergyShield
                {
                    HitPoints = authoring.MaxHitPoints,
                    MaxHitPoints = authoring.MaxHitPoints,
                    RechargeDelay = authoring.RechargeDelay,
                    RechargeRate = authoring.RechargeRate,
                });
            }
        }
    }

    /*
     *  Baker 必须注册其访问的数据。对于 authoring component，这是自动的，但对于其他
     *  data (assets, prefabs, and other GameObject components), you must access them through
     *  Baker 方法确保 Baker 知道它们：
     */

    public struct MyComponent : IComponentData
    {
        public int A;
        public float B;
        public Entity Prefab;
    }

    public class MyAuthoring : MonoBehaviour
    {
        public GameObject prefab;
        public GameObject otherGO;
        public Mesh mesh;

        public class Baker : Baker<MyAuthoring>
        {
            public override void Bake(MyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // 为此，请使用 Baker 提供的函数来访问其他 components
                // 而不是 GameObject 提供的。同样，如果您从资产访问数据，
                // 您需要为其创建依赖项，以便在资产更改时 Baker 重新运行。
                // 如果网格本身发生任何变化，我们想要重新烘焙。

                var transform = GetComponent<Transform>(authoring.otherGO);
                DependsOn(authoring.mesh);

                AddComponent(entity, new MyComponent
                {
                    A = authoring.mesh.vertexCount,
                    B = transform.localPosition.x,
                    // 要注册并转换 Prefabs，请在 baker 中调用 `GetEntity`：
                    Prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
