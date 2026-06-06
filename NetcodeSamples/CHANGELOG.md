# 变更日志

## [1.12.0] - 2026-02-12

### 改变了
- 多人游戏服务 package 更新至 1.2.0 及其他依赖项更新
- 前端菜单现在支持使用会话（多人游戏 SDK），默认情况下，当使用主机迁移或中继支持时，需要使用会话方法。这比手动实现这些功能要简单得多。
- `01b_RelaySupport` 和 `01d_HostMigration` 示例现在包含自己的前端实现，并且独立于默认前端。
- 更新了对最新 Netcode for Entities package (1.12.0) 的公共示例支持
- 默认包含新的 Netcode Profiler （它取代了旧的基于网络的网络调试器，对于调试和优化Netcode项目来说更强大、更有用）

## [1.9.1] - 2025-10-11

### 固定的
- 主机迁移示例中存在以下问题：在托管游戏、离开然后重新托管时，迁移更新数据未正确更新/压缩。
- 通过中继时连接批准示例中的问题。

## [1.9.0] - 2025-09-13

### 额外
- 在 hello netcode 示例中添加了调试级别日志记录，以便更好地跟踪输入处理和生成。

### 固定的
- `Asteroids` 中的问题，其中 `AsteroidScore` ghost 被从构建中删除。
- `ShipTrackingSystem.ShipTrackingJob` 存在问题，它迭代了游戏中的所有 entities，而不是一次。

### Host Migration (HelloNetcode)

#### 变化
- 在 `Asteroids` 中，删除了仅 server 的玩家颜色跟踪 system。现在，颜色再次像以前一样映射到网络 ID，并且这些颜色已正确迁移。一个副作用是，当前一个主机的 client 重新连接时，如果 client world 已被破坏（例如返回主菜单时），他将不再被检测为重新连接。

#### 固定的
- 未正确检测到重新连接的玩家并在 `PlayerSpawn` 示例及其派生者中重复生成的问题。
- 迁移后，`Importance` 示例中产生双倍数量的桶的问题。
- 等待中继加入代码到达时可能会崩溃（server world 可能会消失）。
- 大厅迁移数据中使用 UTC 时间而不是本地时间的问题

## [1.8.0] - 2025-08-17

### 改变了
- 改进了 `Asteroids` 中 system 的重要性。
- 对 `Optimization` 示例的改进。
- 对 `Importance` 示例进行了大修，与 Netcode for Entities package 中的新 `Importance Visualizer` 一起使用。

### 固定的
- 在 `Asteroids` 中，正确应用 prefab 中的子弹秤。


## [1.7.0] - 2025-07-29

### 固定的
- 前端中的问题，在会话之间不会保存所选示例（始终选择示​​例或 HelloNetcode 列表中的第一个）

### Relay 支持 (HelloNetcode)

#### 改变了
- 输入加入代码后，托管按钮将被禁用

#### 固定的
- 当按下加入按钮时不存在加入代码时，消息现在会打印到 UI 字段
- 当在空白字段中按下加入按钮时，当您开始输入加入代码时，它不再尝试立即加入（现在可以正确重置状态）
- 返回前端菜单后无法再次主持或加入的问题
- 通过继电器运行时 `ConnectionApproval` 示例失败


## [1.6.2] - 2025-07-07

### 额外
- 使用添加到项目中的自定义模板进行预序列化测试。


## [1.6.1] - 2025-05-28

### 固定的
- 继电器示例和 WebGL 平台目标无法正常协同工作的问题。

### 小行星

### 改变了
- 改进了相关性和重要性扩展实施，将小行星数量从 200 增加到 800
- 改进了 `AsteroidScore` 和碰撞处理。

### 主机迁移（HelloNetcode）

#### 改变了
- 添加了测试/调试开关，以便在当选为主机时手动使主机迁移失败
- 更新了 Netcode for Entities 1.6.1 中的 API 更改，添加了 `ENABLE_HOST_MIGRATION` 定义到播放器设置以启用主机迁移功能

#### 固定的
- 修复了以下情况：主机迁移正在进行时 client 加入，但新主机无法成为主机，则 client 可能会被选为新主机，但它会位于 join-as-client 流程的中间，并且没有 client world 或 scenes 尚未加载。


## [1.5.0] - 2025-04-22

### 额外

### 改变了
- 修改了 `LoadScenes_AllScenesShouldConnect` 测试以垃圾邮件重新连接按钮，从而对 Netcode for Entities（和每个示例）重新连接流的正确性施加更多负担。
- 清理了一些不断变化的元文件。
- 删除了重复的小行星 scene。
- 修复了 `ConnectionApproval` 示例中的问题，其中断开连接不会停止 RPC 的发送，导致重新连接时出现 RPC 警告。`PlayerList` 和 RPC 示例也是如此。
- 更新了 `SceneLoading` 测试以使用 `AutomaticThinClientWorldsUtility`。
- 停止在示例选择下拉列表中包含 `DisableBootstrap` scene。
- 切换到使用多人游戏 SDK 而不是中继 package。
- 不再需要 `GhostPredictionSwitchingSystemForThinClient` （Netcode不需要它）。

### 固定的
- `PredictedSpawning` 分类 system 的问题。
- `MultyPhysicsWorld` 示例中的粒子问题
