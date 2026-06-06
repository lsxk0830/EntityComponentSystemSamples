# C# Job system

在此页面中：

- [非托管集合](#unmanaged-collections)
- [C# Jobs](#c-jobs-and-job-dependencies)
- [Job 依赖项](#c-jobs-and-job-dependencies)
- [Job 安全检查](#job-safety-checks)
- [并行 Jobs](#parallel-jobs)

进一步阅读：

1. [博客文章：改进 Job System 性能第 1 部分](https://blog.unity.com/engine-platform/improving-job-system-performance-2022-2-part-1)
1. [博客文章：改进 Job System 性能第 2 部分](https://blog.unity.com/engine-platform/improving-job-system-performance-2022-2-part-2)

<br/>

# 非托管集合

`Unity.Collections` 的非托管集合类型比普通 C# 托管集合有一些优势：

- 非托管对象可以在 Burst 编译的代码中使用。
- 非托管对象可以在 jobs 中使用，而在 jobs 中使用托管对象并不总是安全的。
- `Native-` 集合类型具有安全检查，有助于在 jobs 中强制执行线程安全。
- 非托管对象不会被垃圾回收，因此不会产生垃圾回收开销。

不利的一面是，一旦不再需要每个非托管集合，您就有责任对它调用 `Dispose()`。忽略处置集合会导致内存泄漏，并且处置安全检查将引发错误。

*[查看有关非托管集合的更多信息](./cheatsheet/collections.md)。*

## 分配器

实例化非托管集合时，必须指定*分配器*。不同的分配器以不同的方式组织和跟踪它们的内存。三种最常用的分配器是：

- `Allocator.Persistent`：**最慢的分配器。用于无限期的生命周期分配。** 当您不再需要持久分配的集合时，您必须调用 `Dispose()` 来释放它。
- `Allocator.Temp`：**最快的分配器。用于短期分配。** 每个帧，主线程都会创建一个临时分配器，该分配器在帧结束时全部释放。因为 Temp 分配器会作为一个整体被丢弃，所以您实际上不需要手动取消 Temp 分配，事实上，在 Temp 分配的集合上调用 `Dispose()` 是无操作的。
- `Allocator.TempJob`：*（[下面讨论]（#allocations-within-jobs））*

<br/>

# C# Jobs 和 Job 依赖项

&#x1F579;  *[参见示例 jobs](../Assets/ExampleCode/Jobs.cs)。*

C# Jobs system 允许我们在工作线程池中执行 schedule 工作：

- 当工作线程完成当前工作时，该线程会将等待的 job 从队列中拉出，并调用 job 的 `Execute()` 方法到 run 和 job。
- job 类型是通过定义实现 [`IJob`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJob.html) 或其他 job 接口之一（`IJobParallelFor`、`IJobEntity`、`IJobChunk`...）。
- 要将 job 实例放入 job 队列中，请调用扩展方法 `Schedule()`。Jobs 只能从主线程调度，不能从其他 jobs 内部调度。

<br>

## 依赖关系

`Schedule()` 返回表示计划的 job 的 [`JobHandle`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html)。如果将 `JobHandle` 传递给 `Schedule()`，则新的 job 将*依赖于由句柄表示的 job。

**工作线程不会将 job 从 job 队列中拉出，直到 job 的依赖项全部完成执行。**因此，我们可以使用依赖项来规定调度的 jobs 之间的执行顺序。

虽然 `Schedule()` 仅采用一个 `JobHandle` 参数，但我们可以使用 [`JobHandle.CombineDependencies()`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.CombineDependencies.html) 将多个句柄组合成一个逻辑句柄，从而允许 job 具有多个直接依赖项。

<br>

## 正在完成 jobs

在调度 job 后的某个时刻，主线程应该在主线程上调用 `JobHandle` 的 [`Complete()`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.Complete.html) 方法。完成 job 会按以下顺序执行一些操作：

1. 递归完成 job 的所有依赖。
1. 如果 job 尚未完成，则等待其完成执行。
1. 从 job 队列中删除 job 的所有剩余引用。

实际上，一旦 `Complete()` 返回，job 及其所有依赖项就保证已完成执行并已从队列中删除。

另请注意：

- 在已完成的 job 的句柄上调用 `Complete()` 不会执行任何操作，也不会引发任何错误。
- 与调度一样，jobs 只能从主线程完成，而不能从其他 jobs 内部完成。
- 尽管 job 可以在计划后立即完成，但通常最好推迟完成 job，直到实际需要完成工作的最晚可能时刻。一般来说，每个 job 的调度与其完成之间的间隔越长，主线程和工作线程花费不必要的空闲时间的可能性就越小。

<br>

## jobs 中的数据访问

在绝大多数情况下：

- job 不应执行 I/O。
- job 不应访问托管对象。
- job 应该只访问只读的静态字段。

调度 job 会创建该结构的私有副本，该副本仅对正在运行的 job 可见。因此，对 job 中字段的任何修改仅在 job 中可见。但是，由于非托管集合结构将其*内容*存储在外部而不是存储在结构本身中，因此对集合字段内容的修改将在 job 外部可见。

<br>

## jobs 内的分配

传递给 job 的集​​合必须使用 `Allocator.Persistent`、`Allocator.TempJob` 或其他线程安全分配器进行分配。

使用 `Allocator.Temp` 分配的集合*不能*传递到 jobs 中。然而，job 的每个线程都有自己的临时分配器，因此 `Allocator.Temp` 可以安全地在*jobs 内使用。job 中的所有临时分配将在 job 末尾自动处置。

使用 `Allocator.TempJob` 进行的分配必须手动处置。如果启用了处置安全检查，则当使用 `Allocator.TempJob` 进行的任何分配在分配后 4 帧内未处置时，将引发异常。


<br/>


# Job 安全检查

对于访问相同数据的任何两个 jobs，通常不希望它们的执行重叠或执行顺序不确定。例如，如果两个 jobs 读写一个原生数组的内容，我们应该确保两个 jobs 之一在另一个开始之前完成执行。否则，当 job 修改数组时，该更改可能会干扰另一个 job 的结果，具体取决于 job 在另一个之前运行的情况以及它们的执行是否重叠。

因此，当两个 jobs 之间存在此类数据冲突时，您应该：

- Schedule 并在安排另一项之前完成一项 job...
- ...或 schedule 一个 job 作为另一个的依赖项。

当您调用 `Schedule()` 时，如果 job 安全检查（如果启用）检测到潜在的竞争条件，它们将引发异常。例如，如果您首先使用本机数组的 schedule 一个 job，然后使用 schedule 另一个使用相同本机数组但不依赖于第一个 job 的 job，则会引发异常。

作为一种特殊情况，如果两个 jobs 都只“读取”数据，那么两个 jobs 访问相同的数据总是安全的。因为 job 都不修改数据，所以不会互相干扰。我们可以通过使用 [`[ReadOnly]`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnlyAttribute.html) 属性标记结构字段来指示只能在 job 中读取本机数组或集合。如果两个 jobs 共享的所有本机数组或集合在两个 jobs 中都标记为 `[ReadOnly]`，则 job 安全检查不会认为两个 jobs 发生冲突。

在某些情况下，您可能希望对 job 中使用的特定本机数组或集合完全禁用 job 安全检查。这可以通过使用 [`[NativeDisableContainerSafetyRestriction]`](https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute.html) 属性对其进行标记来完成。只要确保您没有创建竞争条件即可！

当任何当前计划的 jobs 使用本机集合时，如果您尝试在主线程上读取或修改该本机集合，安全检查将引发异常。作为一种特殊情况，如果在调度的 jobs 中标记为 `[ReadOnly]`，主线程可以从本机集合“读取”。

<br/>


# 并行 Jobs

要将处理数组或列表的工作拆分到多个线程中，我们可以使用 [`IJobParallelFor`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobParallelFor.html) 接口定义 job：

```csharp
[BurstCompile]
public struct SquareNumbersJob : IJobParallelFor
{
    public NativeArray<int> Nums;

    // Each Execute call processes only a single index.
    public void Execute(int index)
    {
        Nums[index] *= Nums[index];
    }
}
```

当我们 schedule job 时，我们指定索引计数和批量大小：

```csharp
// ... scheduling the job
var job = new SquareNumbersJob { Nums = myArray };
JobHandle handle = job.Schedule(
        myArray.Length,    // count
        100);              // batch size
```

当 job 运行时，其 `Execute()` 将被调用 *`count`* 次，所有从 0 到 *count* 的值都会传递给 `index`。

job 的索引被分成由批次大小确定的批次，然后工作线程可以从队列中获取这些批次。实际上，单独的批次可以在单独的线程上同时处理，但单个批次的所有索引将在单个线程中一起处理。

在此示例中，如果数组长度为 250，则 job 将分为三批：第一批覆盖索引 0 到 99；第二批覆盖索引 0 到 99。第二覆盖索引 100 到 199；最后一个批次覆盖其余部分，索引 200 到 249。由于 job 被分为三个批次，因此最多将在三个工作线程中进行有效处理。如果我们想将 job 拆分到更多线程，我们必须选择较小的批量大小。

| &#x1F4DD; NOTE |
| :- |
| 选择好的批量大小并不是一门精确的科学！在极端情况下，我们可以选择批量大小为 1，从而将每个单独的索引拆分为自己的批量，但请记住，拥有太多小批量可能会产生大量的 job system 开销。一般来说，您应该选择一个看起来不太大但也不太小的批量大小，然后尝试找到对于每个特定 job 来说似乎最佳的大小。 |

当处理一个批次时，它应该只访问自己批次的数组或列表索引。为了强制执行此操作，如果我们使用除 `index` 参数之外的任何值对数组或列表进行索引，安全检查将引发异常：

```csharp
[BurstCompile]
public struct MyJob : IJobParallelFor
{
    public NativeArray<int> Nums;

    public void Execute(int index)
    {
        // The expression Nums[0] triggers a safety check exception!
        Nums[index] = Nums[0];
    }
}
```

此限制不适用于标有 `[ReadOnly]` 属性的数组和列表字段。对于需要在 job 中写入的数组或列表字段，可以通过将字段标记为 `[NativeDisableParallelForRestriction]` 来禁用限制。只是要小心，不要创造竞争条件！