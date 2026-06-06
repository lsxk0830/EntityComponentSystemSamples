# HelloNetcode 引导程序和前端示例

Netcode package 提供了一种通过使用自定义引导程序和自动连接器功能自动创建 client 和 server worlds 并将它们连接在一起（client 连接到 server）的方法。这适用于快速测试功能的简单情况。

还可以通过辅助方法手动执行所有操作，包括创建 worlds 和设置连接。

当直接打开任何 HelloNetcode 示例时（_Frontend_ scene 除外），将使用自动连接功能。

看

* [入门](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/getting-started.html) 指南中的 _ 建立连接 _ 部分
* [Client Server Worlds](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/client-server-worlds.html)
* [网络连接](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html)
* [多人游戏会话](https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual)
* [主机迁移](https://docs.unity3d.com/Packages/com.unity.netcode@1.10/manual/host-migration/host-migration.html)

## 示例描述

此示例显示了引导功能所需的修改，以启用自动连接功能并选择替代手动连接和 world 创建方法（此处称为 _Frontend_）。它分为两部分：

* 示例的 Bootstrap 文件夹包含自定义引导代码文件，它在不使用前端时启用自动连接，并且还会创建默认的 client 和 server worlds （与默认Netcode引导程序发生的情况类似）。
* Frontend 文件夹显示 UI，供您在运行时选择是否要托管游戏或加入游戏。它在运行时仅创建一个 client world（加入）或同时创建 client 和 server（主机），然后根据需要手动开始侦听或连接。

前端被放置在与引导程序集不同的单独程序集中，因为我们不希望将其包含在专用 server 构建中（定义了 UNITY_SERVER）。

前端允许您使用直接 IP/端口连接或使用会话功能。会话允许您设置一个会话 ID，然后任何人都可以通过中继或直接连接到该会话。它还允许您以简单的方式启用主机迁移支持。

这里有三个 scenes：

* _Frontend.unity_ 具有手动连接设置（连接或主机）和按需 world 创建（在运行时），并通过 scene 选择项目中的所有示例 scenes。
* 有一个配套的 _FrontendHUD.unity_ scene，它显示从前端打开 scene 后返回主菜单的 UI、连接状态的文本以及正在使用的会话 ID（如果有）。
* 还有一个 _HostMigrationHUD.unity_ scene 仅在启用主机迁移支持时加载。这显示了有关主机迁移的统计信息，例如迁移数据大小。
* _FrontendBootstrap_ scene 应首先放置在构建设置 scene 列表中，并为加载的第一个示例 scene 设置一些流程。当启动专用 server 时，它默认加载 Asteroids 示例，否则加载前端 scene。当在命令行上给出 scene 名称时，它会选择该名称。

## 笔记

> [!NOTE]
> 有两个额外的前端 scenes 用于展示如何在不使用会话的情况下手动设置中继和主机迁移支持。要了解有关这些的更多信息，请参阅 01b_RelaySupport 和 01d_HostMigration 说明。另请注意，一次只能在构建设置中启用一个前端，要在构建中尝试不同的前端，请在构建设置 scene 列表中选择它（并取消选择当前前端）。