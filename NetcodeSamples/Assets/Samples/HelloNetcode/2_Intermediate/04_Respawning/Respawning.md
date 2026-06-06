# HelloNetcode 重生武器示例

此示例展示了如何实现重生玩家。

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。使用Physics 示例中的动态物理对象。

* GoInGame
* SpawnPlayer
* Physics
* CharacterController
* ThinClients
* HitScanWeapon

## 示例描述

该示例建立在命中扫描武器示例的基础上，并添加了重生功能。使用至少一个薄 client 运行示例，可以在多人游戏模式工具中进行设置。
您可以瞄准并单击鼠标左键来击中它们。五次成功的命中就足以击倒他们。通过命中标记可以看出命中成功，如 HitScanWeapon 示例中所述。
当死亡时，clients 会进行旋转，然后在地图上的随机点重生。

在 `RespawnSystem` 中，重生的逻辑展示了如何摧毁旧玩家 entity 并重建它。
对于Netcode特定部分，重新​​设置这些部分非常重要，i.e。如果 `CommandTargetComponent` 和 `LinkedEntityGroup` 未正确设置，重生后断开玩家连接将无法正确使 entity 消失。

