# HelloNetcode 具有自动命令示例的 Spawn Player

可以为 server 上的每个 client 生成一个玩家。某些功能专门适用于此类 entity 的滞后补偿技术，例如 client 侧 prediction（但也可以应用于其他 entities）。有时称为 client 拥有的 entity。

通过使用 ghost authoring prefab 上切换的自动命令功能并利用 `IInputComponentData` 存储输入数据，可以更轻松地设置输入命令。只有一个 entity 可以向 server（通常是玩家角色）发送输入。

看

* [Entity 生成](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html)
* [命令流](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/command-stream.html)

## 要求

连接需要已经在游戏中。

* GoInGame

## 示例描述

ghost authoring component 对默认值进行了以下调整：

* `Has Owner` 切换。这仅使 client run prediction 位于其自己的播放器实例上，而不是 prefab 的远程播放器实例。
* `Support Auto Commands` 切换。这使得命令目标自动设置在连接上（指向 client 拥有的玩家 entity）。需要设置命令目标才能正确发送命令。

一旦在“游戏中”的 server 上检测到连接，但尚未拥有 `PlayerSpawned` component，就会生成一个玩家。然后，ghost snapshot 复制确保它也在所有 clients 上生成。

使用 `IInputComponentData` 设置输入。这确保它们自动设置为命令（通过代码生成），这些命令将收集在 client 上并放置在命令缓冲区中。然后，输入也可以在 server（或具有远程播放器 prediction 的其他播放器）上进行处理，无需任何额外的工作。

`IInputComponentData` 输入结构上设置的任何 `[GhostComponent]` 属性值也将应用于输入缓冲区，但此示例使用默认值。
