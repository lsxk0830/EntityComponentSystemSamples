最好使用菜单栏中的此选项查看本文档：*视图 → 文本宽度 → 宽*

# Unity Job System 101

[谷歌文档](https://docs.google.com/document/d/1gtXwUwsuQTfpBUmdFd5ieZaL7v3UdYTKq9H5P0M57Mg/edit?tab=t.0)

本文档介绍了非托管集合和 C# job system 的基本概念和用法。最后，我们将演练一个简单的示例项目，演示 jobs 的基本用法和优点。

## C\# Jobs

C\# job system 允许您将工作放入队列中以由工作线程池执行：

* 当工作线程完成其当前工作时，该线程将从队列中拉出等待的 job 和 run job （通过调用其 Execute() 方法）。
* job 类型是通过定义实现 IJob 或其他 job 接口之一（IJobParallelFor、IJobEntity、IJobChunk...）的结构来创建的。
* 要将 job 实例放入 job 队列中，请调用扩展方法 Schedule()。Jobs 只能从主线程调度，不能从其他 jobs 内部调度。

```c#
public struct ExampleJob : IJob
{
    // Execute()是Job启动时调用
    public void Execute()
    {
    	// ...
    }
}

// ... 在（Unity 的）主线程上的某个地方（例如，在 MonoBehaviour 的 Update 方法中）

// 调度一个Job 
var job = new ExampleJob{};
job.Schedule();
```

## Job 依赖项

**工作线程不会将 job 从 job 队列中拉出，直到它所依赖的其他 jobs 全部完成执行为止。** 实际上，job 依赖项允许您新调度的 jobs 等待先前调度的 jobs ，从而在需要时控制执行顺序。

仅当 job 被调度时，才可以为其赋予依赖关系：

* Schedule() 返回代表新 job 的 JobHandle。
* 如果将 JobHandle 传递给 Schedule()，则新的 job 将取决于句柄表示的 job。

```c#
// Schedule a job.
var job1 = new ExampleJob{};
JobHandle handle1 = job1.Schedule();

// 调度另一个相同类型的 Job，并让它依赖于第一个 Job
// 由于这个 Job 依赖于第一个 Job，因此在第一个 Job 执行完成之前，工作线程（Worker Thread）不会执行第二个 Job
var job2 = new ExampleJob{};
JobHandle handle2 = job1.Schedule(handle1);
```

虽然 Schedule() 仅采用一个 JobHandle 参数，但我们可以使用 **JobHandle.CombineDependencies()** 将多个句柄组合成一个逻辑句柄，从而允许我们为 job 提供多个直接依赖项。

```c#
// 将三个 JobHandle 合并成一个
JobHandle combinedHandle = JobHandle.CombineDependencies(handle1, handle2, handle3);

// 调度一个同时依赖于 handle1、handle2 和 handle3 的 Job
var job = new ExampleJob{};
job.Schedule(combinedHandle);
```

## 正在完成 jobs

在调度 job 后的某个时刻，您应该在主线程上调用 JobHandle 的 Complete() 方法。完成 job 会按以下顺序执行一些操作：

1. 完成 job 的所有依赖（包括依赖的依赖，递归）
2. 等待 job 完成执行（如果尚未完成）
3. 从 job 队列中删除对 job 的所有剩余引用

实际上，一旦 Complete() 返回，job 及其所有依赖项就保证已完成执行并已从队列中删除

另请注意：

* 在已完成的 job 的句柄上调用 Complete() 不会执行任何操作，也不会引发任何错误 
* 与调度一样，只有主线程可以完成 jobs：在 job 内调用 Complete() 是无效的
* 尽管 job 可以在计划后立即完成，但通常最好推迟完成 job，直到工作实际需要完成的最晚可能时刻。一般来说，jobs 的调度和完成之间的间隔越长，主线程和工作线程花费不必要的空闲时间的可能性就越小。


## jobs 中的数据访问

作为一般规则：

* job 不应执行 I/O
* job 不应访问托管对象
* job 只能访问只读静态字段

另请注意，调度 job 会创建该结构的“私有副本”，该副本仅对正在运行的 job 可见。因此，对 job 中字段的任何修改仅在 job 中可见。然而，如果 job 的字段包含指针或引用，则 job 对该外部数据的修改在 job 外部可见。

## 非托管集合

Unity.Collections package 的非托管集合类型比普通 C\# 托管集合有一些优势：

* 非托管对象可以在 Burst 编译的代码中使用
* 非托管对象可以在 jobs 中使用（而在 jobs 中使用托管对象通常并不安全）
* 本机集合类型具有“job 安全检查”，可在 jobs 中强制执行线程安全
* 非托管对象不会被垃圾回收，因此不会引起垃圾回收开销

不利的一面是，一旦不再需要每个非托管集合，您就有责任调用 **Dispose()**。如果您忽略处置集合，则会造成[内存泄漏](https://en.wikipedia.org/wiki/Memory_leak)。然而，对于 Native 集合，“处置安全检查”可以捕获许多（但不是全部）这些泄漏并引发错误。

### 分配器

实例化非托管集合时，必须指定分配器。不同的分配器以不同的方式组织和跟踪它们的内存。三种最常用的分配器是：

* **Allocator.Persistent**：最慢的分配器。用于无限期的生命周期分配。当您不再需要持久分配的集合时，您必须调用 Dispose() 来取消分配它。
* **Allocator.Temp**：最快的分配器。用于短期分配。每一帧，主线程都会创建一个临时分配器，该分配器在帧结束时被全部释放。因为 Temp 分配器会作为一个整体被丢弃，所以您实际上不需要手动取消 Temp 分配（事实上，在 Temp 分配的集合上调用 Dispose() 不会执行任何操作）。
* **Allocator.TempJob**：可以传递到 jobs 的中间层速度分配。

传递给 job 的集​​合必须使用 Allocator.Persistent、Allocator.TempJob 或其他线程安全分配器进行分配。

```c#
public struct ExampleJob : IJob
{
	public NativeList<int> Nums;

	public void Execute() { ... }
}

// ... 在（Unity 的）主线程上的某个地方（例如，在 MonoBehaviour 的 Update 方法中）

// 传递给 Job 的这个列表（List）必须使用线程安全的分配器（Allocator）创建
// 如果这里使用 Allocator.Temp，那么在调度（Schedule）该 Job 时会触发错误
var list = new NativeList<int>(100, Allocator.TempJob);

var job = new ExampleJob{ Nums = list };
job.Schedule();
```

使用 Allocator.Temp 分配的集合不能传递到 jobs 中。但是，job 的每个线程都有自己的临时分配器，因此 Allocator.Temp 在 jobs 中可以安全使用。job 中的所有临时分配将在 job 末尾自动处置。

使用 Allocator.TempJob 进行的分配必须手动处置。如果启用了处置安全检查，则当使用 Allocator.TempJob 进行的任何分配在分配后 4 帧内未处置时，将引发异常。

### Job 安全检查

对于访问相同数据的任何两个 jobs，通常不希望它们的执行重叠或执行顺序不确定。例如，如果两个 jobs 读写一个原生数组的内容，我们应该确保两个 jobs 之一在另一个开始之前完成执行。否则，当任一 job 修改数组时，这些修改可能会干扰另一个 job.根据 job 在另一个 job 之前运行的情况以及它们的执行是否重叠，一个或两个 jobs 可能会产生错误的结果。

因此，当两个 jobs 之间存在此类数据冲突时，您应该：

* Schedule 并在安排另一项之前完成一项 job...
* ...或 schedule 一个 job 作为另一个的依赖项。


无论哪种方式，都会保证两个 jobs 不会重叠并按一定的顺序执行。

当您调用 Schedule() 时，如果 job 安全检查（如果启用）检测到潜在的竞争条件，它们将引发异常。例如，如果您首先使用本机数组的 schedule 一个 job，然后使用 schedule 另一个使用相同本机数组但不依赖于第一个 job 的 job，则会引发异常。如果允许这样的安排，一个或两个 jobs 可能会产生错误或不一致的结果。

同样：

* 作为一种特殊情况，如果两个 jobs 都只“读取”数据，那么两个 jobs 访问相同的数据总是安全的。因为 job 都不修改数据，所以不会互相干扰。我们可以通过使用 **[ReadOnly]** 属性标记结构字段来指示只能在 job 中读取本机数组或集合。如果两个 jobs 共享的所有本机数组或集合在两个 jobs 中都标记为[ReadOnly]，则 job 安全检查不会认为两个 jobs 发生冲突。
* 在某些情况下，您可能希望对 job 中使用的特定本机数组或集合完全禁用 job 安全检查。这可以通过使用**[NativeDisableContainerSafetyRestriction]** 属性对其进行标记来完成。只要确保您没有创建竞争条件即可！
* 当任何当前计划的 jobs 使用本机集合时，如果您尝试在主线程上读取或修改该本机集合，安全检查将引发异常。作为一种特殊情况，如果在调度的 jobs 中标记为 [ReadOnly]，主线程可以从本机集合中读取。

## 并行 Jobs

为了将处理数组或列表的工作拆分到多个线程中，我们可以使用 **IJobParallelFor** 接口定义 job：

```c#
[BurstCompile]
public struct SquareNumbersJob : IJobParallelFor
{
    public NativeArray<int> Nums;

    // 每次 Execute 调用只处理一个索引（Index）
    public void Execute(int index)
    {
    	Nums[index] *= Nums[index];
    }
}
```

当我们 schedule job 时，我们指定索引计数和批次大小：

```java
// ... scheduling the job
var job = new SquareNumbersJob { Nums = myArray };
JobHandle handle = job.Schedule(
myArray.Length,    // count
100);              // batch size
```

当 job 运行时，其 Execute() 方法将被调用 *count* 次，并将从 0 到 *count* 的所有值传递给 *index* 参数。

job 的索引被分成由批次大小确定的批次，然后工作线程可以从队列中单独抓取这些批次。实际上，单独的批次可以在单独的线程上同时处理，但单个批次的所有索引将在单个线程中一起处理。

在此示例中，如果数组长度为 250，则 job 将分为三批：第一批覆盖索引 0 到 99；第二批覆盖索引 100 到 199；最后一批覆盖其余部分，索引 200 到 249。由于 job 被分为三批，因此最多将在三个工作线程中进行有效处理。如果我们想将 job 拆分到更多线程，我们必须选择较小的批量大小。

**注意**：选择好的批量大小并不是一门精确的科学！在极端情况下，我们可以选择批量大小为 1，从而将每个单独的索引拆分为自己的批量。但请记住，拥有太多小批量可能会产生大量 job system 开销。一般来说，您应该选择一个看起来不太大但也不太小的批量大小，然后尝试找到对于每个特定 job 来说似乎最佳的大小。

处理批次时，它应该只访问批次指定范围内的数组或列表的索引。为了强制执行此操作，如果您使用索引参数以外的任何值访问数组或列表的索引，安全检查将引发异常：

```c#
[BurstCompile]
public struct MyJob : IJobParallelFor
{
    public NativeArray<int> Nums;

    public void Execute(int index)
    {
        // 表达式 Nums[0] 会触发安全检查异常（Safety Check Exception）
        Nums[index] = Nums[0];
    }
}
```

此限制不适用于标有 **[ReadOnly]** 属性的数组和列表字段。对于需要写入 job 中的数组或列表，可以通过用 **[NativeDisableParallelForRestriction]** 标记字段来禁用限制。请注意，放宽此限制不会导致可能的竞争条件！

# 示例：目标和搜寻者

*请参阅[此示例的视频演练](https://youtu.be/oOgNg2gL2yw)（17 分钟）*

我们要解决的问题：

* 搜索者（蓝色立方体）和目标（红色立方体）各自在 2D 平面上沿随机方向缓慢移动。
* 从每个搜索器到最近的目标绘制一条白色调试线。

<img src=".\Texture\JobSystem101\find_target.gif" align="left" />

我们将针对这个问题提出四种解决方案：

1. **不使用 jobs 的解决方案**
2. **使用单线程 job 的解决方案**
3. **使用并行 job 的解决方案**
4. **以及使用并行 job 和比暴力更智能的算法的解决方案**

## 解决方案 1 \- 仅主线程

* 单个 Spawner.cs (Mono) 在 XZ 平面上的 500 x 500 区域内实例化 1000 个追寻者（蓝色立方体）和 1000 个目标（红色立方体）。
* 追寻者和目标沿着随机矢量缓慢移动每个追寻者和目标。
* Spawner 将目标的变换存储在静态数组中。
* FindNearest.cs (Mono) （附加到追寻者 prefab）使用目标变换数组来查找最近的目标位置并绘制白色调试线。

这里使用的寻找算法很简单：对于每个搜索者，我们循环遍历每个目标。

以下是具有 1000 个追寻者和 1000 个目标的典型框架的概况：

<img src=".\Texture\JobSystem101\step1_profile.png" align="left" />

每个追寻者需要约 0.3 毫秒来更新，总共花费超过 330 毫秒。

## 方案 2 \-单线程 job

* 生成器将搜索者和目标的变换存储在静态数组中。
* FindNearest.cs (Mono)  从Seeker.prefab 移至场景物体上
* FindNearest 将追寻者和目标变换的 localPosition 值复制到 float3 的 NativeArrays 中。
* FindNearest 调度并完成新的 FindNearestJob，它使用这些 NativeArrays。

通过将艰苦的工作放入 job 中，我们可以将工作从主线程移动到工作线程，并且我们可以 Burst 编译代码。Jobs 和 Burst 编译的代码无法访问任何托管对象（包括 GameObjects 和 GameObject components），因此我们必须首先将 job 要处理的所有数据复制到非托管集合中，例如 NativeArray 的。

**注意**：严格来说，jobs 实际上可以访问托管对象，但这样做需要特别小心，通常不是一个好主意。此外，我们肯定想用 Burst 编译这个 job，而 Burst 编译的代码严格来说根本无法访问托管对象。

虽然我们仍然可以使用 Vector3 和 Mathf，但我们将使用 Unity.Mathematics package 中的 float3 和 math，它具有针对 Burst 的特殊优化挂钩。

那么我们的 FindNearestJob job 的定义方式如下：

```c#
public struct FindNearestJob : IJob
{
    // 任何 Job 在执行过程中要使用的数据，都必须声明为该 Job 结构体的字段
    public NativeArray<float3> TargetPositions;
    public NativeArray<float3> SeekerPositions;
    public NativeArray<float3> NearestTargetPositions;

    public void Execute()
    {
    // ... Job 将要执行的代码写在这里
    }
}
```

FindNearest 的 Update()：

1. 将追寻者和目标变换复制到 float3 的 NativeArrays 中
2. 创建 FindNearestJob 的实例，并使用 NativeArrays 初始化其字段
3. 在 job 实例上调用 Schedule() 扩展方法
4. 在 Schedule() 返回的 JobHandle 上调用 Complete()
5. 使用 NearestTargetPositions 数组（由 job 填充）来绘制从每个导引器到其最近目标的调试线

以下是实例化、调度和完成 job 的摘录：

```c#
// 对于 schedule 和 job，我们首先需要创建一个实例并填充其字段
FindNearestJob findJob = new FindNearestJob
{
    TargetPositions = TargetPositions,
    SeekerPositions = SeekerPositions,
    NearestTargetPositions = NearestTargetPositions,
};

// Schedule() 会将 Job 实例放入 Job 队列中
JobHandle findHandle = findJob.Schedule();

// Complete() 方法在由该 handle 表示的 Job 执行完成之前不会返回。在某些情况下，Job 可能在调用 Complete() 之前就已经执行完成。
// 无论如何，Complete() 只有在 Job 完成后才会返回。
findHandle.Complete();
```

以下是具有 1000 个追寻者和 1000 个目标的典型框架的概况，没有使用 Burst 编译 job：

<img src=".\Texture\JobSystem101\step2_profile_no_burst.png" align="left" />

\~30ms 肯定比我们之前看到的 \~330ms 好，但接下来让我们通过在 job 结构体上添加 \[BurstCompile\] 属性来启用 Burst 编译。还要确保在菜单栏中启用了 Burst 编译：

<img src=".\Texture\JobSystem101\enable_burst.png" align="left" />

这是启用 Burst 后得到的结果：

<img src=".\Texture\JobSystem101\step2_profile.png" align="left" />

在~1.5ms 时，我们完全在 60fps 的 16.6ms 预算之内。

由于我们现在有这么多的空间，让我们尝试增加到 10,000 个搜索者和 10,000 个目标：

<img src=".\Texture\JobSystem101\step2_profile_10000.png" align="left" />

追寻者和目标增加 10 倍会导致 run 时间增加 70 倍，但考虑到每个追寻者都会检查其与每个目标的距离，这是预期的。

**注意**：如果你太早等待 Job 完成，Unity 可能直接在主线程执行这个 Job，以避免浪费时间等待 Worker Thread。

## 解决方案 3 \-并行 job

* FindNearestJob.cs 现在实现 IJobParallelFor 而不是 IJob。
* FindNearest 中的 Schedule() 调用现在采用两个 int 参数：索引计数和批量大小。

对于处理数组或列表的 job，通常可以通过将索引拆分为子范围来并行化工作。例如，数组的一半可以在一个线程上处理，而另一半则同时在另一个线程上处理。

我们可以使用 IJobParallelFor 方便地创建这样的 jobs，其 Schedule() 方法接受两个 int 参数：

* 索引计数：正在处理的数组或列表的大小
* 批量大小：子范围的大小，a.k.a.批次

例如，如果 job 的索引计数为 100，其批次大小为 40，则 job 会分为三个批次：第一个覆盖索引 0 到 39；第二覆盖索引 40 至 79；第三个覆盖索引 80 到 99。

工作线程将这些批次单独从队列中拉出，因此单个 job 的批次可以在不同线程上同时处理.

IJobParallelFor 的 Execute() 方法采用索引参数，并为每个索引调用一次，从 0 到索引计数：

```c#
public struct FindNearestJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> TargetPositions;
    [ReadOnly] public NativeArray<float3> SeekerPositions;
    public NativeArray<float3> NearestTargetPositions;

    // 每个Execute调用只处理一个单独的索引
    public void Execute(int index)
    {
    	// ...
    }
}

// 这个 Job 会处理每一个 Seeker，因此使用 Seeker 数组的长度作为索引总数（Index Count）
// 这里选择批处理大小（Batch Size）为 100，主要是一个比较随意的取值，只是因为它既不会太大，也不会太小
JobHandle findHandle = findJob.Schedule(SeekerPositions.Length, 100);
```

具有 10,000 个追寻者和 10,000 个目标的典型框架的轮廓：

<img src=".\Texture\JobSystem101\step3_profile.png" align="left" />

由于工作分散在 16 个内核上，CPU 总共需要约 260 毫秒的时间，但从开始到结束的经过时间不到 17 毫秒。

## 解决方案 4 \- 并行 job 和更智能的算法

* FindNearest MonoBehaviour 现在安排额外的 job 来根据 X 坐标对目标位置数组进行排序。
* 由于目标位置数组已排序，FindNearestJob 不再需要详尽地考虑每个目标。

如果我们以某种方式组织数据，例如将目标分类到四叉树或 k-d 树中，我们可以获得巨大的性能提升。为了简单起见，我们将仅按目标的 X 坐标对目标进行排序，尽管我们也可以按 Z 坐标进行排序（选择是任意的）。然后可以通过以下步骤找到距离单个追寻者最近的目标：

1. 使用最接近搜索者 X 坐标的 X 坐标对目标进行二分搜索。
2. 从该目标的索引开始，在数组中上下搜索二维距离较小的目标。
3. 当我们在数组中上下搜索时，一旦 X 轴距离超过到当前候选最近目标的二维距离，我们就会提前退出。

这里的关键见解是：

* 我们不需要检查任何 X 轴距离大于当前候选的二维距离的目标。
* 假设目标按照 X 坐标排序，如果一个目标的 X 轴距离太大，那么数组中所有目标到它一侧的 X 轴距离也一定太大。

因此，搜索不再需要考虑每个搜索者的每个目标。

我们可以编写自己的 job 来进行排序，但我们将调用 NativeArray 扩展方法 SortJob()，它返回 SortJob 结构体。SortJob 的 Schedule() 方法调度两个 jobs：SegmentSort（并行对数组的各个段进行排序）和 SegmentSortMerge（合并已排序的段）。（合并必须在单独的单线程 job 中完成，因为合并算法无法并行化。）

在 SegmentSort job 本身完成执行之前，SegmentSortMerge job 不应开始执行，因此 SegmentSort job 成为 SegmentSortMerge 的依赖项 job。通常，工作线程在其所有依赖项完成执行之前不会执行 job。Job 依赖关系有效地允许我们指定 jobs 和 schedule 之间的顺序执行顺序。

我们的 FindNearestJob 需要等待排序完成，所以它必须依赖于排序 jobs。这是创建和调度 jobs 的代码：

```java
SortJob<float3, AxisXComparer> sortJob = TargetPositions.SortJob(
    new AxisXComparer { });

FindNearestJob findJob = new FindNearestJob
{
TargetPositions = TargetPositions,
SeekerPositions = SeekerPositions,
NearestTargetPositions = NearestTargetPositions,
};

JobHandle sortHandle = sortJob.Schedule();

// To make the find job depend upon the sorting jobs,
// we pass the sort job handle when we schedule the find job.
JobHandle findHandle = findJob.Schedule(
SeekerPositions.Length, 100, sortHandle);

// Completing a job also completes all of its
// dependencies, so completing the find job also
// completes the sort jobs.
findHandle.Complete();
```

jobs 的顺序是：SegmentSort \-\> SegmentSortMerge \-\> FindNearestJob。

如果我们忽略使排序 job 成为查找 job 的依赖项，则当我们尝试 schedule 查找 job 时，job 安全检查将引发异常。

对于此解决方案，以下是具有 10,000 个追寻者和 10,000 个目标的典型框架的概况：

<img src=".\Texture\JobSystem101\step4_profile.png" align="left" />现在，FindNearestJob 总共只需要 7.5 毫秒的 CPU 时间和从开始到结束的 0.5 毫秒的经过时间。

放大后，我们可以看到 SegmentSort 和 SegmentSortMerge jobs：

<img src=".\Texture\JobSystem101\step4_profile_sort.png" align="left" />

SegmentSort 从开始到结束的时间不到 0.1ms，单线程 SegmentSortMerge 需要约 0.5ms。与 FindNearestJob 的巨大改进相比，额外的排序步骤非常值得额外的成本。

现在大部分帧时间都被 GameObjects 的低效率所消耗，这可以通过用 entities 替换 GameObjects 来解决

------

## 对比表

这个示例的核心问题是：每个 Seeker 都要在所有 Target 中找到距离最近的一个。最朴素的写法需要对每个 Seeker 遍历全部 Target，因此计算量大致是 `Seekers * Targets`。后续几个 Step 的优化方向可以分成两类：

- Step 2 和 Step 3 主要优化执行方式：让同样的计算跑在更适合 CPU 的数据结构、Burst 编译代码和多线程 Job 上。
- Step 4 开始优化算法本身：通过排序和提前退出，减少每个 Seeker 实际需要检查的 Target 数量。

|  步骤  | 实现方式            | 核心变化                                                     | 典型耗时                                                     | 为什么能降低 ms                                              |
| :----: | ------------------- | :----------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Step 1 | 无 Jobs             | 每个 Seeker 的 `Update()` 自己遍历全部 Target                | 1000 x 1000 时约 330ms                                       | 所有查找都在主线程执行，并且核心循环频繁访问 `Transform` 等托管对象，CPU 难以高效批量执行。 |
| Step 2 | 单线程 Job + Burst  | 把查找搬到单个 `IJob`，数据放入 `NativeArray<float3>`，用 Burst 编译 | 不开 Burst 约 30ms，开 Burst 约 1.5ms                        | 算法仍是全量遍历，但核心循环脱离 GameObject/Transform，使用连续的原生数组和 `Unity.Mathematics`，Burst 能生成更紧凑、更高效的机器码。 |
| Step 3 | Parallel Job        | 把 `IJob` 改成 `IJobParallelFor`，按 Seeker 拆分批次并行执行 | 10000 x 10000 时总 CPU 约 260ms，墙钟时间低于 17ms           | 每个 Seeker 的最近目标查找互不依赖，可以分给多个 worker 线程同时做。总计算量没有减少，但等待结果的实际时间下降。 |
| Step 4 | Parallel Job + Sort | Target 按 X 坐标排序，查找时二分定位并向两侧剪枝搜索         | `FindNearestJob` 总 CPU 约 7.5ms，墙钟约 0.5ms，排序额外约 0.5ms | 不再总是检查所有 Target。排序后可以利用 X 轴距离判断后续目标不可能更近，从而提前退出，距离计算次数大幅减少。 |

------

## 总结

四个 Step 的优化路径可以概括为：

- Step 1：朴素主线程实现，简单但所有压力都在主线程和托管对象访问上。
- Step 2：把核心循环搬进 Burst Job，让数据更连续、代码更接近原生数值计算。
- Step 3：把每个 Seeker 的独立查找分发到多个线程，降低墙钟时间。
- Step 4：通过排序和剪枝减少实际比较次数，从算法层面降低工作量。

所以，ms 降低并不是单一原因造成的，而是逐层叠加的结果：先减少托管和主线程开销，再利用 Burst 优化 CPU 指令，再利用多核并行，最后减少算法本身需要做的计算。

# 其他资源

* [收藏 package 备忘单](./EntitiesSamples/Docs/cheatsheet/collections.md)
* [博客文章：改进 Job System 性能第 1 部分](https://blog.unity.com/engine-platform/improving-job-system-performance-2022-2-part-1)
* [博客文章：改进 Job System 性能第 2 部分](https://blog.unity.com/engine-platform/improving-job-system-performance-2022-2-part-2)
