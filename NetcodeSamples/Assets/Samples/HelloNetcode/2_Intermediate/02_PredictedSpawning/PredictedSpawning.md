# HelloNetcode 字符控制器示例

此示例展示了在 clients 实例化网络对象时如何使用 predicted 生成。server 拥有所有联网对象或 ghosts，通常需要生成它们，但是当 clients 启动实例化时，它可以预测生成对象，然后在 server 中收到 ghost 时将其交换 snapshot。

此示例还演示了如何实现自定义分类 system 而不是使用默认分类。

看


* [Prediction](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/prediction.html)
* _ 为玩家生成的对象实现 Predicted 生成 _[Ghost snapshot](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/ghost-snapshots.html) 文档中的部分。

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。它构建在角色控制器示例之上，并使用相同的输入和角色控制器 prefab。

* GoInGame
* SpawnPlayer
* CharacterController

## 示例描述

此示例在 CharacterController 示例中向玩家添加了一个榴弹发射器。它为辅助火力输入添加了一个处理程序，该处理程序在 prediction system 中的 client 和 server 上生成手榴弹。

每个手榴弹生成都标有生成 ID（输入事件计数器与连接的网络 ID 值）。自定义分类 system 使用此生成 ID 将本地 predicted 生成的手榴弹与来自 ghost snapshots 的新生成相匹配。当它匹配时，它会使用已经生成的对象，而不是像通常那样生成一个新对象。

手榴弹本身是一个物理对象，会反弹并与其他物理对象相互作用。当发射器发射时，会产生一颗手榴弹并为其指定初始物理速度。之后物理学就会处理它。它被配置为 predicted ghost，因此它将立即与 client 上的对象发生碰撞，但如果 servers 视图不同，则会得到更正。

在一定时间间隔后，手榴弹会被 server 摧毁，并根据距离将其他物理对象推开，clients 不会预测此事件。当 client 检测到手榴弹被摧毁时，它会为其实例化粒子爆炸效果。这只是视觉效果，因此仅发生在 client 上。粒子效果是混合粒子 system，但播放完一轮后需要手动销毁。

生成的手榴弹将在红色和绿色之间交替，通过 snapshot system 创建的手榴弹将在红色和绿色之间交替，并且不会与预测生成交换。只是为了帮助查看交换是否正确。

配置 prefab 位于 scene 中，可以调整各种值，例如手榴弹的初始速度（投掷力量）及其引信计时器。