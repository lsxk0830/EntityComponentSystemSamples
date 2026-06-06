# Prediction 开关示例
[Prediction 切换](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/prediction.html#prediction-switching) 允许您动态切换 ghost 的 `GhostMode`（i.e。在播放模式期间），允许 clients 选择加入 prediction，最终节省了 CPU 周期。

此示例通过简化的“足球”沙箱演示了该想法。

* `Sphere.prefab` 是一个物理球，预测它的成本相对较高（大量）。是本次优化的目标。
* `Player.prefab` 是我们的播放器（i.e.角色控制器），它：
    * 与这些球互动（通过与它们碰撞）。
    * 定义“Prediction 切换半径”的中心（请参阅 `PredictionSwitchingSystem.cs`）。

#### 色键
* **青色** - Interpolated Ghosts。
* **绿色** - Predicted Ghosts。
* _ 玩家颜色为 excluded._

> [!NOTE]
> “边界框抽屉”工具也使用此颜色键，该工具可通过 `Multiplayer PlayMode Tools Window > Bounding Box Drawer > Disabled` 按钮进行切换。

通过进入游戏模式，您可以观察到玩家半径内的球转变为 Predicted（反之亦然）。
您还应该能够观察到 interpolation 的应用（在过渡期间），尤其是当球相互弹开时。

请参阅 `PredictionSwitchingSettingsAuthoring` `MonoBehaviour` 来修改设置，并观察它们如何影响游戏玩法。

## 要点
* 一般来说：与 NetCode 中的所有内容一样，移动速度更快（且更难以预测）的 entities 更难补偿。
* “Prediction 切换”允许您有选择地选择“预测 ghost”，让您在 client 性能（通过 Interpolation）和游戏响应能力（通过 Client）之间进行适当的权衡 Prediction)，同时保持 server 权限。
