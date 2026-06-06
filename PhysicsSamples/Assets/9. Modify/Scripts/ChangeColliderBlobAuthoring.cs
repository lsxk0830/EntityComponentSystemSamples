// 该脚本用于 `5g1. Change Collider Material - Bouncy Boxes` 演示中，它基于
// 关闭 ChangeBoxColliderSizeAuthoring.cs 脚本，但它还扩展了此行为
// 根据盒子是否增大或缩小来更改物理材料属性。
// The material (colour) is also changed to reflect modifications to the blob data.
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using BoxCollider = Unity.Physics.BoxCollider;

public struct ChangeColliderBlob : IComponentData
{
    public float3 Min;
    public float3 Max;
    public float3 Target;
}

public class ChangeColliderBlobAuthoring : MonoBehaviour
{
    public float3 Min = 0;
    public float3 Max = 10;
}

class ChangeColliderBaker : Baker<ChangeColliderBlobAuthoring>
{
    public override void Bake(ChangeColliderBlobAuthoring blobAuthoring)
    {
        var entity = GetEntity(TransformUsageFlags.ManualOverride);
        AddComponent(entity, new ChangeColliderBlob
        {
            Min = blobAuthoring.Min,
            Max = blobAuthoring.Max,
            Target = math.lerp(blobAuthoring.Min, blobAuthoring.Max, 0.5f),
        });

        AddComponent(entity, new PostTransformMatrix
        {
            Value = float4x4.identity,
        });
    }
}

/// <summary>
/// 这个 system 需要在 BeforePhysicsSystemGroup 中的 run，在 EnsureUniqueColliderSystem 之后。这
/// EnsureUniqueColliderSystem 负责更新任何prefab上唯一的 collider 标志。通过运行
/// 首先是 EnsureUniqueColliderSystem，它确保在修改此处的 blob 时唯一的 colliders 可用
/// </summary>
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct ChangeColliderBlobSystem : ISystem
{
    const float k_GrowingRestitution = 0.75f;
    /// <summary>
    /// 这个 job 改变了盒子 collider 的大小（类似于 ChangeBoxColliderSizeJob）但是扩展
    /// 还可以通过改变物理材料恢复来实现。
    /// 如果盒子缩小，则恢复 = 0
    /// 如果盒子正在增长，则恢复 = 0.75
    /// </summary>
    [BurstCompile]
    public partial struct ChangeColliderBlobJob : IJobEntity
    {
        public void Execute(ref PhysicsCollider collider, ref ChangeColliderBlob size,
            ref PostTransformMatrix postTransformMatrix)
        {
            // 确保我们正在处理盒子
            if (collider.Value.Value.Type != ColliderType.Box) return;

            float3 oldSize = 1.0f;
            float3 newSize = 1.0f;
            const float k_ShrinkingRestitution = 0.0f;

            unsafe
            {
                // 更新盒子的大小
                // 抓住盒子指针
                BoxCollider* bxPtr = (BoxCollider*)collider.ColliderPtr;
                oldSize = bxPtr->Size;
                newSize = math.lerp(oldSize, size.Target, 0.05f);

                // 如果我们达到了目标大小，则获取新目标
                float3 newTargetSize = math.select(size.Min, size.Max, size.Target == size.Min);
                size.Target = math.select(size.Target, newTargetSize,
                    math.abs(newSize - size.Target) < new float3(0.1f));

                var boxGeometry = bxPtr->Geometry;
                boxGeometry.Size = newSize;
                bxPtr->Geometry = boxGeometry;

                // 修改物理材质恢复
                var oldRestitution = collider.Value.Value.GetRestitution();
                var newRestitution = oldRestitution;

                var sizeChange = CheckIfGrowing(oldSize, newSize);
                if (sizeChange > 0) //生长
                {
                    newRestitution = k_GrowingRestitution;
                }
                else if (sizeChange < 0) //缩小
                {
                    newRestitution = k_ShrinkingRestitution;
                }
                //else leave it alone

                if (!newRestitution.Equals(oldRestitution))
                {
                    collider.Value.Value.SetRestitution(newRestitution);
                }
            }

            // 现在调整盒子的图形表示
            float3 newScale = newSize / oldSize;
            postTransformMatrix.Value.c0 *= newScale.x;
            postTransformMatrix.Value.c1 *= newScale.y;
            postTransformMatrix.Value.c2 *= newScale.z;

            if (!collider.IsUnique)
            {
                Debug.LogWarning($"Error: The collider {collider.Value.Value.Type} is not unique. Check your system order.");
            }
        }

        private int CheckIfGrowing(float3 oldSize, float3 newSize)
        {
            const float threshold = 0.0001f;
            var compare = newSize - oldSize;

            var sum = 0;
            sum += SingleCompare(compare.x, threshold);
            sum += SingleCompare(compare.y, threshold);
            sum += SingleCompare(compare.z, threshold);

            return sum;
        }

        private int SingleCompare(float compare, float threshold)
        {
            var sum = 0;
            if (compare < threshold)
            {
                sum += -1;
            }
            else if (compare > threshold)
            {
                sum += 1;
            }
            //else no change

            return sum;
        }
    }

    private EntityQuery m_MaterialQuery;

    public void OnCreate(ref SystemState state)
    {
        m_MaterialQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(ColliderMaterialsComponent)
            }
        });
    }

    public void OnUpdate(ref SystemState state)
    {
        var blobJob = new ChangeColliderBlobJob().Schedule(state.Dependency);
        blobJob.Complete();

        // 根据 colliders 的恢复情况更改其颜色（由 blob job 更改）
        var entityArray = m_MaterialQuery.ToEntityArray(Allocator.Temp);
        if (entityArray.Length == 0) return;
        var materials = state.EntityManager.GetSharedComponentManaged<ColliderMaterialsComponent>(entityArray[0]);

        var renderMeshArraysToAdd = new List<RenderMeshArray>();
        var entitiesToAdd = new NativeList<Entity>(Allocator.Temp);
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach (var(renderMeshArray, collider, blob, entity) in SystemAPI
                 .Query<RenderMeshArray, RefRO<PhysicsCollider>, ChangeColliderBlob>()
                 .WithEntityAccess()
                 .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            var restitution = collider.ValueRO.Value.Value.GetRestitution();
            var useMaterial = (restitution < k_GrowingRestitution) ? materials.ShrinkMaterial : materials.GrowMaterial;
            var materialArray = new[] { (UnityObjectRef<UnityEngine.Material>)useMaterial };
            var newRenderMeshArray = new RenderMeshArray(materialArray, renderMeshArray.MeshReferences);

            renderMeshArraysToAdd.Add(newRenderMeshArray);
            entitiesToAdd.Add(entity);
        }
        commandBuffer.Playback(state.EntityManager);

        for (int i = 0; i < entitiesToAdd.Length; i++)
        {
            var e = entitiesToAdd[i];
            var renderMeshArray = renderMeshArraysToAdd[i];

            RenderMeshUtility.AddComponents(
                e,
                state.EntityManager,
                new RenderMeshDescription(ShadowCastingMode.Off),
                renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
        }

        entitiesToAdd.Dispose();
        commandBuffer.Dispose();
        entityArray.Dispose();
    }

    public void OnDestroy(ref SystemState state) {}
}
