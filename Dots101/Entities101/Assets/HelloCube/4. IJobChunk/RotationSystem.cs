using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HelloCube.JobChunk
{
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExecuteIJobChunk>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spinningCubesQuery = SystemAPI.QueryBuilder().WithAll<RotationSpeed, LocalTransform>().Build();

            var job = new RotationJob
            {
                TransformTypeHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(),
                RotationSpeedTypeHandle = SystemAPI.GetComponentTypeHandle<RotationSpeed>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            // 与 IJobEntity 不同，IJobChunk 必须手动传递 query。
            // 此外，IJobChunk 不会隐式传递和分配 state.Dependency JobHandle。
            // （这种传递和分配 state.Dependency 的模式可确保 entity jobs 调度
            // 不同的 systems 将根据需要相互依赖。）
            state.Dependency = job.Schedule(spinningCubesQuery, state.Dependency);
        }
    }

    [BurstCompile]
    struct RotationJob : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> TransformTypeHandle;
        [ReadOnly] public ComponentTypeHandle<RotationSpeed> RotationSpeedTypeHandle;
        public float DeltaTime;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            // 当 entities 中的一个或多个时，useEnableMask 参数为 true
            // chunk 的 query 的 components 被禁用。
            // 如果没有一个 query component 类型实现 IEnableableComponent，
            // 我们可以假设 useEnabledMask 始终为假。
            // 但是，最好添加此防护检查以防万一
            // 后来有人更改了 query 或 component 类型。
            Assert.IsFalse(useEnabledMask);

            var transforms = chunk.GetNativeArray(ref TransformTypeHandle);
            var rotationSpeeds = chunk.GetNativeArray(ref RotationSpeedTypeHandle);
            for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
            {
                transforms[i] = transforms[i].RotateY(rotationSpeeds[i].RadiansPerSecond * DeltaTime);
            }
        }
    }
}
