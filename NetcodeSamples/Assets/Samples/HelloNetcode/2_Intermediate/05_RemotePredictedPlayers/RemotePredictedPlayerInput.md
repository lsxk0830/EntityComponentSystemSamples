# HelloNetcode 远程播放器输入 Prediction

玩家输入可以是基于其输入缓冲区中先前看到的值的 predicted。此示例与之前的 _Spawn Player_ 示例完全相同，但经过调整以启用远程玩家输入 prediction 功能。

看

* [命令流](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/command-stream.html)
* [Ghost snapshots](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html) 中的 _ 创作动态缓冲区序列化 _ 部分

## 要求

连接需要已经在游戏中，并且来自玩家生成示例的脚本被重新使用。

* GoInGame
* SpawnPlayer

## 示例描述

输入结构和播放器 prefab 对于远程播放器 prediction 进行了更改，但除此之外，这与 _Spawn Player_ 是相同的示例。在 prefab 上我们有以下更改：

* `Supported Ghost Modes` 切换。更改为 _ 预测 _，因为所有玩家 ghosts 现在仅在 prediction 模式下运行，并且我们不会生成 interpolated 玩家。

输入是使用 `IInputComponentData` 设置的，如 _Spawn Player_ 中所示，但现在每个变量都具有 `[GhostField]` 属性集，这将使输入缓冲区同步到所有非所有者玩家。ghost component 属性不需要进一步调整，因为默认值是为此场景配置的。现在还添加了 `[GhostComponent]` 属性，该属性指定输入结构应仅出现在 predicted ghost prefab （_AllPredicted_ prefab 类型）上。

`RemotePredictedPlayerAutoCommands.cs` 脚本是 _Spawn Player_ 自动命令脚本的副本，只不过它被配置为在 `RemotePredictedPlayerInput` 输入结构上工作。

## 笔记

此示例中的玩家可能看起来与上一个示例中的非 predicted 玩家几乎相同。为了确保输入实际上同步，您可以使用 DOTS Hierarchy 窗口查看生成的远程播放器上的输入缓冲区。
