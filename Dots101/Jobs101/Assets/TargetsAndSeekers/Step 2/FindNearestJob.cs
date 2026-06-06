using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

// 我们将使用 Unity.Mathematics.float3 而不是 Vector3，
// 我们将使用 Unity.Mathematics.math.distancesq 而不是 Vector3.sqrMagnitude。
using Unity.Mathematics;

namespace Tutorials.Jobs.Step2
{
    // 将 BurstCompile 属性包含到 Burst 编译 job
    [BurstCompile]
    public struct FindNearestJob : IJob
    {
        // job 将访问的所有数据都应该纳入其领域。在这种情况下，job 需要三个 float3 数组。

        // 只读的数组和集合字段 job 应标有 ReadOnly 属性。尽管在这种情况下并非绝对必要，但标记数据
        // 因为 ReadOnly 可能允许 job 调度程序安全地运行多个 jobs 互相并发。
        // （有关更多详细信息，请参阅“jobs 简介”。）

        [ReadOnly] public NativeArray<float3> TargetPositions;
        [ReadOnly] public NativeArray<float3> SeekerPositions;

        // 对于 SeekerPositions[i]，我们将分配最近的目标位置为 NearestTargetPositions[i]。
        public NativeArray<float3> NearestTargetPositions;

        // Execute是 IJob 接口的唯一方法。 当工作线程执行 job 时，它会调用此方法。
        public void Execute()
        {
            // 计算每个Seeker到每个目标的平方距离。
            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                float3 seekerPos = SeekerPositions[i];
                float nearestDistSq = float.MaxValue;
                for (int j = 0; j < TargetPositions.Length; j++)
                {
                    float3 targetPos = TargetPositions[j];
                    float distSq = math.distancesq(seekerPos, targetPos);
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        NearestTargetPositions[i] = targetPos;
                    }
                }
            }
        }
    }
}