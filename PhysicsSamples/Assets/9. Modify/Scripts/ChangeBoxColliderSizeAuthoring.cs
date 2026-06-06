using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;

public struct ChangeBoxColliderSize : IComponentData
{
    public float3 Min;
    public float3 Max;
    public float3 Target;
}

// 一般来说，您应该在 run 时间将 colliders 视为不可变数据，因为多个主体可能共享相同的 collider。
// 如果您计划在 run 时间修改网格或凸面 colliders，请记住勾选 PhysicsShapeAuthoring component 上的强制唯一框。
// 这保证了 PhysicsCollider component 在所有情况下都将具有唯一的实例。

public class ChangeBoxColliderSizeAuthoring : MonoBehaviour
{
    public float3 Min = 0;
    public float3 Max = 10;
}

class ChangeBoxColliderSizeBaker : Baker<ChangeBoxColliderSizeAuthoring>
{
    public override void Bake(ChangeBoxColliderSizeAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.ManualOverride);
        AddComponent(entity, new ChangeBoxColliderSize
        {
            Min = authoring.Min,
            Max = authoring.Max,
            Target = math.lerp(authoring.Min, authoring.Max, 0.5f),
        });

        // 添加 PostTransformMatrix component，以防主体 baker 尚未完成此操作，
        // 如果 collider world 变换在编辑时尚未具有剪切或非均匀缩放，则会发生这种情况。
        // 如果编辑时确实存在剪切或不均匀比例，则必须添加 PostTransformMatrix component
        // 由主体 baker 组成。
        float4x4 localToWorld = authoring.gameObject.transform.localToWorldMatrix;
        if (!(localToWorld.HasShear() || localToWorld.HasNonUniformScale()))
        {
            AddComponent(entity, new PostTransformMatrix
            {
                Value = float4x4.identity,
            });
        }
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct ChangeBoxColliderSizeSystem : ISystem
{
    [BurstCompile]
    public partial struct ChangeBoxColliderSizeJob : IJobEntity
    {
        public void Execute(ref PhysicsCollider collider, ref ChangeBoxColliderSize size, ref PostTransformMatrix postTransformMatrix)
        {
            // 确保我们正在处理盒子
            if (collider.Value.Value.Type != ColliderType.Box) return;

            // 调整盒子的物理表示

            // NOTE: 此方法会影响使用相同 BlobAsset 的所有实例
            // 所以你不能简单地使用这种方法来实例化prefab
            // 如果要独立修改 prefab 实例，则需要创建
            // 独特的 BlobAssets 在 run 时间并在完成后将其丢弃

            float3 oldSize = 1.0f;
            float3 newSize = 1.0f;
            unsafe
            {
                // 抓住盒子指针
                BoxCollider* bxPtr = (BoxCollider*)collider.ColliderPtr;
                oldSize = bxPtr->Size;
                newSize = math.lerp(oldSize, size.Target, 0.05f);

                // 如果我们达到了目标大小，则获取新目标
                float3 newTargetSize = math.select(size.Min, size.Max, size.Target == size.Min);
                size.Target = math.select(size.Target, newTargetSize, math.abs(newSize - size.Target) < new float3(0.1f));

                var boxGeometry = bxPtr->Geometry;
                boxGeometry.Size = newSize;
                bxPtr->Geometry = boxGeometry;
            }

            // 现在调整盒子的图形表示
            float3 newScale = newSize / oldSize;
            postTransformMatrix.Value.c0 *= newScale.x;
            postTransformMatrix.Value.c1 *= newScale.y;
            postTransformMatrix.Value.c2 *= newScale.z;

            if (!collider.IsUnique)
            {
                throw new ArgumentException($"Error: The collider {collider.Value.Value} is not unique");
            }
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ChangeBoxColliderSizeJob().Schedule(state.Dependency);
    }
}
