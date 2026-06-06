# HelloNetcode 预生成 ghosts 示例

预生成的 ghost 只是一个 ghost，当Netcode游戏开始时，它已经设置并联网。每个 ghost 实例的数据包含在 scene 资产中。此示例演示了如何设置预生成的 ghosts。

看

* [Ghost snapshot](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html) 文档中的 _Prespawned Ghosts_ 部分。

## 要求

预生成的 ghosts 在您进入游戏时进行配置，因此在连接上出现 `NetworkStreamInGame` component 之前不会发生任何情况。因此，该示例需要：

* `GoInGame`

## 示例描述

预生成的 ghost 必须是 prefab 的实例，并放置在 `SubScene` 中。在示例 scene 中，我们有 3 个 ghosts 的变体，配置如下：

`PrespawnBarrel`

* 包含两个 ghost components，一个带有 ghost 字段，另一个是缓冲区。两者都可以在 scene 实例中调整预配置值或值（桶 prefab 将 ghost 字段值设置为 1，但 scene 将其更改为 10000）。

 `PrespawnBarrelWithNestedChild`

* 类似于 _PrespawnBarrel_，但具有 prefab，它是另一个 prefab (_PrespawnBarrelChild_) 的实例，并嵌套在主要资产 prefab 内。其中包含 ghost 字段。
* 嵌套子 prefab 不能拥有自己的 ghost authoring component，只有父级可以。

`PrespawnBarrelWithSceneAdditions`

* 是 _PrespawnBarrelWithNestedChild_ 的实例，但对 scene 中的层次结构进行了更改。
* 具有子 prefab 的另一个实例，但添加在 scene 本身中。
* 拥有 scene 资产（不是 prefab 实例）。这是有效的，因为它是正确的 ghost prefab 上的子项。这还包含 ghost 字段。

还有一个在 _PrespawnData_ 上运行的 system 运动，所有桶都显示 snapshot 数据正在应用，并且一切都已正确连接。

这里的所有 ghosts 都配置为使用动态优化模式（默认），这意味着它们预计会经常更新。请参阅 ghost 优化示例，了解何时使用静态优化模式（ghosts 很少移动）有意义以及它会产生什么差异。

## NOTE/TODO

* 嵌套子 ghosts 始终可见，但 entity scene (subscene) 中的根/主桶实例仅在 entity 时在 scene 视图中可见 scene 已打开（只要在 DOTS 菜单 _ 转换设置 _ 中选择 _Scene 视图中的创作状态 _）
