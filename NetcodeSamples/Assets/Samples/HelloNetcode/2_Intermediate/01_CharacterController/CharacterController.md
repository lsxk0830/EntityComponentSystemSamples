# HelloNetcode 字符控制器示例

此示例展示了如何扩展 Physics 示例中使用的运动学角色控制器，以允许角色对静态几何体做出反应并在其上行走。该运动仍然使用运动物理学，但它会检查它是否站在某物上并确保不会穿过障碍物。

看

* [Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/physics.html)
* [Unity Physics](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/TableOfContents.html) package

## 要求

生成播放器示例用于 trigger 在建立连接时自动生成播放器。使用Physics 示例中的动态物理对象。

* GoInGame
* SpawnPlayer
* Physics

## 示例描述

此示例的工作方式与 Physics 示例非常相似，唯一的区别在于角色的移动方式。该角色引用了角色控制器配置，该配置具有单独的物理 collider。用于角色控制器的 collider 仅与静态几何体碰撞，这意味着角色控制器可以执行 collider 转换来找到要移动到的有效位置。实际的移动是通过在真实的 collider 上设置物理速度并让物理移动它来执行的。这意味着与动态对象的所有交互都会执行，但它们不会影响玩家的移动。

与物理播放器示例相比，角色控制器 prefab 已进行修改，如下所示：

* 添加 CharacterControllerAuthoring 并引用新的 prefab，该新 CharacterControllerConfigAuthoring

CharacterControllerConfig 是一个单独的 prefab，它具有仅与静态对象碰撞的 collider 设置，以及一些用于移动的配置参数。
