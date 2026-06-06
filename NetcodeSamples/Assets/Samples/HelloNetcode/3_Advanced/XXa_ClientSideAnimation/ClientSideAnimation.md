# HelloNetcode Client 侧面动画示例

## 要求

生成播放器示例用于 trigger 建立连接时自动生成播放器。
角色控制器用于使用键盘和鼠标输入来移动和旋转玩家。

* GoInGame
* SpawnPlayer
* CharacterController

## 示例描述

此示例代码演示了如何使用 Unity 中的内置动画 system（称为 [Mecanim](https://docs.unity3d.com/Manual/AnimationOverview.html)）向生成的播放器添加动画。
动画仅在 client 端运行，不同步到 server。父 entity 的翻译是唯一同步的 component。

scene 包含角色可以在其上移动的平面和 `SpawnPlayer` 示例中提到的生成器。
进入游戏模式后，角色将出现在游戏视图中，并且摄像机将跟随他们。
要移动角色，请使用箭头键。您可以使用鼠标转动角色，这将改变视点。请注意，角色跟随摄像机视图移动。

在 `Character` 文件夹中，您将找到 prefab `ClientAnimatedCharacter`，它是分配给 subscene 中的 `Spawner` 的 entity。
它被设置为处理 `CharacterController` 示例等字符控制器。
连接了 `Ghost Authoring Inspection Component` 并将 `DontSerializeVariant` 设置在旋转 component 上。
这很重要，因为 server 上的旋转不同步，并且 server 的值将覆盖 client 侧设置的旋转值。

`Ghost Presentation Game Object Authoring` 用于生成 `Terraformer` prefab 作为角色的 client 侧表示。
未设置 server 端表示，因为 server 端不需要表示对象。
在 baking 步骤期间以及进入播放模式时，将生成演示对象并为其分配 entity。
Netcode 在 package 代码中的 `GhostPresentationGameObjectSystem` system 内部执行此操作。
一旦 `Terraformer` 生成，entity 和游戏对象 worlds 之间的通信可以通过附加到它的 `MonoBehaviour` 进行。

`Terraformer` prefab 具有 `Character Animation` component 和 `Animator` component。
`Animator` component 具有 `Animator Controller`，其中包含何时播放动画剪辑的逻辑。
该控制器具有 `Character Animation` 脚本中使用的参数列表，用于控制正在播放的动画。

从 system `UpdateAnimationStateSystem` 调用 `Character Animation` 类，以确保在调用 `UpdateAnimationState` 方法之前收集输入数据。
此方法还返回 system 中使用的更新的 `LocalTransform` component 以更新角色的旋转。

通过结合角色控制器数据来知道何时跳转、run 等，动画控制器的状态机中的状态正确响应这些信息。
此外，当通过在游戏视图中移动鼠标来转动视口时，动画会跟随相机的视点。

## 笔记
* 在下面的示例中，您将看到 server 侧面动画示例。
* 为了同步旋转值，还需要扩展角色控制器示例中的 CharacterControllerPlayerInput。这里我们需要发送更新的旋转值来更新 server 端。

## Hack 修复编辑器问题

在示例 scene 中，添加了一个名为 Hack 的附加 scene。其中包含一个 component Hack，它引用 Terraformer 并将来自 Animator 的所有动画剪辑存储在列表中。
在编辑器/播放器实现中发现了烘焙 entity 引用的序列化和反序列化工作方式的错误。
在唤醒期间，引用未按预期顺序初始化，导致独立构建中出现未分配的动画剪辑引用。

引用动画剪辑的附加 scene 可确保为动画器正确维护和序列化动画剪辑引用。
当运行时的修复程序发布时，这种黑客攻击将不再需要。

为了使此黑客攻击在 CPU 成本方面成为零成本抽象，SubScene 上已禁用自动加载。这可以在 Hack scene 的检查器中看到。
