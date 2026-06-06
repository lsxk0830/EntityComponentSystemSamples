using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Unity.Physics
{
    // 获取 GameObject 中指定的材质和网格并将该数据写入 RenderMeshArray component
    public class CreateMeshFromResourcesAuthoring : MonoBehaviour
    {
        public UnityEngine.Material MaterialA;
        public UnityEngine.Material MaterialB;
        public UnityEngine.Material MaterialC;
        public UnityEngine.Mesh MeshA;
        public UnityEngine.Mesh MeshB;

        class CreateMeshFromResourcesBaker : Baker<CreateMeshFromResourcesAuthoring>
        {
            public override void Bake(CreateMeshFromResourcesAuthoring authoring)
            {
                var materialA = authoring.MaterialA;
                var materialB = authoring.MaterialB;
                var materialC = authoring.MaterialC;
                var meshA = authoring.MeshA;
                var meshB = authoring.MeshB;

                var createComponent = new RenderMeshArray(
                    new[] { materialA, materialB, materialC },
                    new[] { meshA, meshB, meshB });

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddSharedComponentManaged(entity, createComponent);
                AddComponent(entity, new ResourcesLoadedTag());
            }
        }
    }

    // 用作标签可以更轻松地识别具有加载资源的 RenderMeshArray 数据的 entity
    public struct ResourcesLoadedTag : IComponentData {}
}
