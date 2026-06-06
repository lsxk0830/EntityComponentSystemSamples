using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;

namespace ContentManagement.Sample
{
#if UNITY_EDITOR
    //  此 authoring component 添加了两个 components 和 WeakObjectReferences：一个 component 用于网格，一个用于材料列表。
    //  如果设置了 UseUntypedId 标志，则会添加两个 components 和 UntypedWeakReferenceIds。
    //
    //  WeakObjectReference 基本上是 UntypedWeakReferenceId 的类型包装，使用起来稍微方便一些。
    //  通常，首选使用 WeakObjectReference，除非您需要的引用的资产类型在编译时未固定，
    //  在这种情况下，您将需要 UntypedWeakReferenceId。
    //  此外，将 LocalContent component 分配给结果 entity，
    //  使 LoadingLocalCatalogSystem 能够直接从磁盘加载文件。
    public class WeakRenderedObjectAuthoring : MonoBehaviour
    {
        public bool UseUntypedId;

        public Mesh Mesh;
        public Material[] Materials;  // 数组，因为单个网格可以有多种材质

        class Baker : Baker<WeakRenderedObjectAuthoring>
        {
            public override void Bake(WeakRenderedObjectAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var mesh = authoring.Mesh;
                var materials = authoring.Materials;

                // 这允许 [LoadingLocalCatalogSystem] 运行并加载内容，
                // 那么 system [WeakObjectLoadingSystem] 可以 run 并连接参考。
                AddComponent<LocalContent>(entity);

                if (authoring.UseUntypedId)
                {
                    AddComponent(entity, new WeakMeshUntyped
                    {
                        Value = UntypedWeakReferenceId.CreateFromObjectInstance(mesh)
                    });

                    var matsBuffer = AddBuffer<WeakMaterialUntyped>(entity);
                    foreach (var mat in materials)
                    {
                        matsBuffer.Add(new WeakMaterialUntyped
                        {
                            Value = UntypedWeakReferenceId.CreateFromObjectInstance(mat)
                        });
                    }
                }
                else
                {
                    AddComponent(entity, new WeakMesh
                    {
                        Value = new WeakObjectReference<Mesh>(mesh),
                    });

                    var matsBuffer = AddBuffer<WeakMaterial>(entity);
                    foreach (var mat in materials)
                    {
                        matsBuffer.Add(new WeakMaterial
                        {
                            Value = new WeakObjectReference<Material>(mat),
                        });
                    }
                }
            }
        }
    }
#endif

    public struct WeakMeshUntyped : IComponentData
    {
        public UntypedWeakReferenceId Value;
    }

    public struct WeakMaterialUntyped : IBufferElementData
    {
        public UntypedWeakReferenceId Value;
    }

    public struct WeakMesh : IComponentData
    {
        public WeakObjectReference<Mesh> Value;
    }

    public struct WeakMaterial : IBufferElementData
    {
        public WeakObjectReference<Material> Value;
    }
}