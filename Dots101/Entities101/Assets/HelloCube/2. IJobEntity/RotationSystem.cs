using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HelloCube.JobEntity
{
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExecuteIJobEntity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new RotateAndScaleJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime, ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            };
            job.Schedule();
        }
    }

    [BurstCompile]
    partial struct RotateAndScaleJob : IJobEntity
    {
        public float DeltaTime;
        public float ElapsedTime;

        // 在源生成中，根据 Execute() 的参数创建 query。
        // 此处，query 将匹配所有具有 LocalTransform、PostTransformMatrix 和 RotationSpeed component 的 entities。
        // （在 scene 中，根立方体具有不均匀的比例，因此在 baking 中赋予它 PostTransformMatrix component。）
        void Execute(ref LocalTransform transform, ref PostTransformMatrix postTransform, in RotationSpeed speed)
        {
            transform = transform.RotateY(speed.RadiansPerSecond * DeltaTime);
            postTransform.Value = float4x4.Scale(1, math.sin(ElapsedTime), 1);
        }
    }
}
