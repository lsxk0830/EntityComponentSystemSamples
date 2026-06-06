using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Tutorials.Jobs.Step4
{
    [BurstCompile]
    public struct FindNearestJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> TargetPositions;
        [ReadOnly] public NativeArray<float3> SeekerPositions;

        public NativeArray<float3> NearestTargetPositions;

        public void Execute(int index)
        {
            float3 seekerPos = SeekerPositions[index];

            // 找到 X 坐标最接近的目标。
            int startIdx = TargetPositions.BinarySearch(seekerPos, new AxisXComparer { });

            // 当没有找到精确匹配时，BinarySearch 返回最后搜索的偏移量的按位否定。
            // 因此，当 startIdx 为负数时，我们再次翻转这些位，但必须确保索引在范围内。
            if (startIdx < 0) startIdx = ~startIdx;
            if (startIdx >= TargetPositions.Length) startIdx = TargetPositions.Length - 1;

            // X 坐标最接近的目标位置。
            float3 nearestTargetPos = TargetPositions[startIdx];
            float nearestDistSq = math.distancesq(seekerPos, nearestTargetPos);

            // 在阵列中向上搜索更近的目标。
            Search(seekerPos, startIdx + 1, TargetPositions.Length, +1, ref nearestTargetPos, ref nearestDistSq);

            // 在阵列中向下搜索更近的目标。
            Search(seekerPos, startIdx - 1, -1, -1, ref nearestTargetPos, ref nearestDistSq);

            NearestTargetPositions[index] = nearestTargetPos;
        }

        void Search(float3 seekerPos, int startIdx, int endIdx, int step,
                    ref float3 nearestTargetPos, ref float nearestDistSq)
        {
            for (int i = startIdx; i != endIdx; i += step)
            {
                float3 targetPos = TargetPositions[i];
                float xdiff = seekerPos.x - targetPos.x;

                // 如果 x 距离的平方大于当前最近的，我们可以停止搜索。
                if ((xdiff * xdiff) > nearestDistSq) break;

                float distSq = math.distancesq(targetPos, seekerPos);

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestTargetPos = targetPos;
                }
            }
        }
    }

    public struct AxisXComparer : IComparer<float3>
    {
        public int Compare(float3 a, float3 b)
        {
            return a.x.CompareTo(b.x);
        }
    }
}
