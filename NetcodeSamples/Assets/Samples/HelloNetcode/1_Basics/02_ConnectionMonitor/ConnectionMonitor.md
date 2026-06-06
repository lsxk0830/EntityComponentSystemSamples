# HelloNetcode 连接监视器示例

一旦 server 开始监听或 client 开始连接，就会创建连接 entity。它最初会获得 `NetworkStreamConnection` component，然后根据其配置方式获得其他信息。可以查询它以获取连接的状态（请参阅 `ConnectionState`）。

连接设置为较短的自定义断开连接超时，因此可以快速测试超时。

看

* [Entities 列表](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/entities-list.html) 中的 _Connection_ 部分显示了 entity 可以拥有的所有 components 网络连接。
* [网络连接](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html)

## 示例描述

此示例演示如何通过查询连接 components 来检测各种事件。使用反映连接流的 `ConnectionState` component 创建连接 entity

* 已断开连接
* 正在连接
* 握手
* 已连接

要在驱动程序上设置断开连接超时，需要设置自定义/手动驱动程序，以便可以将不同的网络参数传递给它。需要在引导程序中设置自定义驱动程序构造函数，以便尽早替换默认驱动程序。这是在 _NetCodeBootstrapExtension.cs_ 中完成的，但它被文件顶部的 _ENABLE_NETCODE_SAMPLE_TIMEOUT_ 定义禁用，因为启用它意味着它在整个项目中全局强制执行。要启用它，只需取消注释定义即可。

## 笔记

尝试使用玩家模式工具中添加的一根薄 client 来查看断开连接时会传递哪些消息。尝试使用此示例进行独立构建（可以从前端示例列表中选取），然后在测试以查看超时事件时终止独立进程。

UI 的设置并不是为了执行任何花哨的操作，而只是演示断开连接时的连接事件。

请注意标有 `ServerWorld`、`ClientWorld` 和 `ThinClientWorld` 的按钮下方的说明。这些描述指示了存储给定连接的特定 world（连接的名称是我们可以断开连接的号码）。例如，如果我们使用单个 ThinClient 在编辑器中测试此示例，您将观察到此连接出现两次：一次在 `ThinClientWorld` 中，一次在 `ServerWorld` 中。发生这种重复是因为这两个 worlds 存储相同的连接。在另一种情况下，如果 server 在编辑器中可操作，并且我们从不同的构建连接到它，您会注意到编辑器中存在 `ServerWorld` 的连接，而构建中存在 `ClientWorld` 的连接。
