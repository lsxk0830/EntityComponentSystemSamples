using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Baking.BakingTypes
{
    public class BoundingBoxAuthoring : MonoBehaviour
    {
        class Baker : Baker<BoundingBoxAuthoring>
        {
            public override void Bake(BoundingBoxAuthoring authoring)
            {
                // 获取对网格和变换的依赖关系
                // 这确保如果其中任何一个发生更改，Baker 都会重新运行
                var mesh = GetComponent<MeshFilter>().sharedMesh;
                var pos = GetComponent<Transform>().position;
                DependsOn(mesh);

                var parentBox = GetComponentInParent<CompoundBBAuthoring>();
                var parentEntity = GetEntity(parentBox, TransformUsageFlags.Dynamic);

                var hasMesh = mesh != null;
                float xp = float.MinValue, yp = float.MinValue, zp = float.MinValue;
                float xn = float.MaxValue, yn = float.MaxValue, zn = float.MaxValue;

                // 计算边界框
                if (hasMesh)
                {
                    var vertices = new List<Vector3>(4096);
                    mesh.GetVertices(vertices);
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        var p = vertices[i];
                        xp = math.max(p.x, xp);
                        yp = math.max(p.y, yp);
                        zp = math.max(p.z, zp);
                        xn = math.min(p.x, xn);
                        yn = math.min(p.y, yn);
                        zn = math.min(p.z, zn);
                    }
                }
                else
                {
                    xp = yp = zp = xn = yn = zn = 0;
                }

                var minBoundingBox = new float3(xn, yn, zn) + new float3(pos.x, pos.y, pos.z);
                var maxBoundingBox = new float3(xp, yp, zp) + new float3(pos.x, pos.y, pos.z);

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new BoundingBox()
                {
                    Parent = parentEntity,
                    MinBBVertex = minBoundingBox,
                    MaxBBVertex = maxBoundingBox
                });
                AddComponent<Changes>(entity);
            }
        }
    }

    // BakingType components 存在于 Baking 进程中，但不存在于目标 world 中。
    // 它可用于将数据从 Baker 获取到 Baking System。
    [BakingType]
    public struct BoundingBox : IComponentData
    {
        public Entity Parent;
        public float3 MinBBVertex;
        public float3 MaxBBVertex;
    }

    // TemporaryBakingType components 在 Baking systems run 之后删除。
    [TemporaryBakingType]
    public struct Changes : IComponentData
    {
    }

    // 此 component 被添加到带有 BoundingBoxComponent 的每个 entity 中。它跟踪 entity 的前一个父项。
    // 当 entity 被重新设置父级或被销毁时，需要重新计算其先前父级的边界框。
    [BakingType]
    public struct BoundingBoxCleanup : ICleanupComponentData
    {
        public Entity PreviousParent;
    }
}
