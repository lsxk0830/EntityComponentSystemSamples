using System;
using AutoAuthoring;
using Unity.Entities;
using Unity.Mathematics;

namespace Baking.AutoAuthoring.BakingTypeAutoAuthoring
{
    // 在此示例中，component 复合体将许多值得一起编辑的值捆绑在一起。
    // 通过定义 AutoAuthoring<Complex> MonoBehaviour，component 在检查器中自动可见和可编辑。

    // authoring component BakingTypeAutoAuthoring 实现了自定义 Baker，允许我们为运行时数据创建不同的表示形式。
    // 例如，baker 在主 entity 上添加 Speed component，确保运行时此属性的最佳数据访问。
    // 此外，其他属性是从 baking system 中的 authoring component 中提取的。

    // 由于 Complex component 具有属性[BakingType]，因此在最终的运行时数据中不会被序列化。

    // 相同的模式还可以用于重构运行时数据，以确保最佳的运行时访问，同时
    // 提供必要的灵活性，以方便的方式定义 authoring components。
    // 这有助于轻松进行原型设计，同时为以后优化数据留下了余地。


    // Authoring component，为方便编辑数据而优化。
    public class BakingTypeAutoAuthoring : AutoAuthoring<Complex>
    {
        class Baker : Baker<BakingTypeAutoAuthoring>
        {
            public override void Bake(BakingTypeAutoAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Speed { Value = authoring.Data.Properties.Speed });

                // 我们这里不调用 GetEntity，因为 Reference 字段是在 ComponentAuthoring baker 中分配的。
                AddComponent(entity, new SpawnPrefab() { Prefab = authoring.Data.Reference });
            }
        }
    }

    [Serializable]
    public struct Properties
    {
        public float3 Speed;
        public float3 Position;
        public float3 Rotation;
    }

    [BakingType]
    [Serializable]
    public struct Complex : IComponentData
    {
        public Entity Reference;
        public Properties Properties;
    }

    public struct Speed : IComponentData
    {
        public float3 Value;
    }

    public struct SpawnPrefab : IComponentData
    {
        public Entity Prefab;
    }
}
