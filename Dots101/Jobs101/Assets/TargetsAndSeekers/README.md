# Jobs 教程

[视频：Jobs 教程演练](https://youtu.be/oOgNg2gL2yw)（17 分钟）

本教程分为多个独立的步骤，每个步骤都建立在最后一个步骤的基础上。步骤 N 位于 `Assets/StepN` 目录中，*e.g.* 步骤 2 位于 `Assets/Step2` 目录中。

### 我们要解决的问题：

- 搜索者（蓝色立方体）和目标（红色立方体）各自在 2D 平面上沿随机方向缓慢移动。
- 从每个搜索器到最近的目标绘制一条白色调试线。

![每个蓝色导引头找到最近的红色目标](./Common/Images/find_target.gif)

<br>

# 步骤 1 - 没有 jobs 的解决方案

- 单例 `Spawner` MonoBehaviour 在 XZ 平面上的 500 x 500 区域内实例化 1000 个导引头（蓝色立方体）和 1000 个目标（红色立方体）。
- `Seeker` 和 `Target` MonoBehaviours 沿着随机矢量缓慢移动每个导引头和目标。
- `Spawner` 将目标的变换存储在静态数组中。
- `FindNearest` MonoBehaviour（附加到导引头 prefab）使用目标变换数组来查找最近的目标位置并绘制白色调试线。

## 结果

这里使用的寻找算法很简单：对于每个搜索者，我们循环遍历每个目标。

以下是具有 1000 个导引头和 1000 个目标的典型框架的概况：

![步骤 1 的配置文件](./Common/Images/step1_profile.png)

每个导引头需要大约 0.3 毫秒来更新，总共需要超过 330 毫秒。

<br>

# 步骤 2 - 使用单线程 job 的解决方案

- `Spawner` 将导引头和目标的变换存储在静态数组中。
- `FindNearest` MonoBehaviour 从导引头 prefab 移至“生成器”GameObject。
- `FindNearest` 将导引头和目标变换的 `localPosition` 值复制到 `float3` 的 `NativeArray` 中。
- `FindNearest` 调度并完成新的 `FindNearestJob`，它使用这些 NativeArray。

通过将艰苦的工作放入 job 中，我们可以将工作从主线程移动到工作线程，并且我们可以 Burst 编译代码。Jobs 和 Burst 编译的代码无法访问任何托管对象（包括 GameObjects 和 GameObject components），因此我们必须首先将 job 要处理的所有数据复制到非托管集合中，例如 `NativeArray` 的。

| &#x1F4DD; NOTE |
| :- |
| 严格来说，jobs 实际上*可以*访问托管对象，但这样做需要特别小心，通常不是一个好主意。此外，我们肯定希望 Burst 编译此 job，而 Burst 编译的代码严格“无法”访问托管对象。 |

虽然我们仍然可以使用 `Vector3` 和 `Mathf`，但我们将使用 `Unity.Mathematics` package 中的 `float3` 和 `math`，它具有特殊的优化挂钩 Burst。

那么我们的 `FindNearestJob` job 的定义方式如下：


```c#
public struct FindNearestJob : IJob
{
    // All the data which a job will access
    // must be included in its fields.
    public NativeArray<float3> TargetPositions;
    public NativeArray<float3> SeekerPositions;
    public NativeArray<float3> NearestTargetPositions;

    public void Execute()
    {
        // ... the code which the job will run goes here
    }
}
```

`FindNearest` 的 `Update()`：

1. 将导引头和目标变换复制到 `float3` 的 `NativeArray` 中。
1. 创建 `FindNearestJob` 的实例，并使用 `NativeArray` 初始化其字段。
1. 在 job 实例上调用 `Schedule()` 扩展方法。
1. 在 `Schedule()` 返回的 `JobHandle` 上调用 `Complete()`。
1. 使用 `NearestTargetPositions` 数组（由 job 填充）来绘制从每个导引器到其最近目标的调试线。

以下是实例化、调度和完成 job 的摘录：

```c#
// To schedule a job, we first create an instance and populate its fields.
FindNearestJob findJob = new FindNearestJob
{
    TargetPositions = TargetPositions,
    SeekerPositions = SeekerPositions,
    NearestTargetPositions = NearestTargetPositions,
};

// Schedule() puts the job instance on the job queue.
JobHandle findHandle = findJob.Schedule();

// The Complete() method will not return until the job
// represented by the handle has finished execution.
// In some cases, a job may have finished
// execution before Complete() is called on its handle.
// Either way, the Complete() call will only return
// once the job is done.
findHandle.Complete();
```

## 结果

以下是具有 1000 个导引头和 1000 个目标的典型框架的配置文件**没有 Burst 编译 job**：

![步骤 2 的配置文件（Burst 已禁用）](./Common/Images/step2_profile_no_burst.png)

~30ms 肯定比我们之前看到的 ~330ms 好，但接下来让我们通过在 job 结构体上添加 `[BurstCompile]` 属性来**启用 Burst 编译**。**还要确保在菜单栏中启用了 Burst 编译：**

![启用 Burst 编译](./Common/Images/enable_burst.png)。

这是我们通过 Burst 得到的结果：

![第 2 步的配置文件（启用 Burst）](./Common/Images/step2_profile.png)

在大约 1.5 毫秒时，我们完全在 60 fps 的 16.6 毫秒预算之内。

由于我们现在有这么多的空间，让我们尝试 10,000 个搜索者和 10,000 个目标：

![步骤 2 的配置文件（启用 Burst，10,000 个导引头和目标）](./Common/Images/step2_profile_10000.png)

导引头和目标增加 10 倍会导致 run 时间增加 70 倍，但考虑到每个导引头都会检查其与每个目标的距离，这是预期的。

| &#x1F4DD; NOTE |
| :- |
| 请注意，在这些配置文件中，主线程上的 jobs run。当我们在尚未从 job 队列中拉出的 `Complete()` 上调用 `Complete()` 时，可能会发生这种情况：因为主线程在等待 job 完成时会处于空闲状态，所以主线程本身可能会 run job。 |

<br>

# 步骤 3 - 使用并行 job 的解决方案

- `FindNearestJob` 现在实现 `IJobParallelFor` 而不是 ZXQLQNYXNA​​WTQHAZXQ。
- `FindNearest` 中的 `Schedule()` 调用现在采用两个 int 参数：*索引计数*和*批量大小*。

对于处理数组或列表的 job，通常可以通过将索引拆分为子范围来并行化工作。例如，数组的一半可以在一个线程上处理，而另一半则同时在另一个线程上处理。

我们可以使用 `IJobParallelFor` 方便地创建这样的 jobs，其 `Schedule()` 方法接受两个 int 参数：

- *索引计数*：正在处理的数组或列表的大小
- *批次大小*：子范围的大小，*a.k.a.* 批次

例如，如果 job 的索引计数为 100，其批次大小为 40，则 job 会分为三个批次：第一个覆盖索引 0 到 39；第二个覆盖索引 0 到 39。第二覆盖索引 40 至 79；第三个覆盖索引 80 到 99。

工作线程将这些批次单独从队列中拉出，因此单个 job 的批次可以在不同线程上同时处理。

`IJobParallelFor` 的 `Execute()` 方法采用索引参数，并为每个索引调用一次，从 0 到索引计数：

```c#
public struct FindNearestJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> TargetPositions;
    [ReadOnly] public NativeArray<float3> SeekerPositions;
    public NativeArray<float3> NearestTargetPositions;

    // Each Execute call processes only an individual index.
    public void Execute(int index)
    {
        // ...
    }
}
```

```c#
// This job processes every seeker, so the
// seeker array length is used as the index count.
// A batch size of 100 is semi-arbitrarily chosen here
// simply because it's not too big but not too small.
JobHandle findHandle = findJob.Schedule(SeekerPositions.Length, 100);
```

## 结果

具有 10,000 个导引头和 10,000 个目标的典型框架的轮廓：

![第 3 步的配置文件](./Common/Images/step3_profile.png)

由于工作分配到 16 个内核，CPU 总时间约为 260 毫秒，但从开始到结束不到 17 毫秒。

<br>

# 第 4 步 - 使用并行 job 和更智能的算法解决方案

- `FindNearest` MonoBehaviour 现在安排额外的 job 根据 X 坐标对目标位置数组进行排序。
- 由于目标位置数组已排序，`FindNearestJob` 不再需要详尽地考虑每个目标。

如果我们以某种方式组织数据，例如将目标排序到[四叉树](https://en.wikipedia.org/wiki/Quadtree) 或[*k*-d 树](https://en.wikipedia.org/wiki/K-d_tree)，我们可以获得巨大的性能提升。为了简单起见，我们将仅按目标的 X 坐标对目标进行排序，尽管我们也可以按 Z 坐标进行排序（选择是任意的）。然后可以通过以下步骤找到距离单个导引头最近的目标：

1. 使用最接近搜索者 X 坐标的 X 坐标对目标进行二分搜索。
2. 从该目标的索引开始，在数组中上下搜索二维距离较小的目标。
3. 当我们在数组中上下搜索时，一旦 X 轴距离超过到当前候选最近目标的二维距离，我们就会提前退出。

这里的关键见解是：

- 我们不需要检查任何 X 轴距离大于当前候选的二维距离的目标。
- 假设目标按照 X 坐标排序，如果一个目标的 X 轴距离太大，那么数组中所有目标到它一侧的 X 轴距离也一定太大。

因此，搜索不再需要考虑每个搜索者的每个目标。

## 使用 jobs 排序

我们可以编写自己的 job 来进行排序，但我们将调用 `NativeArray` 扩展方法 `SortJob()`，它返回 `SortJob` 结构。`SortJob` 的 `Schedule()` 方法调度*两个* jobs：`SegmentSort`（并行对数组的各个段进行排序）和 `SegmentSortMerge`（合并已排序的段）。（合并必须在单独的单线程 job 中完成，因为合并算法无法并行化。）

## Job 依赖项

在 `SegmentSort` job 本身完成执行之前，`SegmentSortMerge` job 不应开始执行，因此 `SegmentSort` job 成为 `SegmentSortMerge` 的“依赖项” job。通常，**工作线程在其所有依赖项完成执行之前不会执行 job。** Job 依赖项有效地允许我们在计划的 jobs 中指定顺序执行顺序。

我们的 `FindNearestJob` 需要等待排序完成，所以它必须依赖于排序 jobs。这是创建和调度 jobs 的代码：

```c#
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

jobs 的顺序是：`SegmentSort` -> `SegmentSortMerge` -> `FindNearestJob`。

如果我们忽略使排序 job 成为查找 job 的依赖项，则当我们尝试 schedule 查找 job 时，job 安全检查将引发异常。

## 结果

对于此解决方案，以下是具有 10,000 个导引头和 10,000 个目标的典型框架的概况：

![第 4 步的配置文件](./Common/Images/step4_profile.png)

现在，`FindNearestJob` 总共只需要大约 7.5 毫秒的 CPU 时间，从开始到结束大约需要 0.5 毫秒。

放大后，我们可以看到 `SegmentSort` 和 `SegmentSortMerge` jobs：

![第 4 步的配置文件（排序）](./Common/Images/step4_profile_sort.png)

`SegmentSort` 从开始到结束的时间不到 0.1 毫秒，单线程 `SegmentSortMerge` 大约需要 0.5 毫秒。与 `FindNearestJob` 的巨大改进相比，额外的排序步骤非常值得额外的成本。

现在大部分帧时间都被 GameObjects 的低效率所消耗，这可以通过用 entities 替换 GameObjects 来解决。







