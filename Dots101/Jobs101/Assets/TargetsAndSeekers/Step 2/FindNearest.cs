using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Tutorials.Jobs.Step2
{
    public class FindNearest : MonoBehaviour
    {
        // 我们的数组的大小不需要改变，将在 Awake() 中创建数组并存储它们
        NativeArray<float3> TargetPositions;
        NativeArray<float3> SeekerPositions;
        NativeArray<float3> NearestTargetPositions;

        public void Start()
        {
            Spawner spawner = Object.FindFirstObjectByType<Spawner>();
            // 我们使用持久分配器，因为这些数组必须
            // 存在于程序的 run 中。
            TargetPositions = new NativeArray<float3>(spawner.NumTargets, Allocator.Persistent);
            SeekerPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
            NearestTargetPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
        }

        // 我们负责处置我们的分配
        // 当我们不再需要它们时。
        public void OnDestroy()
        {
            TargetPositions.Dispose();
            SeekerPositions.Dispose();
            NearestTargetPositions.Dispose();
        }

        public void Update()
        {
            // 将每个目标转换复制到 NativeArray。
            for (int i = 0; i < TargetPositions.Length; i++)
            {
                // Vector3 隐式转换为 float3
                TargetPositions[i] = Spawner.TargetTransforms[i].localPosition;
            }

            // 将每个导引头变换复制到 NativeArray。
            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                // Vector3 隐式转换为 float3
                SeekerPositions[i] = Spawner.SeekerTransforms[i].localPosition;
            }

            // 对于 schedule 和 job，我们首先需要创建一个实例并填充其字段。
            FindNearestJob findJob = new FindNearestJob
            {
                TargetPositions = TargetPositions,
                SeekerPositions = SeekerPositions,
                NearestTargetPositions = NearestTargetPositions,
            };

            // Schedule() 会将 Job 实例放入 Job 队列中
            JobHandle findHandle = findJob.Schedule();

            // Complete 方法将不会返回，直到 job 表示句柄完成执行。实际上，主线程等待直到 job 完成。
            // // Complete() 方法在由该 handle 表示的 Job 执行完成之前不会返回。在某些情况下，Job 可能在调用 Complete() 之前就已经执行完成。
            // 无论如何，Complete() 只有在 Job 完成后才会返回。
            findHandle.Complete();

            // 从每个导引头到最近的目标绘制一条调试线。
            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                // float3 隐式转换为 Vector3
                Debug.DrawLine(SeekerPositions[i], NearestTargetPositions[i]);
            }
        }
    }
}
