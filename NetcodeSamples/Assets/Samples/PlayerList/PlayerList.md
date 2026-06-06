# PlayerList - 加入者和离开者“饲料”

## 要求
* “PlayerList”使用 RPCs，即使“游戏外”也可用。
* “PlayerList”示例使用可选的 `ICleanupComponentData` `ConnectionState`。

## 示例描述

“PlayerList”示例使用 RPCs 创建已连接/断开连接的玩家的“提要”，包括断开连接的“原因”(i.e.cause)。

>[!NOTE]
> 它是通过 RPCs 实现的，这样就可以在不需要同步 snapshots 的情况下使用。
> 然而，另一种方法（此处未演示）是通过单个静态优化的 ghost（带有包含所有连接玩家的 IBufferElementData）来实现此行为。

### `ServerPlayerListSystem`
`ConnectionState` 通过 `OnUpdate` `AddComponent` 调用添加（使用所有 `NetworkStreamConnection` entities 的 query，其中没有 ConnectionState）。

新加入者在 `HandleNewJoinersJob` 中处理。此 job 必须等待来自 client (`ClientRegisterUsernameRpc`) 的 RPC，因为除了请求之外无法推断出 clients `Username`。
因此，每个 client 必须选择在玩家列表中显示。
然后验证该用户名，并将其广播给所有其他玩家。玩家现在被视为“已连接”（或“已加入”）。

>[!NOTE]
> 对于您自己的游戏，您可以根据自己的喜好获取用户名。
> E.g。GameServer 可以与后端通信，接收预期的 clients 列表。
> Clients 在连接到 server 时进行身份验证，并预计提供一次性加入令牌，然后可以将其映射到用户名。

对于断开连接的玩家：`NotifyPlayersOfDisconnectsJob` job 轮询每个 `ConnectionState` FSM，检测断开连接（无论原因如何），并再次将此更改广播到 clients。
Unity 传输 Package (UTP) 可以检测：
* Client 断开连接。
* Server 断开连接（i.e。“踢”或“引导”玩家）。
* 套接字/驱动程序超时（i.e。X 秒内 client 没有响应，假设已断开连接）。
这个 `DisconnectReason` 也会广播给所有其他玩家，供您选择阅读。我们建议将其传达给玩家，因为这是一项 UX 改进：玩家通常很高兴知道他们的朋友发生了什么事。

### `ClientPlayerListSystem`
* 使用“PlayerList”子 system 注册用户名（通过 `ClientRegisterUsernameRpc`）。
* 尝试设置无效用户名时接收并处理 `InvalidUsernameResponseRpc`（e.g。您可能需要使用亵渎或滥用过滤器）。
* 接收并处理 `PlayerListEntry.ChangedRpc` 更改事件（玩家连接、断开连接和更改用户名）。

### 渲染 Systems
* `ClientPlayerListEventSystem` 获取所有 `ChangedRpc` 并将它们转换为 `PlayerListNotificationBuffer` 条目。
* `RenderPlayerListMb` 是此数据的无状态、调试、IMGUI 渲染器。它还提供了一个文本字段，允许您更新您的用户名。

### 测试
您还可以使用“多人游戏 > 窗口：PlayMode 工具”中的新控件来测试各种形式的断开连接（使用瘦 clients 来模拟其他玩家）。
现在也可以测试“套接字超时”。UTP 默认为 30 秒连接超时。
显然，也支持构建一个播放器来测试这一点。

>[!NOTE]
> 我们已尽力确保通知的有序性。I.e。连接时，您只会看到在您之后 _ 发送连接通知 RPCs 的玩家的“新加入者”通知。
