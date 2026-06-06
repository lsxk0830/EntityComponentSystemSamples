# HelloNetcode Relay Server 示例

该示例展示了继电器 server 集成。

看

* [Relay server 文档](https://docs.unity.com/relay/introduction.html)
* [Relay server 产品页面](https://unity.com/products/relay)

## 要求

* 为项目设置中继支持。通过上面的文档链接，按照“**开始使用 Relay**”的说明进行操作。

## 示例描述

此示例连接到前端示例 scene 中的复选按钮。通过切换此按钮，网络设置将利用中继 server 服务来允许 clients 连接到通过中继 server 服务托管的 servers。

启用 **Relay 支持**后，单击 **启动 Client & Server** 将启动启用中继支持的 server。然后 client 将通过中继服务连接到托管的 server。
**加入现有游戏** 用于将 client 连接到 server

在 Relay Server 复选框旁边可以看到状态消息。如果在中继服务的启动和初始化过程中发现任何错误，将显示失败消息，并且控制台日志将包含详细信息。

切换中继支持时，地址和端口输入字段将替换为单个输入字段。您可以在此处输入加入代码，该代码是通过中继服务连接托管 server 所需的信息。
NetCodeSetup/RelayHUD.cs 内部是复选框行为的条目。加入代码将显示在托管游戏的左上角。

> ＃＃ 笔记
> 在播放模式下，您可能会注意到示例 scene 的显示时间比未启用中继支持的情况要长。该延迟是由继电器 server 的往返时间引起的。

## 设计决策
该示例允许选择两个不同的选项来测试中继连接：
1. 使用中继进行传入连接的自托管，但使用 IPC 将 client 连接到本地 server 连接。
2. 通过使用中继将 client 连接到本地 server 连接，模拟连接到远程 server 的“client”。

前端菜单有一个特定的工具来选择所需的模式。

### 网络构建限制

使用 Web 构建运行示例时，启动 client/server 按钮将被禁用，直到检查了继电器的使用。

在 `NetcodeSetup/RelayHUD.cs` 中，client 连接端点设置为中继 server，必须按照本地 client 的描述进行更改。
