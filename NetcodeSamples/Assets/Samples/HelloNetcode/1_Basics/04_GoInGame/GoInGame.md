# HelloNetcode 进入游戏示例

进入游戏意味着启用 ghost snapshot 同步。在完成此操作之前，client 需要准备好从 server 接收 snapshots（例如加载在 server 上运行的适当级别）。必须在 server 与 client 的连接以及他自己上完成此操作。

看

* [入门](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/getting-started.html) 指南中的 _ 将其结合在一起 _ 部分
* [网络连接](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html)
* [Ghost snapshot](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html) 文档中的 _Prespawned Ghosts_ 部分。

## 要求

需要建立连接。

* 联系

## 示例描述

这里，system 只是监视新的未初始化连接，并在看到它们时立即将 `NetworkStreamInGame` component 添加到它们（并标记为已初始化）。
