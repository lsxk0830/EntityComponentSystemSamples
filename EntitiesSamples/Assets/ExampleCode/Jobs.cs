using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

#if false
namespace ExampleCode.IJobs
{
    // 示例 job 递增数组的所有数字。
    public struct IncrementJob : IJob
    {
        // job 需要使用的数据应该都是
        // 作为结构体的字段包含在内。
        public NativeArray<float> Nums;
        public float Increment;

        // Execute() is called when the job runs.
        public void Execute()
        {
            for (int i = 0; i < Nums.Length; i++)
            {
                Nums[i] += Increment;
            }
        }
    }

    // 调度 IJob 的 system。
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new IncrementJob
            {
                Nums = CollectionHelper.CreateNativeArray<float>(1000, state.WorldUpdateAllocator),
                Increment = 5f
            };

            JobHandle handle = job.Schedule();
            handle.Complete();
        }
    }
}

namespace ExampleCode.IJobParallelFors
{
    // 示例 job 并行递增数组的所有数字。
    public struct IncrementParallelJob : IJobParallelFor
    {
        // job 需要使用的数据必须全部
        // 作为结构体的字段包含在内。
        public NativeArray<float> Nums;
        public float Increment;

        // Execute(int) is called when the job runs.
        public void Execute(int index)
        {
            Nums[index] += Increment;
        }
    }

    // 调度 IJobParallelFor 的 system。
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new IncrementParallelJob
            {
                Nums = new NativeArray<float>(1000, state.WorldUpdateAllocator),
                Increment = 5f
            };

            JobHandle handle = job.Schedule(
                job.Nums.Length, // 调用执行的次数
                64); // 将调用分成 64 个批次
            handle.Complete();
        }
    }
}

namespace ExampleCode.IJobChunks
{
    // 示例 IJobChunk。
    [BurstCompile]
    public struct MyIJobChunk : IJobChunk
    {
        // job 需要每个 component 类型的类型句柄
        // 它将从 chunks 访问。
        public ComponentTypeHandle<Foo> FooHandle;

        // 只能读取的 components 的句柄应该是
        // 标有 [ReadOnly]。
        [ReadOnly] public ComponentTypeHandle<Bar> BarHandle;

        // 如果我们需要 entity 类型句柄
        // 想读 entity ID 的。
        public EntityTypeHandle EntityHandle;

        // Jobs 不应使用 EntityManager 来创建和修改
        // 直接 entities。相反，job 可以将命令记录到
        // 稍后播放的 EntityCommandBuffer
        // job 完成后的某个时刻的主线程。
        // 如果 job 将与 ScheduleParallel() 一起安排，
        // 我们必须使用 EntityCommandBuffer.ParallelWriter。
        public EntityCommandBuffer.ParallelWriter Ecb;

        // 当这个 job 运行时，Execute() 将被调用一次
        // chunk 与传递给 Schedule() 的 query 匹配。

        // 如果满足以下任一条件，则 useEnableMask 参数为 true
        // entities 中的 chunk 已禁用 components
        // query 的。换句话说，这个参数是 true
        // 如果 chunk 中的任何 entities 应被跳过。

        // chunkEnabledMask 标识哪个 entities
        // 启用 query 的所有 components，i.e。其中 entities
        // 应处理：
        //   - 设置位指示应处理 entity。
        //   - 清除位表示 entity 有一个或多个
        //     已禁用 components，因此应跳过。

