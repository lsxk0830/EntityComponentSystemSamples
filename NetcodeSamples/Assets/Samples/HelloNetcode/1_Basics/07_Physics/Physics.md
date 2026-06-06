# HelloNetcode Physics 示例

与普通变换驱动的 ghost 相比，启用 Physics 的 ghosts 可以与所需的一些调整同步。在此示例中，物理过程在 client 和 server 上运行。client 上的所有内容都是运动学的，并由 server 驱动，但玩家是 predicted，因此可以立即进行物理模拟，并通过 server snapshot 更新进行纠正。这是使用 Unity Physics (com.unity.physics) package。

看

* [Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/physics.html)
* [Unity Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/TableOfContents.html) package

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。

* GoInGame
* SpawnPlayer

## 示例描述

为了能够在 client 和 server 上模拟 predicted ghosts，并且仅在 server 上模拟 interpolated ghosts，需要将 _NetCodePhysicsConfig_ component 添加到项目中。需要选中 _ 预测物理 _ 复选框，但默认值在这里可以正常工作。

与生成播放器示例相比，物理播放器 prefab 已被修改，如下所示：

* 添加 _PhysicsBody_ 和 _PhysicsShape_ components
* 将 _ 形状类型 _ 更改为 _ 胶囊 _ 以匹配玩家形状。
* 将 _Motion Type_ 更改为 _Kinematic_，以便玩家手动驱动。
* Ghost authoring 不变（所有者 predicted）

由于它是运动学的，因此它是手动控制的，而不是受到物理（如重力）的影响。但它可以与其他物理对象相互作用。

interpolated 枪管 prefab 具有相同的物理设置，除了

* _ 运动类型 _ 是动态的（默认值）。
* 将 _Shape Type_ 更改为 _Cylinder_ 以匹配其形状
* _ 支持的 Ghost 模式 _ 设置为 _ 插值 _

它纯粹由 server 模拟驱动，client 仅应用 server 给出的值。

predicted 枪管 prefab 具有与 components 相同的物理特性，设置与 interpolated 相同，但是

* Ghost authoring component 将 _ 支持的 Ghost 模式 _ 设置为 _ 全部 _ 并将 _ 默认 Ghost 模式 _ 设置为 _ 预测 _。

在这两种情况下（interpolated 和 predicted），枪管将在 client 上设置为运动学，但 predicted 将从物理 prediction 组手动驱动模拟步骤，因此会受到重力等物理力的影响。

## 笔记

当 predicted 玩家遇到 interpolated 桶时，有两件事会影响它的外观。首先，如果此处的帧速率降至 60 以下，物理模拟将开始受到影响并且看起来更糟。这可以通过确保您拥有来改进

* Burst 编译启用
* Jobs 调试器已禁用

其次，随着延迟的增加，在应用新的 ghost snapshot 更新时，可能需要进一步预测。这会增加性能损失，因为 ghost prediction system 组需要多次 run。这可以通过播放模式工具中的模拟参数进行测试，并在分析窗口中查看 CPU 模块中的时间线。添加例如 50 毫秒延迟，并查看 _PredictedPhysicsSystemsGroup_ 在 _GhostPredictionSystemGroup_ 内执行的次数差异。
