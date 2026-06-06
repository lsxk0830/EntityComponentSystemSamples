using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Tutorials.Firefighters
{
    public partial struct BucketSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ExecuteBucket>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Config>();

            foreach (var (bucket, trans, color) in
                     SystemAPI.Query<RefRW<Bucket>, RefRW<LocalTransform>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                // todo 我们只需要在水值变化时更新颜色
                color.ValueRW.Value = math.lerp(config.BucketEmptyColor, config.BucketFullColor, bucket.ValueRO.Water);
                trans.ValueRW.Scale = math.lerp(config.BucketEmptyScale, config.BucketFullScale, bucket.ValueRO.Water);

                if (bucket.ValueRO.IsCarried)
                {
                    var botTrans = SystemAPI.GetComponent<LocalTransform>(bucket.ValueRO.CarryingBot);
                    trans.ValueRW.Position = botTrans.Position + new float3(0, 1, 0); // 放置在机器人头部上方
                }
            }
        }
    }
}
