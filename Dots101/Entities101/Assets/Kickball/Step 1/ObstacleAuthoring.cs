using Unity.Entities;
using UnityEngine;

namespace Tutorials.Kickball.Step1
{
    // “authoring component”是具有相应 Baker<T> 类的 MonoBehaviour。
    public class ObstacleAuthoring : MonoBehaviour
    {
        // 在这个简单的情况下，authoring component 本身不需要任何字段。
        // 将 baker 嵌套在其关联的 authoring component 中是
        // 规定的样式，但这不是必需的。
        // baker 类的名称并不重要：重要的是我们已经
        // 定义了一个继承 Baker<ObstacleAuthoring>的类。
        class Baker : Baker<ObstacleAuthoring>
        {
            // Bake() is called every time the authoring component gets re-baked.
            // authoring component 被传递给参数（尽管在本例中我们忽略该参数）。
            public override void Bake(ObstacleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // 将障碍物 component 添加到 baking 中生成的 entity 中
                // 来自 authoring component 的 GameObject。
                AddComponent<Obstacle>(entity);
            }
        }
    }

    // 基本 entity component 类型由实现 IComponentData 的结构体定义。
    // 该接口没有方法：它只是将类型标记为 component 类型。
    public struct Obstacle : IComponentData
    {
        // 该结构可以包含任何非托管字段，但在本例中我们将其留空。
        // 空结构称为“标记 component”。虽然它们不包含任何数据，但标签 components 可以
        // 仍然像任何其他 component 类型一样被查询，因此它们对于标记 entities 很有用。
        // 这个障碍物标签 component 被添加到所有障碍物 entities 中，这样我们就可以为所有障碍物 query。
    }
}
