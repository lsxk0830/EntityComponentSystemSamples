using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public class AddComponentsExample : MonoBehaviour
{
    public Mesh Mesh;
    public Material m_material;
    public bool m_differentMaterial = false;

    public int m_w = 30;
    public int m_h = 30;


    // 创建许多 entities 的示例 Burst job
    [GenerateTestsForBurstCompatibility]
    public struct SpawnJob : IJobParallelFor
    {
        public Entity Prototype;
        public int w;
        public int h;
        public bool singleMat;
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute(int index)
        {
            // 克隆原型 entity 以创建新的 entity。
            var e = Ecb.Instantiate(index, Prototype);
            // 原型前面有所有正确的 components，可以使用 SetComponent 来
            // 设置新创建的 entity 特有的值，例如变换。
            int matIndex = singleMat ? 0 : index;
            Ecb.SetComponent(index, e, MaterialMeshInfo.FromRenderMeshArrayIndices(matIndex, 0));
            Ecb.SetComponent(index, e, new LocalToWorld {Value = ComputeTransform(index)});
        }

        public float4x4 ComputeTransform(int index)
        {
            int y = index / w;
            int x = index % h;
            float3 pos = new float3(x - (float)w * 0.5f, 0, y - (float)h * 0.5f);

            return float4x4.Translate(pos);
        }
    }

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

        int objCount = m_w * m_h;
        var matList = new List<Material>();
        if ( m_differentMaterial )
        {
            for (int i=0;i<objCount;i++)
            {
                var mat = new Material(m_material);
                Color col = Color.HSVToRGB(((float)(i * 10) / (float)objCount) % 1.0f, 0.7f, 1.0f);
                //                Color col = Color.HSVToRGB(Random.Range(0.0f,1.0f), 1.0f, 1.0f);
                mat.SetColor("_Color", col);              // 设置为 LW
                mat.SetColor("_BaseColor", col);          // 设置为 HD
                matList.Add(mat);
            }
        }
        else
        {
            matList.Add(m_material);
        }

        // 使用便捷构造函数创建 RenderMeshDescription
        // 带有命名参数。
        var desc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off,
            receiveShadows: false);

        var renderMeshArray = new RenderMeshArray(matList.ToArray(), new[] { Mesh });

        // 创建空底座 entity
        var prototype = entityManager.CreateEntity();

        // 调用 AddComponents 以使用所需的 components 填充基础 entity
        // 通过 Entities Graphics
        RenderMeshUtility.AddComponents(
            prototype,
            entityManager,
            desc,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(prototype, new LocalToWorld());

        // 通过克隆预先创建的原型 entity，在 Burst job 中生成大部分 entities，
        // 它可以是在 run 时间创建的 Prefab 或 entity，如本示例中所示。
        // 这是在 run 时间创建 entities 的最快且最有效的方法。
        var spawnJob = new SpawnJob
        {
            Prototype = prototype,
            Ecb = ecb.AsParallelWriter(),
            w = m_w,
            h = m_h,
            singleMat = !m_differentMaterial
        };

        var spawnHandle = spawnJob.Schedule(m_h*m_w,128);
        spawnHandle.Complete();

        ecb.Playback(entityManager);
        ecb.Dispose();
        entityManager.DestroyEntity(prototype);
    }
}
