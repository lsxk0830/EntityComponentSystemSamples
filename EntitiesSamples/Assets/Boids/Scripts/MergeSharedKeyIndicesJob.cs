using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Boids
{
    // IJobNativeParallelMultiHashMapMergedSharedKeyIndices：自定义 job 类型，遵循自己定义的自定义安全规则：
    // A）因为我们知道哈希图安全性如何工作，B）我们可以安全地并行迭代
    // 显着特点：
    // 1）哈希图必须是 NativeParallelMultiHashMap<int,int>，其中键是一些数据的哈希，索引是
    // 唯一索引（通常是其他集合中的相关数据）。
    // 2) 每个桶与其他桶同时处理。
    // 3) 每个存储桶中的所有键/值对都由单个线程单独处理（按顺序）。
    [JobProducerType(typeof(JobNativeParallelMultiHashMapUniqueHashExtensions.JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<>))]
    public interface IJobNativeParallelMultiHashMapMergedSharedKeyIndices
    {
        // 第一次遇到每个键（=哈希）时，将使用相应的值（=索引）调用 ExecuteFirst()。
        void ExecuteFirst(int index);

        // 对于存储桶中相同键的每个后续实例，使用相应的调用 ExecuteNext()
        // value (=index) for that key, as well as the value passed to ExecuteFirst() the first time this key
        // was encountered (=firstIndex).
        void ExecuteNext(int firstIndex, int index);
    }

    [BurstCompile]
    public static class JobNativeParallelMultiHashMapUniqueHashExtensions
    {
        internal struct JobWrapper<T> where T : struct
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, int> HashMap;
            public T JobData;
        }

        /// <summary>
        /// 收集并缓存内部 job system 的托管绑定的反射数据。Unity 负责调用此方法 - 不要自己调用它。
        /// </summary>
        /// ZXQ 红笔 WZXUZXQ
        /// <remarks>
        /// 当项目中包含 Jobs package 时，Unity 会生成在启动时调用 EarlyJobInit 的代码。这允许 Burst 将代码编译为 schedule jobs，因为与突发编译器约束不兼容的初始化反射部分已经发生在 EarlyJobInit 中。
        ///
        /// __Note__：虽然 Jobs package 代码生成器会自动为所有封闭的 job 类型处理此问题，但您必须为每个专业化手动注册那些通用参数（例如 IJobChunk<MyJobType<T>>） [[Unity.Jobs.RegisterGenericJobTypeAttribute]]。
        /// </remarks>
        public static void EarlyJobInit<T>()
            where T : struct, IJobNativeParallelMultiHashMapMergedSharedKeyIndices
        {
            JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<T>.Initialize();
        }

        public static unsafe JobHandle Schedule<T>(this T jobData, NativeParallelMultiHashMap<int, int> hashMap,
                int minIndicesPerJobCount, JobHandle dependsOn = default)
            where T : struct, IJobNativeParallelMultiHashMapMergedSharedKeyIndices
        {
            var jobWrapper = new JobWrapper<T>
            {
                HashMap = hashMap,
                JobData = jobData,
            };
            JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<T>.Initialize();
            var reflectionData = JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<T>.reflectionData.Data;
            CollectionHelper.CheckReflectionDataCorrect<T>(reflectionData);

            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobWrapper),
                reflectionData,
                dependsOn,
                ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, hashMap.GetUnsafeBucketData().bucketCapacityMask + 1, minIndicesPerJobCount);
        }


        [BurstCompile]
        internal struct JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<T>
            where T : struct, IJobNativeParallelMultiHashMapMergedSharedKeyIndices
        {
            internal static readonly SharedStatic<IntPtr> reflectionData = SharedStatic<IntPtr>.GetOrCreate<JobNativeParallelMultiHashMapMergedSharedKeyIndicesProducer<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (reflectionData.Data == IntPtr.Zero)
                    reflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobWrapper<T>), typeof(T), (ExecuteJobFunction)Execute);
            }

            delegate void ExecuteJobFunction(ref JobWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            [BurstCompile]
            public static unsafe void Execute(ref JobWrapper<T> jobWrapper, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex)
            {
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out int begin, out int end))
                    {
                        return;
                    }

                    var bucketData = jobWrapper.HashMap.GetUnsafeBucketData();
                    var buckets = (int*)bucketData.buckets;
                    var nextPtrs = (int*)bucketData.next;
                    var keys = bucketData.keys;
                    var values = bucketData.values;

                    for (int i = begin; i < end; i++)
                    {
                        int entryIndex = buckets[i];

                        while (entryIndex != -1)
                        {
                            var key = UnsafeUtility.ReadArrayElement<int>(keys, entryIndex);
                            var value = UnsafeUtility.ReadArrayElement<int>(values, entryIndex);

                            jobWrapper.HashMap.TryGetFirstValue(key, out int firstValue, out NativeParallelMultiHashMapIterator<int> it);

                            // [macton] Didn't expect a usecase for this with multiple same values
                            // （因为它的预期用途是用于唯一索引。）
                            // https://forum.unity.com/threads/ijobnativemultihashmapmergedsharedkeyindices-unexpected-behavior.569107/#post-3788170
                            if (entryIndex == it.GetEntryIndex())
                            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobWrapper), value, 1);
#endif
                                jobWrapper.JobData.ExecuteFirst(value);
                            }
                            else
                            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                var startIndex = math.min(firstValue, value);
                                var lastIndex = math.max(firstValue, value);
                                var rangeLength = (lastIndex - startIndex) + 1;

                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobWrapper), startIndex, rangeLength);
#endif
                                jobWrapper.JobData.ExecuteNext(firstValue, value);
                            }

                            entryIndex = nextPtrs[entryIndex];
                        }
                    }
                }
            }
        }
    }
}
