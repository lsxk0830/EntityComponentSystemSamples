# HelloNetcode RPC 示例

远程过程调用（RPC）意味着在远程端点上执行函数。在Netcode package 中，这涉及设置有效负载 component 并将其作为消息发送到 client/server 连接，在那里它们可以被处理/处理。

看

* [RPCs](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/rpcs.html)

## 要求

只需要建立一个连接即可发送。

* 联系

## 示例描述

此示例旨在显示您可以在 clients 和 servers 上使用Netcode package 发送的每种类型的 RPC 消息。它有一个基于 GameObject 的 UI，它呈现一个简单的聊天窗口，以在发送聊天和用户消息时演示简单的 RPC 例程：

* 当 client 连接 server 时，RPC 会将 client 作为新用户广播到现有连接（包括新的 clients 连接）。
* 现有用户列表也仅发送到该新连接（目标 RPC）。
* Clients 仅向 server 发送聊天消息（使用广播类型作为目标只能是 server）。
* 当 server 收到聊天消息时，他 RPC 会将其广播到所有连接，包括发送者，然后发送者将在聊天窗口中显示自己的消息。
