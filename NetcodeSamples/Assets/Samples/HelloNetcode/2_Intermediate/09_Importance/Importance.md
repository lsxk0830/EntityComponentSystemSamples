# HelloNetcode 重要示例

此示例展示了如何设置内置的**距离重要性缩放**功能。

看

* [优化模式的 GhostAuthoringInspector](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/manual/ghost-snapshots.html#authoring-ghosts)
* [有关此优化的进一步阅读](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/manual/optimizations.html#importance-scaling)

## 要求

* GoInGame
* 优化，桶螺旋生成逻辑

## 示例描述

当进入播放模式时，此示例会生成许多在 scene 中旋转的桶。通过使用这个密集的 scene，我们演示了重要性行为是如何工作的。
可以通过打开 subscene 并更改 `BarrelSetup` 的检查器上的参数来更改生成的桶的数量。

- **圆圈数量**：可用于在进入游戏模式时添加额外的桶圈。
- **间距**：更改桶之间的距离以获得更好的可见性。

桶由 server 生成，不包含任何物理 components。
每个桶都在 server 上旋转，因此每个桶的变换将同步到 client（然后将对其进行渲染）。

在 scene 视图中查看桶时，可以看到斑点外边缘的一些桶的旋转不如朝向中心的桶那样平滑。
根据视图的不同，可能需要稍微缩小。

桶会通过 Entities chunks 中的 3D `int3` 平铺自动分组。I.e。它们被移动到空间位置 chunks。
然后，每个图块（i.e.chunks）将基于 `GhostDistanceImportance.Scale` 中的算法，根据到每个连接的（整个 chunk）距离进行更新。

重要性缩放的设置位于 `UpdateConnectionPositionSystem.OnUpdate` 中。
这里，正在创建一个 entity，有两个 components；即 `GhostDistanceData` 和 `GhostImportance`。
这两个 components 都是 `GhostDistanceImportance.Scale` 回调所期望的。
> ![NOTE]
> scene 中必须存在 `EnableImportance` 标志 component 单例才能触发此调用。重要的是，我们必须将此检查放在 `OnUpdate` 中，因为加载子 scene 在大多数情况下是异步操作。例外情况是在编辑器中手动加载子 scene 时。

最后一个重要性 component 是 `GhostConnectionPosition`，必须将其添加到 server 上的所有连接 entities（请参阅本示例中的 `UpdateConnectionPositionSystem.cs`）。
这个 component 的值表示应该被认为是 client 最重要的点的位置，
从而允许 `GhostSendSystem`（和 `GhostChunkSerializer`）确定重要性中心的能力，
为每个 client 构建每个 snapshot 时。

I.e。从这一点开始，基于距离的缩放将计算到每个图块中心的距离，
从而确定多人游戏的重要性以应用于此图块内的所有 entities (i.e.chunk)。

## 笔记

> [!NOTE]
> 您的 PC 规格将决定您的 PC 可以轻松生成多少个桶，因此您可能需要调整生成值以使这种重要性缩放更加清晰。
> 我们还建议在启用 Burst 的情况下对此进行测试，因为我们在这里尝试演示的效果与延迟有关，因此 CPU 限制可能会增加不需要的噪音。

根据模拟此示例的 PC，平滑更新的中心可能会减少/增加。通过从 subscene 中的 BarrelSetup component 生成越来越少的桶，它可以增加/减少生成的数量。

也可以更改图块大小配置以显示这些更改的影响。

可以创建自定义重要性等级实现，并切换所有 components 以适应其他用例。唯一发布的实现是方形图块的基于距离的重要性，因为这涵盖了大多数简单的用例。

在典型的游戏中，`GhostConnectionPosition` 会跟随角色四处移动，因此有必要使用此信息更新 `GhostConnectionPosition` component。这将增加最接近玩家的桶的行为将更频繁地更新。

## **BarrelWithoutImportance**
您可能注意到，红色的、稀疏生成的“BarrelWithoutImportance”桶**没有**被强制降低发送速率。
它们被明确地从重要性缩放子 system 中过滤掉。

要从重要性缩放中选择退出特定的 ghosts，您必须执行以下操作：
1. 将 `GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionSharedComponent` 设置为 `false`（或使用您自己的定制 system）。
这将禁用 `GhostDistancePartitioningSystem`s 默认行为，即将 `GhostDistancePartitionShared` 共享 component 添加到满足其条件的所有 ghost 实例。
2. 请勿将 `GhostDistancePartitionShared` 添加到这些 ghost 实例 (i.e。请注意 **`EnableImportanceScalingOnThisGhost` authoring 在 **BarrelWithoutImportance** 上的**缺失** prefab)。

此示例展示了如何设置内置的**距离重要性缩放**功能。
* [优化模式的 GhostAuthoringInspector](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/manual/ghost-snapshots.html#authoring-ghosts)
* [有关此优化的进一步阅读](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/manual/optimizations.html#importance-scaling)
当进入播放模式时，此示例会生成许多在 scene 中旋转的桶。通过使用这个密集的 scene，我们演示了重要性行为是如何工作的。
- **圆圈数量**：可用于在进入游戏模式时添加额外的桶圈。
桶由 server 生成，不包含任何物理 components。
每个桶都在 server 上旋转，因此每个桶的变换将同步到 client（然后将对其进行渲染）。
在 scene 视图中查看桶时，可以看到斑点外边缘的一些桶的旋转不如朝向中心的桶那样平滑。
根据视图的不同，可能需要稍微缩小。
桶会通过 Entities chunks 中的 3D `int3` 平铺自动分组。I.e。它们被移动到空间位置 chunks。
然后，每个图块（i.e.chunks）将基于 `GhostDistanceImportance.Scale` 中的算法，根据到每个连接的（整个 chunk）距离进行更新。
重要性缩放的设置位于 `UpdateConnectionPositionSystem.OnUpdate` 中。
这里，正在创建一个 entity，有两个 components；即 `GhostDistanceData` 和 `GhostImportance`。
这两个 components 都是 `GhostDistanceImportance.Scale` 回调所期望的。
这个 component 的值表示应该被认为是 client 最重要的点的位置，
从而允许 `GhostSendSystem`（和 `GhostChunkSerializer`）确定重要性中心的能力，
为每个 client 构建每个 snapshot 时。
I.e。从这一点开始，基于距离的缩放将计算到每个图块中心的距离，
从而确定多人游戏的重要性以应用于此图块内的所有 entities (i.e.chunk)。

## **BarrelWithoutImportance**
您可能注意到，红色的、稀疏生成的“BarrelWithoutImportance”桶**没有**被强制降低发送速率。
它们被明确地从重要性缩放子 system 中过滤掉。

要从重要性缩放中选择退出特定的 ghosts，您必须执行以下操作：
1. 将 `GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionSharedComponent` 设置为 `false`（或使用您自己的定制 system）。
这将禁用 `GhostDistancePartitioningSystem`s 默认行为，即将 `GhostDistancePartitionShared` 共享 component 添加到满足其条件的所有 ghost 实例。
2. 请勿将 `GhostDistancePartitionShared` 添加到这些 ghost 实例 (i.e。请注意 **`EnableImportanceScalingOnThisGhost` authoring 在 **BarrelWithoutImportance** 上的**缺失** prefab)。

## 重要性可视化工具
`Importance Visualizer` 是 PlaymodeTool 的抽屉，可帮助可视化重要性缩放结果。

支持的模式有：
* **PerEntityHeatmap** - 绘制每个 entity 热图，表示应用于整个 chunk 的重要性缩放。
  支持自定义重要性缩放结构。
* **PerEntitySpatialChunkStructure** - 为每个 chunk 分配随机颜色，并为该 chunk 中的所有 entities 绘制所述随机颜色，以及将它们相互链接的线条。
  支持自定义重要性缩放结构。
* **DrawGrid** - 绘制 GhostDistanceData` tiles used by the `GhostDistancePartitioningSystem` 的平面热图。
因此，仅在使用默认重要性缩放函数时才起作用。

