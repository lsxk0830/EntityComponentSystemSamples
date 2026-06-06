using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
partial struct ColliderBakeTransformSystem : ISystem
{
    private NativeQueue<BlobAssetReference<Unity.Physics.Collider>> m_ColliderBlobsToDisposeNow;

    [BurstCompile]
    public partial struct BakeTransformJob : IJobEntity
    {
        public float TimeStep;
        public NativeQueue<BlobAssetReference<Unity.Physics.Collider>>.ParallelWriter ColliderBlobsToDisposeNow;

        void Execute(ref ColliderBakeTransform transformData, ref SaveColliderBlobForDisposal saveCollider, ref PhysicsCollider collider, ref PhysicsMass mass,
            ref PostTransformMatrix postTransformMatrix, Entity entity, [ChunkIndexInQuery] int chunkIndex)
        {
            // 在 job 运行之前，Collider 应该是唯一的
            if (!collider.IsUnique)
                return;

            // 如果先前已将变换应用于 collider 并且不请求动画，则跳过
            if (transformData.FrameCount > 0 && transformData.AnimationDuration <= 0)
                return;

            // 如果同时启用了漂移预防和 collider baking 动画，
            // 如果达到漂移阈值，则存储一些数据以供以后 collider 复位使用。
            if (transformData.DriftPrevention && transformData.AnimationDuration > 0)
            {
                // 如果重置动画时几何体发生漂移，则存储原始几何体数据以供以后重置，以及
                // 立即用有保证的唯一克隆替换烘焙的 collider。
                if (transformData.FrameCount == 0 && !transformData.OriginalCollider.IsCreated)
                {
                    transformData.OriginalCollider = collider.Value;
                    collider.Value = transformData.OriginalCollider.Value.Clone();
                    saveCollider.Collider = collider.Value;

                    transformData.OriginalPostTransformMatrix = postTransformMatrix;
                }
            }

            var animationFactor = 1f;
            if (transformData.AnimationDuration > 0)
            {
                var animationFrames = math.ceil(transformData.AnimationDuration / TimeStep);
                if (transformData.FrameCount >= animationFrames)
                {
                    transformData.FrameCount = 0;
                    if (transformData.DriftPrevention)
                    {
                        var lengthSq = math.lengthsq(postTransformMatrix.Value.c0)
                            + math.lengthsq(postTransformMatrix.Value.c1)
                            + math.lengthsq(postTransformMatrix.Value.c2)
                            + math.lengthsq(postTransformMatrix.Value.c3);

                        var lengthSqOrig = math.lengthsq(transformData.OriginalPostTransformMatrix.Value.c0)
                            + math.lengthsq(transformData.OriginalPostTransformMatrix.Value.c1)
                            + math.lengthsq(transformData.OriginalPostTransformMatrix.Value.c2)
                            + math.lengthsq(transformData.OriginalPostTransformMatrix.Value.c3);

                        if (math.abs(lengthSq - lengthSqOrig) > transformData.DriftErrorThreshold)
                        {
                            var driftedCollider = collider.Value;

                            //克隆 blob 并将其存储在 save 中以供处置，以便在帧末尾进行处置
                            collider.Value = transformData.OriginalCollider.Value.Clone();
                            saveCollider.Collider = collider.Value;

                            // 我们还无法在 Collider.value 中处理该 blob，因为它可能需要
                            // DebugDraw system。相反，将其添加到队列中以处理下一帧。
                            ColliderBlobsToDisposeNow.Enqueue(driftedCollider);

                            postTransformMatrix = transformData.OriginalPostTransformMatrix;
                        }
                    }
                }

                // 考虑动画持续时间内动画函数权重的总和，标准化动画因子。
                // 在这里，我们使用正弦离散和的恒等式。
                var N = math.ceil(animationFrames / 2);
                var d = math.PI / N;
                var s = math.sin(0.5f * d);
                var o_t = 1f;
                if (math.abs(s) > math.EPSILON)
                {
                    var R = math.sin(N * 0.5f * d) / s;
                    o_t = R * math.sin((N - 1) * 0.5f * d);
                }

                float o = math.sin(2f * math.PI * (transformData.FrameCount / animationFrames));
                animationFactor = o / math.abs(o_t);
            }

            ++transformData.FrameCount;

            if (math.abs(animationFactor) < math.EPSILON)
            {
                return;
            }

            var deltaScale = transformData.Scale - 1f;

            // 根据 baking 数据中提供的平移、旋转、缩放和剪切计算仿射变换。
            var bakeTransform = new AffineTransform(
                animationFactor * transformData.Translation,
                math.slerp(math.conjugate(transformData.Rotation), transformData.Rotation,
                    (animationFactor + 1f) / 2f),
                1 + animationFactor * deltaScale);

            float3x3 shearXZ, shearYZ;
            var shearXY = shearXZ = shearYZ = float3x3.identity;

            shearXY[2][0] = animationFactor * transformData.ShearXY.x;
            shearXY[2][1] = animationFactor * transformData.ShearXY.y;
            shearXZ[1][0] = animationFactor * transformData.ShearXZ.x;
            shearXZ[1][2] = animationFactor * transformData.ShearXZ.y;
            shearYZ[0][1] = animationFactor * transformData.ShearYZ.x;
            shearYZ[0][2] = animationFactor * transformData.ShearYZ.y;

            bakeTransform = math.mul(bakeTransform, math.mul(shearXY, math.mul(shearXZ, shearYZ)));

            // 将仿射变换应用于 collider 几何体。
            collider.Value.Value.BakeTransform(bakeTransform);

            // 通过复制来更新刚体的质量属性（如果可用且动态）
            // 新的、修改后的 collider 的质量属性到 PhysicsMass component 中。
            if (!mass.IsKinematic)
            {
                var massProperties = collider.MassProperties;
                mass.Transform = massProperties.MassDistribution.Transform;
                mass.InverseInertia = math.rcp(massProperties.MassDistribution.InertiaTensor);
                mass.AngularExpansionFactor = massProperties.AngularExpansionFactor;
            }

            // 还将烘焙变换应用到 PostTransformMatrix 以影响视觉效果。
            postTransformMatrix = new PostTransformMatrix
            {
                Value = math.mul((float4x4)bakeTransform, postTransformMatrix.Value)
            };
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ColliderBakeTransform>();
        m_ColliderBlobsToDisposeNow = new NativeQueue<BlobAssetReference<Unity.Physics.Collider>>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        var dt = SystemAPI.Time.DeltaTime;

        // 确保我们想要应用转换的所有 colliders 都具有 PostTransformMatrix component，以便
        // 我们还可以影响他们的视觉效果。
        foreach (var(scaleAndShearData, collider, entity) in SystemAPI
                 .Query<ColliderBakeTransform, RefRW<PhysicsCollider>>()
                 .WithNone<PostTransformMatrix>()
                 .WithEntityAccess())
        {
            ecb.AddComponent(entity, new PostTransformMatrix { Value = float4x4.identity });
            ecb.AddComponent(entity, new SaveColliderBlobForDisposal
            {
                Collider = BlobAssetReference<Unity.Physics.Collider>.Null
            });

            if (!collider.ValueRO.IsUnique)
            {
                collider.ValueRW.MakeUnique(entity, ecb);
            }
        }

        ecb.Playback(state.EntityManager);

        var disposeJobHandle = new DisposeJob()
        {
            DisposeNow = m_ColliderBlobsToDisposeNow
        }.Schedule(state.Dependency);

        // 使用独特的 colliders 在 NON-STATIC 主体上执行 collider 变换 baking
        state.Dependency = new BakeTransformJob()
        {
            TimeStep = dt,
            ColliderBlobsToDisposeNow = m_ColliderBlobsToDisposeNow.AsParallelWriter()
        }.ScheduleParallel(disposeJobHandle);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        using var ecb = new EntityCommandBuffer(Allocator.Temp);
        // 清理尚未通过 m_ColliderBlobsToDisposeNow 队列处理的任何已保存的 collider blob
        foreach (var(saveCollider, entity) in
                 SystemAPI.Query<RefRW<SaveColliderBlobForDisposal>>().WithEntityAccess())
        {
            //将最新的 collider 克隆放入我们的烘焙数据 component
            if (saveCollider.ValueRO.Collider.IsCreated)
            {
                saveCollider.ValueRW.Collider.Dispose();
                ecb.RemoveComponent<SaveColliderBlobForDisposal>(entity);
            }
        }
        ecb.Playback(state.EntityManager);

        // 处理来自 transformData.OriginalCollider 克隆的斑点，这些斑点用于动画漂移重置
        while (!m_ColliderBlobsToDisposeNow.IsEmpty())
        {
            m_ColliderBlobsToDisposeNow.Dequeue().Dispose();
        }

        m_ColliderBlobsToDisposeNow.Dispose();
    }

    private struct DisposeJob : IJob
    {
        public NativeQueue<BlobAssetReference<Unity.Physics.Collider>> DisposeNow;
        public void Execute()
        {
            while (!DisposeNow.IsEmpty())
            {
                DisposeNow.Dequeue().Dispose();
            }
        }
    }
}
