# HelloNetcode 命中扫描武器示例

此示例展示了如何实现具有滞后补偿的命中扫描武器（即时命中、零尺寸射弹）。

看

* [Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/physics.html)
* [Unity Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/TableOfContents.html) package

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。使用Physics 示例中的动态物理对象。

* GoInGame
* SpawnPlayer
* Physics
* CharacterController

## 示例描述

该示例基于角色控制器示例构建，并添加了命中扫描武器。命中扫描武器将执行 raycast 以确定武器击中的位置。为了处理 interpolated 和 predicted 角色之间的时间差异，而不强迫玩家引导目标，server 使用滞后补偿 - 这意味着它会针对接近玩家在射击时在屏幕上看到的内容执行 raycast。

该示例有一个复选框可以切换延迟补偿以演示效果，请确保您正在使用模拟延迟进行测试以查看效果。
当命中扫描武器击中时，它将生成命中标记，显示命中位置，使用 + 表示 client 命中，使用 x 表示 server 命中。
