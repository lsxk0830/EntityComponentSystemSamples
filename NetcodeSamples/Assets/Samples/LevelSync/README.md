# 联网级同步演示

此示例包含如何在使用Netcode package 时管理 server 和 clients 之间的级别同步的演示。

## 结构

- 主要模块是 _NetcodeLevelSync.cs_ 文件，它实现了网络级别同步流程。它不会自行加载/卸载 scene，因为这是非常特定于用例的。
- _LevelSync_Bootstrap_ scene 包含基于 subscene 的关卡流程，演示如何使用网络同步模块。
  - 它有一个 UI，它使用 LevelManager.cs 作为入口点。
  - 它使用 LevelTracker* systems 来处理加载/卸载例程。

## 流动

_NetcodeLevelSync_ 实现以下流程：
- Server 启动关卡加载
  - 禁用 ghost 同步（所有连接上的 _NetworkStreamInGame_）
  - 卸载当前关卡并开始加载新关卡
  - 发送 RPC 命令到连接的 clients 告诉它们也切换级别
- 当 client 收到 RPC 命令时
  - 禁用 ghost 同步（server 连接上的 _NetworkStreamInGame_）
  - 卸载当前关卡并开始加载下一关
  - 加载完成后，发送 RPC 到 server 表示您已准备好
- 当 server 收到来自所有 clients 的就绪消息并且自己完成加载时
  - 启用 ghost 同步（在所有连接上启用 _NetworkStreamInGame_）

_NetcodeLevelSync_ 通过 _LevelSyncStateComponent_ 数据进行控制。
```c#
public struct LevelSyncStateComponent : IComponentData
{
    public LevelSyncState State;
    public int CurrentLevel;
    public int NextLevel;
}
```

有效的电平同步状态是
```c#
public enum LevelSyncState
{
    Idle,
    LevelLoadRequest,
    LevelLoadInProgress,
    LevelLoaded
}
```

- 通常状态为 _Idle_。
- 当 server 开始关卡加载时，他应该将状态置于 _LevelLoadInProgress_ 中
- 当加载新关卡的命令到达时，client 将看到 _LevelLoadRequest_，并需要通过自己启动关卡加载例程并将状态设置为 _LevelLoadInProgress_ 来对此做出反应。
- 当 server 或 client 完成加载时，它们应将状态设置为 _LevelLoaded_
- 处理完成后，状态将返回 _Idle_。

## 示例代码参考

更清楚地查看流程的一个好方法是查看它的测试，因为它在一个文件中以简单的方式完成所有步骤。参见[SceneAutomationSystems.cs](../../Tests/SceneLoadingTests/SceneAutomationSystems.cs)
