# HelloNetcode 命中扫描武器示例

此示例展示了如何实现 world 空间健康栏。

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。使用Physics 示例中的动态物理对象。
角色控制器用于移动角色。

* GoInGame
* SpawnPlayer
* Physics
* CharacterController
* HitScanWeapon
* 重生

## 示例描述

该示例建立在命中扫描武器示例和增加生命值的重生示例的基础上。此示例演示如何将此运行状况 component 显示为 world 空间运行状况栏。
要查看此内容，请进入播放模式，并在播放模式工具窗口中启用至少一个薄 client。您会看到，在所有玩家上方，都有一个可见的生命条，黑色背景，里面有一个绿色条。

当您射击并击中时，您将看到生命条滴答作响，直到它为空，这将 trigger 重生，如名为“重生”的示例中所述。
生命值将以完全健康状态重生，角色也将处于新位置。

## 笔记

`HealthBarSpawnerAuthoring` 正在创建 IComponent 作为类而不是结构。这是因为我们需要维护对生命条 prefab 的游戏对象的引用。
在 `SpawnHealthBarSystem` 中，我们使用常规的 Object.Instantiate 方法实例化游戏对象。
