# HelloCube 示例

[视频：Entities“HelloCube”示例演练](https://youtu.be/32TLgtA9yUM)（30 分钟）

*这些非常简单的示例演示了 Entities API 的基本元素。*

## MainThread 示例

该示例旋转两个立方体，一个父立方体和一个子立方体。

较大的立方体有一个 `RotationSpeedAuthoring` MonoBehavior，它将 `RotationSpeed` IComponentData 添加到 baking 中的 entity。在运行时，`RotationSpeedSystem` 旋转具有 `RotationSpeed` component 的所有 entities（在本例中，仅是单父立方体）。

## IJobEntity 示例

此示例与“MainThread”相同，但 `RotationSpeedSystem` 现在使用 job（`IJobEntity`）来旋转立方体，而不是直接在主线程上这样做。另外，通过设置立方体的 `PostTransformMatrix` component，立方体的 Y 轴比例在 1 和 -1 之间波动。

## Prefabs 示例

该示例包含单个未渲染的 entity 以及引用立方体 prefab 的 `Spawner` component。

在运行时，`SpawnSystem` 生成立方体 prefab 的许多实例，并将这些实例放置在随机位置。`RotationSpeedSystem` 使立方体旋转和下落。当立方体落在 y 坐标 0 以下时，`FallAndDestroySystem` 会销毁立方体。一旦所有立方体都被销毁，`SpawnSystem` 将生成更多立方体。

## IJobChunk 示例

此示例类似于“IJobEntity”，但它使用 `IJobChunk` 而不是 `IJobEntity`。与 `IJobEntity` 相比，`IJobChunk` 需要更多样板，但在某些用例中提供了更明确的控制。

## 重新父子化示例

每隔一段时间，较小的立方体就会与大的旋转立方体建立父子关系或取消父子关系。

## EnableableComponents 示例

旋转立方体的 `EnabelableComponent` 状态定期切换，导致它们开始和停止旋转。

## GameObjectSync 示例

此示例包含带有旋转变换的 entity，但 entity 本身未渲染。相反，entity 将其变换与渲染的 GameObject 同步。UI 复选框可打开和关闭旋转。

## CrossQuery 示例

此示例演示如何比较两个单独查询的 entities。运行时：

- `SpawnSystem` 创建两组盒子：10 个白盒子和 10 个黑盒子。
- `MoveSystem` 从相反的方向开始来回移动白盒和黑盒。
- `CollisionSystem` 当一个盒子与另一个盒子相交时会改变它的颜色：白色盒子变成粉色，黑色盒子变成绿色。

`CollisionSystem` 以两种方式之一执行相交测试，由代码中的 `#if` 切换：

- `#if true` 解决方案复制 entity id 并将框转换为数组，然后循环遍历每个框（使用 [`SystemAPI.Query`]()）以将其位置与每个其他框进行比较。
- `#if false` 解决方案在 job 中完成工作，并通过将 entity chunks 传递到 job 来避免复制框。

## RandomSpawn 示例

运行时：

- `RandomSpawn` system 定期生成 200 个盒子，并将它们放置在圆边缘的随机点上。
- `MovementSystem` 向下移动盒子，并在 y 坐标小于零时销毁它们。

由于框位于平行的 job 中，因此随机定位框需要为每个框提供唯一的随机种子值。

## FirstPersonController 示例

一个非常简单的第一人称控制器，演示基本的输入处理以及与 GameObject（相机）的协调。

## FixedTimestep 示例

此示例演示如何以固定速率更新 systems，类似于 `MonoBehaviour.FixedUpdate` 方法。

## CustomTransforms

专门用于 2D 而不是 3D 的简单自定义变换 system。

## StateChange 示例

这些示例演示了表达状态变化的不同方式。平面上生成了大量立方体，单击可在半径内的所有立方体之间切换两种状态：白色和静止；或红色且旋转。有四种解决方案：

- **启用 component**：立方体状态通过启用和禁用 component 来表示。
- **结构变化**：立方体状态通过添加和删除一个 component 来表示。
- **值变化**：立方体状态由 component 值表示。

## ClosestTarget 示例

此示例类似于 [jobs 教程](../Tutorials/Jobs/README.md)：搜索者（绿色立方体）和目标（红色立方体）各自在 2D 平面上以随机方向缓慢移动。从每个搜索器到最近的目标绘制一条白色调试线。

在 subscene 中，“模拟”GameObject 有一个“SettingsAuthoring”component，其“空间分区”值控制搜索者如何找到目标：

- `None`：暴力搜索选项。对于每个搜索者，`NoPartitioning` 循环遍历每个目标以找到最接近搜索者的目标。
- `Simple`：此选项使用[jobs 教程的步骤 4](../Tutorials/Jobs/README.md#step-4---solution-with-a-parallel-job-and-a-marter-algorithm) 中演示的相同空间分区。
- `KD Tree`：此选项使用 [k-d 树](https://en.wikipedia.org/wiki/K-d_tree)（*k* 维空间中的点树）进行空间分区。

空间分区允许每个搜索者找到最接近的目标，而不必考虑*每个*目标。即使是简单的分区解决方案，其规模也比根本不使用分区要好得多，并且 k-d 树解决方案在非常大的规模下表现得更好。例如，在我的 8 核 CPU 上有 30,000 个搜索器和 30,000 个目标时，使用 k-d 树解决方案的 `TargetingSystem` 的速度比简单解决方案快大约两倍，比无分区解决方案快一百倍以上。


