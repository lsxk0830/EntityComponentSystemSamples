using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Baking.BakingDependencies
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class ImageGeneratorAuthoring : MonoBehaviour
    {
        // 该图像预计将压缩设置为无并启用读/写。
        public Texture2D Image;
        public ImageGeneratorInfo Info;

        class Baker : Baker<ImageGeneratorAuthoring>
        {
            public override void Bake(ImageGeneratorAuthoring authoring)
            {
                // 以下 4 件事会导致 baker 重新运行：

                // 1. 修改图像。
                DependsOn(authoring.Image);

                // 2. 修改 ImageGeneratorInfo 可编写脚本的对象。
                DependsOn(authoring.Info);

                // 3、修改 ImageGeneratorInfo 引用的 Mesh。
                DependsOn(authoring.Info.Mesh);

                // 4.修改 ImageGeneratorInfo 引用的 Material。
                DependsOn(authoring.Info.Material);

                // 检查 authoring component 是否正确设置，如果没有，请尽早退出。
                // 请注意，这些检查是在声明依赖项之后进行的，这是故意的。
                // 因为空引用可以有两种情况：
                // - 实际的空引用（未设置任何内容）
                // - 对缺失内容的引用（e.g。已从资源文件夹中删除的纹理）
                // 在第二种情况下，注册对丢失资产的引用的依赖将导致
                // 当丢失的资产恢复时，baker 重新运行。这是预期的行为。
                if (authoring.Info == null) return;
                if (authoring.Info.Mesh == null) return;
                if (authoring.Image == null) return;

                // 使用 GetComponent 方法访问其他 authoring components 非常重要，因为
                // 这样做会注册依赖项。如果我们在这里使用 authoring.transform 来代替，
                // 依赖项不会被注册，并且在实时 baking 时移动 authoring GameObject
                // 不会按应有的方式重新运行 baker。
                var transform = GetComponent<Transform>();

                var spacing = authoring.Info.Spacing;
                var sizeX = authoring.Image.width;
                var sizeY = authoring.Image.height;
                var centerOffset = transform.TransformPoint(new float3(sizeX - 1, sizeY - 1, 0) / -2.0f);
                var axisX = transform.right;
                var axisY = transform.up;

                // 此 baker 创建的附加 entities 必须进一步处理
                // 通过 baking system。为了跟踪那些 entities 并在它们之间进行通信
                // baker 和 baking system、entities 存储在附加到的动态数组中
                // 当前的 entity。
                var mainEntity = GetEntity(TransformUsageFlags.None);
                var entities = AddBuffer<ImageGeneratorEntity>(mainEntity);

                var typeSet = new ComponentTypeSet(typeof(LocalTransform),
                    typeof(LocalToWorld),
                    typeof(URPMaterialPropertyBaseColor));

                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        var pixel = authoring.Image.GetPixel(x, y);

                        // 跳过完全透明的像素。
                        if (pixel.a == 0) continue;

                        // ManualOverride 阻止变换 baking system 添加变换 components
                        // 到那些 entities，这会与我们在此处明确添加的内容相冲突。
                        var entity = CreateAdditionalEntity(TransformUsageFlags.ManualOverride);

                        // 一次添加所有 components 然后设置它们的值会更有效
                        // 而不是将它们一一相加。我们避免以这种方式在多个 archetypes 之间移动 entity。

                        AddComponent(entity, typeSet);

                        float3 localPosition = (axisX * x + axisY * y + centerOffset) * (1 + spacing);

                        SetComponent(entity, LocalTransform.FromPosition(localPosition));
                        SetComponent(entity, new URPMaterialPropertyBaseColor { Value = (Vector4)pixel });

                        entities.Add(entity);
                    }
                }

                // entities 的渲染需要一系列网格体和材质，每个 entity 标识
                // 按索引使用的网格和材质。在这种情况下，我们创建的所有 entities 将使用
                // 相同的网格和相同的材料。这些仍然必须作为两个数组提供。
                AddComponentObject(mainEntity, new MeshArrayBakingType
                {
                    meshArray = new RenderMeshArray(new[] { authoring.Info.Material }, new[] { authoring.Info.Mesh })
                });

                // 当前 entity 的唯一目的是将信息转发到 baking system。这是
                // 运行时无用。通过向其中添加 BakingOnlyEntity，我们要求 baking 进程应该
                // 将 baking 的结果合并到目标 world 时，保留此 entity。
                AddComponent<BakingOnlyEntity>(mainEntity);
            }
        }
    }

    // 需要 RenderMeshArray 来初始化 entities 进行渲染。但这是在 baking system 中完成的
    // 而数据（网格和材料）来自 baker。为了在两者之间进行通信，需要使用 baking
    // 使用类型。Baking 类型不会转移到目标 world 并保留在 baking world 中。
    [BakingType]
    public class MeshArrayBakingType : IComponentData
    {
        public RenderMeshArray meshArray;
    }

    // 由 baker 创建的 entities 集（图像中每个像素一个）需要发送到 baking system
    // 为了正确设置为可渲染的 entities。为此，使用了 baking 型动态缓冲器。
    [BakingType]
    public struct ImageGeneratorEntity : IBufferElementData
    {
        Entity m_Value;

        // 嵌入在缓冲区元素数据中的值的隐式转换运算符使其使用起来很方便。
        public static implicit operator Entity(ImageGeneratorEntity e) => e.m_Value;
        public static implicit operator ImageGeneratorEntity(Entity e) => new() { m_Value = e };
    }
#endif
}
