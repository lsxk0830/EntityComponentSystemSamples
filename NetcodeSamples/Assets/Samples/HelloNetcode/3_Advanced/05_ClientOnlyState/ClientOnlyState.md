# HelloNetcode Client-仅状态备份

该示例演示如何备份和恢复 predicted ghosts components，这些 predicted 不由 server 复制，并且（通常）仅存在于 client 上。
I.e components 用于跟踪仅 client 的状态机、计数器、动画状态等。

您可以[此处](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/prediction.html) 了解有关 prediction 的更多信息。

## 要求

连接需要已经在游戏中。
* GoInGame
* SpawnPlayer

## 示例描述
该示例使用帮助程序库 SamplesCommon.ClientOnlyComponentBackup，该库提供一组通用实用程序 systems 以及用于恢复和备份 client-only components 的逻辑。

我们选择使 *PlayerMovement* component 不被 server 复制。为此，我们创建了一个新播放器 ghost prefab，类似于 SpawnPlayer 示例中使用的播放器，
并通过使用 *ghost authoring 检查器 component*，我们将 *PlayerMovement* 定制为不复制（但仍然存在于 server 和 client 上，因为 server 也在使用它）
通过将变体设置为“DontSerializeVariant”。您可以在 [Ghost component 类型和变体](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-types-templates.html) 中了解有关变体及其设置的更多信息。

client 通过调用 *PlayerMovement* component 将其注册到 **ClientOnlyCollection** 单例来请求备份/恢复
RegisterClientOnlyComponents 方法。**必须在进入游戏前完成注册**。


运行时，在 worlds 上，两个 systems 负责复制和恢复 components 状态，作为 prediction 循环的一部分：
- **ClientOnlyComponentBackupSystem**：负责备份仅具有 client components 的 ghost 并创建其他附件元数据。
- **ClientOnlyComponentRestoreSystem**：负责在收到新的 snapshots 时恢复仅 components。

### 备份和恢复 systems 的工作原理

*ClientOnlyComponentBackupSystem* run 位于 prediction 循环末尾，它负责构建一些新的元数据，这是备份 component 状态和备份 component 数据所必需的。
尤其：
- 收集并创建所有呈现 client-only component 的 ghost prefab的元数据。这是在进行实际备份之前执行的预处理阶段。
- 向所有使用 prefab 类型和仅 client 数据的 predicted ghosts 添加新的 ClientOnlyBackup 状态 component。component 包含一个缓冲区，用于存储 component 备份，
以及其他附属数据。
- 对于每个完整的 prediction 刻度，它使用 ClientOnlyBackup 处理所有 predicted ghosts，并将其仅 client 的 component 状态存储在备份缓冲区中。

components 备份存储在可调整大小的循环缓冲区中。缓冲区没有固定大小，因为它需要根据需要增长以适应延迟，并且 ghosts 以不同的间隔/优先级发送。自上次 snapshot 以来的所有 predicted 报价
收到的 ghost 必须存在，以便能够恢复正确的 components 状态。

*ClientOnlyComponentRestoreSystem* 负责恢复 component 状态。它作为 GhostSimulationSystemGroup 的一部分运行，位于 GhostUpdateSystem 之后。
该顺序特别重要，原因有二：
- GhostUpdateSystem 更新所有 predicted ghost prediction 应启动的刻度。因此，当 ClientOnlyComponentRestoreSystem 运行时，此信息必须是最新的。
- GhostUpdateSystem 更新所有 ghost components 状态，因为收到 snapshot 或“部分”恢复（prediction 备份）。

通过在 GhostUpdate 之后和下一个 prediction 循环之前运行 system，我们保证**所有 components 都恢复到整个模拟组内的正确刻度**。

恢复过程本身有两个重要步骤：
- 通过从备份中内存复制回 component 数据，刷新所有需要更新的 component 数据。
- 通过删除旧的 component 备份来减小备份缓冲区的大小。这使得缓冲区大小保持在受控状态并具有有限的容量。

更多信息可以在 SamplesCommon.ClientOnlyComponentBackup 中找到（请参阅代码中的所有注释）。

### 笔记
该示例可以被视为有点“不正确”，这意味着 server 也使用 component 来驱动某些模拟。然而，由于逻辑本身的性质（及其可预测性），
client 实际上可以完全预测计数器的值。正因为如此，我们选择使用它作为示例。

### 局限性
- 目前不支持缓冲区
- 只能注册 32 个 client-only component 类型。
