# Entities SceneLoading 示例

*NOTE：在这些示例中，在播放模式下打开和关闭 subscene 可能会出现 trigger 错误。您应该在 subscenes 关闭的情况下进入播放模式。*

## SceneLoading 示例

此示例演示如何加载/卸载 scenes。

## SceneState

此示例有一个 UI 用于加载 scenes 及其部分。

## SectionLoading 示例

此示例演示如何从 scene 加载/卸载部分。

## CrossSectionReferences 示例

此示例演示了一个部分中的 entities 如何能够或不能引用另一部分。

## SectionMetadata 示例

此示例演示如何将元数据添加到 entities 部分。

## StreamingVolume

此示例使用自定义卷来加载和卸载 subscenes 的 trigger。使用“WASD”键移动玩家胶囊。当播放器进入和离开卷时，subscenes 会被加载和卸载。

## SubsceneInstancing 示例

此示例演示如何使用 *PostLoadCommandBuffer* 和 *ProcessAfterLoad* system 多次实例化 scene。

## 完整示例

此示例使用基本的 LOD 解决方案根据玩家位置流式传输图块。使用“WASD”键移动播放器。瓷砖将根据其与玩家的距离来加载和卸载，距离玩家较近的瓷砖将切换到更高的 LOD 级别：

- 蓝色框 = 高 LOD（第 1 部分）
- 黄色框 = 中 LOD（第 2 部分）
- 红色框 = 低 LOD（第 3 部分）
- 接地四边形 = 所有 LOD 级别（第 0 部分）。

![完成示例 1!](./Common/Complete1.gif "Complete Sample 1")

- 地面四边形的 `TileLOD` component 定义加载每个 LOD 级别的距离。在 baking 期间，`TileLODBakingSystem` 将此信息存储在每个部分 entity 上的 `TileLODRange` component 中。在运行时，`TileLoadingSystem` 根据这些加载和卸载距离来加载和卸载图块。
- `TileDistanceSystem` 查找每个图块与最近的相关 entity 之间的距离。
- `TileLODSystem` 根据到最接近的相关 entity 的 LOD 距离加载和卸载部分。

请注意，我们可以有多个 `Relevant` entity。这里我们看到了拥有两个独立的 `Relevant` entities 的效果：


![完成示例 2!](./Common/Complete2.gif "Complete Sample 2")




