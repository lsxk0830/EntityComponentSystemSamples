using Spawner = Tutorials.Jobs.Step2.Spawner;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Tutorials.Jobs.Step3
{
    // 与之前的步骤完全相同，只是这会调度并行 job 而不是单线程 job。
    public class FindNearest : MonoBehaviour
    {
        NativeArray<float3> TargetPositions;
        NativeArray<float3> SeekerPositions;
        NativeArray<float3> NearestTargetPositions;

        public void Start()
        {
            Spawner spawner = GetComponent<Spawner>();
            TargetPositions = new NativeArray<float3>(spawner.NumTargets, Allocator.Persistent);
            SeekerPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
            NearestTargetPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
        }

        public void OnDestroy()
        {
            TargetPositions.Dispose();
            SeekerPositions.Dispose();
            NearestTargetPositions.Dispose();
        }

        public void Update()
        {
            for (int i = 0; i < TargetPositions.Length; i++)
            {
                TargetPositions[i] = Spawner.TargetTransforms[i].localPosition;
            }

            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                SeekerPositions[i] = Spawner.SeekerTransforms[i].localPosition;
            }

            FindNearestJob findJob = new FindNearestJob
            {
                TargetPositions = TargetPositions,
                SeekerPositions = SeekerPositions,
                NearestTargetPositions = NearestTargetPositions,
            };

            // 对于 SeekerPositions 数组的每个元素都会调用一次 Execute，
            // 每个索引从 0 到（但不包括）数组的长度。
            // 执行调用将被分成 100 个批次。
            // 这个 Job 会处理每一个 Seeker，因此使用 Seeker 数组的长度作为索引总数（Index Count）
            // 这里选择批处理大小（Batch Size）为 100，主要是一个比较随意的取值，只是因为它既不会太大，也不会太小
            JobHandle findHandle = findJob.Schedule(SeekerPositions.Length, 100);

            findHandle.Complete();

            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                Debug.DrawLine(SeekerPositions[i], NearestTargetPositions[i]);
            }
        }
    }
}
