# HelloNetcode Server 侧面动画示例

## 要求

生成播放器示例用于 trigger 建立连接时自动生成播放器。
角色控制器用于使用键盘和鼠标输入来移动和旋转玩家。
本示例中重复使用了 client 侧面动画中的角色模型。

* GoInGame
* SpawnPlayer
* CharacterController
* ClientSideAnimation

## 示例描述

此示例展示了如何向生成的播放器添加动画。该动画是使用称为 [Mecanim](https://docs.unity3d.com/Manual/AnimationOverview.html) 的内置动画 system 创建的。
该动画在 server 端运行，并在每个 client 上复制。

scene 包含角色可以在其上移动的平面和示例 SpawnPlayer 中提到的生成器。进入游戏模式时，您将在游戏视图中看到该角色。摄像机将跟随角色四处移动。
要移动角色，您必须使用箭头键。您还可以通过按下鼠标按钮并拖动到两侧来改变视角来转动角色。注意跟随摄像机视图的角色。

名为 ServerAnimatedCharacter 的 prefab 是分配给 subscene 中“Spawner”的 entity。
它被设置为处理字符控制器作为 CharacterController 示例。

我们使用 Ghost 演示游戏对象 Authoring 来生成 Terraformer_Client prefab 作为角色的 client 侧面表示。server 侧面表示设置为 Terraformer_Server prefab。如果打开 server 侧 prefab，您会注意到几何体被禁用，只有骨架保持启用状态。
动画将运动应用到骨架，然后将其复制到启用几何图形的 client 模型版本。

当进入播放模式并在 baking 步骤期间，将生成演示对象并分配一个 entity。您将在 scene 层次视图中将它们视为 Terraformer_[Server/Client]（克隆）。

Terraformer prefab 使用来自 Netcode package 的 GhostAnimationController。这些要求我们将 GhostAnimationGraph 资源分配给名为“Animation Graph Asset”的字段。
在此示例中，其中一个称为 StateSelector，另外创建了四个包含 Jump、Run、Stand 和 InAir 的逻辑。

StateSelector 负责决定动画逻辑中的当前状态。这是通过查询角色控制器逻辑并决定角色是否应该移动、跳跃等来完成的。
