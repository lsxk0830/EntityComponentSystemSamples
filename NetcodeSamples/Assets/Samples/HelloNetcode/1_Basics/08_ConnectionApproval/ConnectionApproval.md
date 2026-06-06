# HelloNetcode 连接批准示例

在将其视为完全连接之前，可以验证连接是否允许连接到 server，以支持常见场景（例如后端用户身份验证、密码保护匹配等）。连接不会被 server 分配 `NetworkId`，直到 server 通过此示例代码中描述的“批准流程”批准该连接。只能在 `Approval` 连接阶段发送 `IApprovalRpcCommand` 类型 RPCs（请参阅 [ConnectionState.State.Approval](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/api/Unity.NetCode.ConnectionState.State.html)）。

看

* [网络连接](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html) 中的 _ 连接批准 _ 部分

## 要求

_ConnectionApproval_ scene 需要通过 _Frontend_ 菜单加载，因为它将 `NetworkStreamDriver.RequireConnectionApproval` 设置为 true，从而在启动 server 之前启用连接批准流程。默认情况下，此流程被禁用，并且 server 将默认立即批准所有 client 连接。

* BootstrapAndFrontend

可选要求使用 [Unity 身份验证](https://docs.unity.com/ugs/manual/authentication/manual/overview) 服务来验证玩家帐户。有关如何设置的详细信息，请参阅[入门](https://docs.unity.com/ugs/en-us/manual/authentication/manual/get-started) 指南。

## 示例描述

此示例演示如何设置连接批准 system 以发送和处理 `IApprovalRpcCommand` RPCs。此示例中可以使用两种批准方法，一种使用虚拟字符串，另一种使用 [Unity 身份验证](https://docs.unity.com/ugs/manual/authentication/manual/overview) 服务。您可以通过在 `ClientConnectionApprovalSystem.OnCreate()` 中切换来更改使用的类型。

使用第一种方法，client 发送一个虚拟负载（只是一个“ABC”字符串），server 验证该负载，然后用于批准 client 连接。打印调试字符串以指示过程的每个阶段（client 发送批准 RPC，server 处理它）。

第二种方法演示了如何使用身份验证服务在允许玩家加入游戏会话之前验证玩家帐户。它使用 client 上的匿名登录方法，并将给定玩家 ID 和访问令牌发送到 server 进行验证。通过从服务中获取给定 ID/令牌对的玩家信息来对其进行验证。