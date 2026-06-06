using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HelloCube.CrossQuery
{
    public partial struct CollisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExecuteCrossQuery>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var boxQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, DefaultColor, URPMaterialPropertyBaseColor>().Build();

#if false
            // 更复杂的解决方案，但它避免创建盒子 components 的临时副本
            new CollisionJob
            {
                LocalTransformTypeHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(true),
                DefaultColorTypeHandle = SystemAPI.GetComponentTypeHandle<DefaultColor>(true),
                BaseColorTypeHandle = SystemAPI.GetComponentTypeHandle<URPMaterialPropertyBaseColor>(),
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                OtherChunks = boxQuery.ToArchetypeChunkArray(state.WorldUpdateAllocator)
            }.ScheduleParallel(boxQuery, state.Dependency).Complete();
#else

            // 简单的解决方案，但它需要创建所有框翻译和 entity IDs 的临时副本
            var boxTransforms = boxQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var boxEntities = boxQuery.ToEntityArray(Allocator.Temp);

            foreach (var (transform, defaultColor, color, entity)
                     in SystemAPI.Query<RefRO<LocalTransform>, RefRO<DefaultColor>, RefRW<URPMaterialPropertyBaseColor>>()
                         .WithEntityAccess())
            {
                // 将框的颜色重置为其默认值
                color.ValueRW.Value = defaultColor.ValueRO.Value;

                // 如果此框与另一个框相交，则更改颜色
                for (int i = 0; i < boxTransforms.Length; i++)
                {
                    var otherEnt = boxEntities[i];
                    var otherTrans = boxTransforms[i];

                    // 盒子不应该与自身相交，因此我们检查另一个 entity 的 id 是否与当前 entity 的 id 匹配。
                    if (entity != otherEnt && math.distancesq(transform.ValueRO.Position, otherTrans.Position) < 1)
                    {
                        color.ValueRW.Value.y = 0.5f; // 设置绿色通道
                        break;
                    }
                }
            }
#endif
        }
    }

    [BurstCompile]
    public struct CollisionJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformTypeHandle;
        [ReadOnly] public ComponentTypeHandle<DefaultColor> DefaultColorTypeHandle;
        public ComponentTypeHandle<URPMaterialPropertyBaseColor> BaseColorTypeHandle;
        [ReadOnly] public EntityTypeHandle EntityTypeHandle;

        [ReadOnly] public NativeArray<ArchetypeChunk> OtherChunks;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref LocalTransformTypeHandle);
            var defaultColors = chunk.GetNativeArray(ref DefaultColorTypeHandle);
            var baseColors = chunk.GetNativeArray(ref BaseColorTypeHandle);
            var entities = chunk.GetNativeArray(EntityTypeHandle);

            for (int i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                var baseColor = baseColors[i];
                var entity = entities[i];

                // 重置为默认颜色
                baseColor.Value = defaultColors[i].Value;

                for (int j = 0; j < OtherChunks.Length; j++)
                {
                    var otherChunk = OtherChunks[j];
                    var otherTranslations = otherChunk.GetNativeArray(ref LocalTransformTypeHandle);
                    var otherEntities = otherChunk.GetNativeArray(EntityTypeHandle);

                    for (int k = 0; k < otherChunk.Count; k++)
                    {
                        var otherTranslation = otherTranslations[k];
                        var otherEntity = otherEntities[k];

                        if (entity != otherEntity && math.distancesq(transform.Position, otherTranslation.Position) < 1)
                        {
                            baseColor.Value.y = 0.5f; // 设置绿色通道
                            break;
                        }
                    }
                }

                baseColors[i] = baseColor;
            }
        }
    }
}
