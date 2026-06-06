### Unity Netcode for Entities 示例

*有关更多 Netcode 和 DOTS 入门材料，请参阅[此仓库主页](../README.md)。*

Netcode for Entities package 提供了实现所需的多人游戏功能
基于 [entities]((https://docs.unity3d.com/Packages/com.unity.entities@latest)) 的多人游戏中的 world 同步。它使用传输 package
对于套接字级功能，Unity Physics 用于网络物理模拟，记录 package 用于数据包转储日志。Netcode for Entities 的主要功能包括：

* Server 权威同步模型。
* RPC 支持，对于控制流或网络事件很有用。
* Client / server world 引导，因此您可以清晰地分离逻辑，并且您可以在单个进程中使用 run 和 server 与多个 clients，就像测试时的编辑器一样。
* 默认情况下，将 entities 与 interpolation 和 client 端 prediction 同步工作。
* 网络流量调试工具。
* GameObject 转换流程支持，因此您可以使用混合模型将多人游戏添加到基于 GameObject/MonoBehaviour 的项目。

[Netcode for Entities 手册](https://docs.unity3d.com/Packages/com.unity.netcode@latest)

[实体论坛 Netcode](https://forum.unity.com/forums/dots-netcode.425/)

### 示例

#### NetCube
具有 Netcode for Entities Package 基本功能的小示例，这是手册中的 __ 入门 __ 指南中使用的示例。

#### HelloNetcode
这是一套旨在小而简单并单独显示功能的示例。随后的示例会重复使用早期的示例，因此可以更轻松地准确查看所显示的内容，并且不需要重复类似的例程（例如连接、进入游戏等）。通过这种方式，示例也可以相互构建并变得更加复杂。根据复杂程度以及正常项目中所显示内容的需要程度，它们分为基础、中级和高级区域。

#### 小行星
一款具有 Netcode for Entities Package 功能的小游戏。

#### PredictionSwitching
使用基于 Unity Physics 的 predicted 物理的示例。该示例预测所有靠近玩家的物体，但不预测远处的物体。球体的颜色将发生变化，以指示它们是 predicted 还是 interpolated。

#### PlayerList
该示例展示了如何使用 RPC 功能维护已连接玩家的列表。