# HelloNetcode Thin Client 示例

精简的 client 是一个精简的 client，它仅将输入发送到 server，而不是 run 任何其他内容。它从 server 接收 ghost snapshot 更新，但丢弃它们（未完成处理）。因此它不会产生任何东西。

Thin clients 仅在编辑器中受支持，并且可以通过模拟额外的 clients 在编辑器中更轻松地开发多人游戏功能，因此您无需将它们作为独立版本构建和启动。您可以在 _ 多人游戏 _ 菜单中的 _ 游戏模式工具 _ 中设置所需的薄 clients 数量。

看

* [Client Server Worlds](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/client-server-worlds.html) 部分中的 _ 瘦客户端 _
* _ 使用 [Command Stream](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/command-stream.html) 部分中的 IInputComponentData_ 部分自动命令输入设置

## 要求

连接需要已经在游戏中并重新使用生成玩家示例，因此您不需要生成任何内容（只需设置新输入）

* GoInGame
* SpawnPlayer

## 示例描述

此示例演示了在使用 [__IInputComponentData__](xref:Unity.NetCode.IInputComponentData) component 进行输入处理时，如何将精简 client 随机输入生成添加到项目中。

要实现瘦 client 的输入处理，您需要手动创建一个虚拟播放器 entity 来包含输入逻辑。虚拟 entity 需要具有最低限度的功能才能正常工作，即输入 component/缓冲区，并具有命令目标和 ghost 所有者 components 为瘦 clients 自己的播放器配置（示例中的 `CreateThinClientPlayer` 函数）。

在此示例中，随机输入只是移动玩家并使其定期跳跃。它不知道其他玩家或其周围环境。如何生成随机输入当然取决于正在开发的游戏。您只需记住，您没有可用的最新游戏数据，因为 ghost snapshots 未被处理，因此不能依赖于该状态。
