# Entities 教程：踢球

[视频：Entities“踢球”教程演练](https://youtu.be/P6_3L7RTcm0)（55 分钟）

本教程项目演示了基本的 *Unity.Entities* 用法。

![](常用/图片/end_result.gif)

- 障碍物（灰色圆柱体）和玩家（橙色胶囊）在场地上生成。
- 方向输入使玩家移动，但玩家无法穿过障碍物。
- 按回车键会在每个玩家胶囊的位置生成一个黄色球。
- 击球空间会踢出靠近球员的球。
- 随着时间的推移，球会从障碍物上弹起并失去动量。

下面的文本描述了每个步骤的总体思路，但强烈建议您研究代码并阅读注释。

<br>

## **第 1 步：** 障碍物生成。

- [`ConfigAuthoring.cs `](./Step%201/ConfigAuthoring.cs)
- [`ObstacleAuthoring.cs `](./Step%201/ObstacleAuthoring.cs)
- [`ObstacleSpawnerSystem.cs `](./Step%201/ObstacleSpawnerSystem.cs)

在步骤 1 中，主 scene 包含嵌入的“子 scene”，表示主 scene 中的 `SubScene` MonoBehaviour 引用的单独的 scene 资产。

- 在构建时，子 scene 中的 GameObjects 被 [烘焙](../../Docs/baking.md) 到序列化的 entities 中。
- 当主 scene 在运行时加载时，序列化的 entities 会与主 scene 的 GameObjects 一起加载。
- **子 scene 的 GameObjects 是运行时加载的 NOT！**

在步骤 1 scene 中，子 scene 包含用于地面“Plane”的渲染平面 GameObject 和名为“Config”的未渲染 GameObject，其具有 `ConfigAuthoring` MonoBehaviour。再次强调，子 scene 的 GameObjects 将不会在运行时加载：您在播放模式下看到的渲染平面是 entity，而不是 GameObject。

![](常用/图片/initial_scene.png)

在 baking 中，`ConfigAuthoring` 在烘焙后的 entity 中添加了一个 `Config` component。这个 `Config` component 存储各种游戏参数，例如生成的障碍物和玩家数量，以及我们将在运行时实例化的 entity prefab。

在游戏模式开始时，`ObstacleSpawnerSystem` 创建障碍物 prefab 的实例。由于生成只应发生一次，因此 system 会禁用自身以停止后续更新。

![](常用/图片/step1_result.png)

<br>

## **第 2 步：** 玩家生成和移动。

- [`PlayerAuthoring.cs `](./Step%202/PlayerAuthoring.cs)
- [`PlayerSpawnerSystem.cs `](./Step%202/PlayerSpawnerSystem.cs)
- [`PlayerMovementSystem.cs `](./Step%202/PlayerMovementSystem.cs)

障碍物生成后，`PlayerSpawnerSystem` 会创建玩家 prefab 的实例，每个障碍物旁边都有一个实例。

每一帧，`PlayerMovementSystem` 都会读取玩家的方向输入并相应地移动所有玩家。使用简单的半径碰撞检查，可以防止玩家穿透障碍物。

![](常用/图片/step2_result.png)

<br>

## **第 3 步：** 球的生成、移动和踢球。

- [`BallAuthoring.cs `](./Step%203/BallAuthoring.cs)
- [`BallSpawnerSystem.cs `](./Step%203/BallSpawnerSystem.cs)
- [`BallMovementSystem.cs `](./Step%203/BallMovementSystem.cs)
- [`BallKickingSystem.cs `](./Step%203/BallMovementSystem.cs)

当用户按下 Enter 键时，`BallSpawnerSystem` 在每个玩家的位置生成一个具有初始速度的球。

每一帧，`BallMovementSystem` 都会根据每个球的当前速度移动每个球，如果球撞到障碍物则使速度偏转，并随着时间的推移减小速度。

当用户按下空格键时，`BallKickingSystem` 会对距玩家短距离内的所有球应用冲击速度。

![](常用/图片/step3_result.png)

<br>

## **步骤 4：** 通过将工作移至并行 jobs 来提高性能。

- [`NewPlayerMovementSystem.cs `](./Step%204/NewPLayerMovementSystem.cs)
- [`NewBallKickingSystem.cs `](./Step%204/NewBallKickingSystem.cs)
- [`NewBallMovementSystem.cs `](./Step%204/NewBallMovementSystem.cs)

这些新的 systems 复制了原始的功能，但它们在 jobs 中而不是在主线程上完成繁重的工作。

请注意，`BallMovementJob` 和 `BallKickingJob` 都具有相同的 query，因此如果将它们组合成一个 job 可能会更优化，但这只是猜测。在实践中，您应该仔细分析，看看合并它们是否实际上更快！

另请注意，当我们 schedule `IJobEntity` jobs 时，我们写：

```csharp
myJob.ScheduleParallel();
```

...但是这些 schedule 调用由 source-gen 修改以传递并返回 job 句柄：

```csharp
state.Dependency = myJob.ScheduleParallel(state.Dependency);   // result of source-gen
```

这是正确处理 entity job 依赖项所必需的，如[此处](../../../Docs/entities-jobs.md#systemstatedependency) 所述。

<br>
<hr>

*END OF TUTORIAL*

<br>