        // `unfilteredChunkIndex` 是 chunk 在所有与 query 匹配的 chunks 的序列中的索引：第一个
        // 与 query 匹配的 chunk 是索引 0，第二个是索引 1，依此类推。该值主要用作
        // 传递给 `EntityCommandBuffer.ParallelWriter` 方法的*排序键*。每个记录的命令包括
        // a sortKey，在播放时，命令在执行之前会按这些键排序。
        // 这种排序有效地保证了命令将以确定的顺序执行，即使
        // 原始记录的命令顺序是不确定的。
        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnableMask,
            in v128 chunkEnabledMask)
        {
            // 从 chunk 获取 entity ID 和 component 数组。
            NativeArray<Entity> entities = chunk.GetNativeArray(EntityHandle);
            NativeArray<Foo> foos = chunk.GetNativeArray(ref FooHandle);
            NativeArray<Bar> bars = chunk.GetNativeArray(ref BarHandle);

            // ChunkEntityEnumerator 帮助我们循环
            // entities 的 chunk，但仅限于那些
            // 匹配 query（占禁用的 components）。
            var enumerator = new ChunkEntityEnumerator(useEnableMask, chunkEnabledMask, chunk.Count);

            // 循环遍历 chunk 中与 query 匹配的所有 entities。
            while (enumerator.NextEntityIndex(out var i))
            {
                // 读取 entity ID 和 component 值。
                var entity = entities[i];
                var foo = foos[i];
                var bar = bars[i];

                // 如果 Bar 值满足标准，我们
                // 在 ECB 中记录命令以将其删除。
                if (bar.Value < 0)
                {
                    Ecb.RemoveComponent<Bar>(unfilteredChunkIndex, entity);
                }

                // 设置 Foo 值。
                foos[i] = new Foo { };
            }
        }
    }

    // 一个 system，调度并完成上述 IJobChunk。
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 从以下位置获取 EntityCommandBuffer
            // BeginSimulationEntityCommandBufferSystem。
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // 创建 job。
            var job = new MyIJobChunk
            {
                FooHandle = state.GetComponentTypeHandle<Foo>(false),
                BarHandle = state.GetComponentTypeHandle<Bar>(true),
                Ecb = ecb.AsParallelWriter()
            };

            var myQuery = SystemAPI.QueryBuilder().WithAll<Foo, Bar, Apple>().WithNone<Banana>().Build();

            // Schedule job。
            // 通过调用 ScheduleParallel() 而不是 Schedule()，
            // 与 job 的 query 匹配的 chunks 将被拆分
            // 分成批次，并且这些批次可以被处理
            // 由工作线程并行执行。
            // 我们通过 state.Dependency 来确保这个 job 取决于
            // 之前的 system 更新中安排的任何重叠的 jobs。
            // 我们将返回的句柄分配给 state.Dependency 以确保
            // 该 job 作为依赖项传递给其他 systems。
            state.Dependency = job.ScheduleParallel(myQuery, state.Dependency);
        }
    }
}

namespace ExampleCode.IJobEntitys
{
    // 示例 IJobEntity 在功能上等同于上面的 IJobChunk。
    // `IJobEntity` 比其等效的 `IJobChunk` 更简洁，因为
    // 它的源代码生成处理一些样板文件。

    // 只有具有 Apple component 类型的 entities 才会匹配 job 的隐式 query
    // 即使 job 不访问 Apple component 值。
    [WithAll(typeof(Apple))]
    // 只有具有 Banana component 类型的 entities NOT 才会匹配 job 的隐式 query。
    [WithNone(typeof(Banana))]
    [BurstCompile]
    public partial struct MyIJobEntity : IJobEntity
    {
        // 由于源生成，IJobEntity 获得类型句柄
        // 它需要自动添加，因此我们不手动添加它们。

        // EntityCommandBuffers 等字段仍需
        // 手动包含。
        public EntityCommandBuffer.ParallelWriter Ecb;

        // 源生成将根据以下内容创建 EntityQuery
        // Execute() 的参数。在这种情况下，生成的 query 将
        // 匹配所有具有 Foo 和 Bar component 的 entities。
        //   - 当这个 job 运行时，Execute() 将被调用一次
        //     对于每个与 ​​query 匹配的 entity。
        //   - 任何带有禁用 Foo 或 Bar 的 entity 将被跳过。
        //   - 'ref' 参数 components 是可读写的
        //   -“in”参数 components 是只读的
        //   - 我们需要将 chunk 索引作为 sortKey 传递给方法
        //     EntityCommandBuffer.ParallelWriter，所以我们包括一个
        //     具有 [ChunkIndexInQuery] 属性的 int 参数。
        [BurstCompile]
        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref Foo foo, in Bar bar)
        {
            // 如果 Bar 值满足此标准，我们
            // 在 ECB 中记录命令以将其删除。
            if (bar.Value < 0)
            {
                Ecb.RemoveComponent<Bar>(chunkIndex, entity);
            }

            // 设置 Foo 值。
            foo = new Foo { };
        }
    }

    // 一个 system，调度并完成上述 IJobEntity。
    public partial struct MySystem : ISystem
    {
        // 我们不需要手动创建 query 因为源生成
        // 创建一个从 IJobEntity 的属性和执行参数推断的值。

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 从 BeginSimulationEntityCommandBufferSystem 获取 EntityCommandBuffer。
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // 创建 job。
            var job = new MyIJobEntity
            {
                Ecb = ecb.AsParallelWriter()
            };

            // Schedule job。源生成隐式创建并传递 query。
            state.Dependency = job.Schedule(state.Dependency);
        }
    }
}
#endif
