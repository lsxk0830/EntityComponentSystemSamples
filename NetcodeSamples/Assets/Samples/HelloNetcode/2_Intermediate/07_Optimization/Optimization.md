# HelloNetcode 优化示例

此示例展示了 ghosts 优化模式（`Static` 与 `Dynamic`）。

* [优化模式的 GhostAuthoringInspector](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html)
* [有关此优化的进一步阅读](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/optimizations.html)

## 要求

* GoInGame
* 重要性

## 示例描述

当进入播放模式时，此示例会在 scene 中生成许多 **Barrel.prefab** ghosts。
通过使用这个密集的 scene，我们证明了通过启用 `Static Optimization Mode` 我们可以实现减少
序列化期间的 CPU 时间，以及通过网络发送的较小的 package 大小。

可以通过打开 subscene 并更改 `BarrelSetup` 的检查器上的参数来更改生成的桶数量：
- **圆圈数量**：可用于在进入游戏模式时添加额外的桶圈。
- **间距**：更改桶之间的距离以获得更好的可见性。
- **启用问题**：确保进入播放模式时启用 `StaticProblemSystem`。本指南稍后对此进行描述。

桶是由 server 生成的，不包含任何物理特性。
木桶不会被任何 system 移动，因此，持续为它们发送 snapshot 更新是没有意义的。
这就是静态优化的设计用途。

右键单击检查器中的 **Barrel** prefab 参考，然后选择 **属性**。
在检查器窗口中向下滚动到 **Ghost Authoring Component**。
您将看到**优化模式**设置为**动态**。
进入游戏模式并查看 scene 中生成的桶。

> [!NOTE]
> 您现在可以忽略 `BarrelWithoutImportance` 字段。
> 它用于 [09_Importance](../09_Importance/Importance.md) 示例。

## Package 尺寸

使用菜单项 `Multiplayer > Open NetDbg` 打开网络调试工具。
这将打开您的浏览器，您可以在其中连接到正在运行的游戏会话。
> [!NOTE]
> 我们看到桶如何影响 client 和 server 上的 package 尺寸。

退出播放模式并将**动态优化模式**更改为**静态**。
现在进入播放模式并重新连接网络调试工具。
优化确定桶变换的增量为零，
因此不会继续通过网络同步 `GhostField` 的不变值。

## CPU 时间

要记住的一件事是，如果任何 system 或 job 有可能修改 entity 上的同步 components，则Netcode同步必须序列化 component 来计算增量。
如果没有更改，我们将丢弃结果并浪费 CPU 序列化 components 所花费的时间。

当查看 CPU 时，我们使用的是 Unity Profiler。为了让探查器显示 job 线程的计时，我们必须启用菜单项 `Jobs > Use Job Threads`。还要记住选择 `Timeline` 视图。

退出播放模式并在 `BarrelSetup` 的检查器上选中 **启用静态优化问题**。当我们进入播放模式时，这将使 `StaticProblemSystem` 保持运行。
job 所做的就是引用 scene 中的所有 `Translation` components。它在 job 中没有执行任何操作，因此桶的变换保持不变。
打开 Unity Profiler 并进入播放模式。让模拟 run 几秒钟，然后按暂停。（按分析器中图表上的任意位置可暂停）

在此处查找 `ServerSimulationSystemGroup`，单击 `GhostSendSystem`，它将在下面的 job 列表中突出显示由该 system 生成的 jobs。
在 jobs 的列表中搜索，你会发现 `GhostSendSystem:SerializeJob`。展开 Worker 并注意 InterpolatedBarrel。
使用静态优化时很容易错过，可以写入 component 的 system，无论是否写入，始终都必须被序列化。
通过重写 system 以排除桶，或完全禁用 system，我们优化了 package 大小和 CPU 时间。

## 笔记

我们启用的优化越少，就越容易发现性能差异。Disabling burst will increase the timings for the samples significantly and make it easier to show the scaling that the optimization on its own can impact.

根据您的硬件，时间可能会有所不同。You can play around with the spawning parameters to increase/decrease the workload.
