# DOTS 和 UI 工具包集成示例

此示例演示了 UI 工具包在基于 Entities 的项目中的使用。在游戏中，玩家控制一个巫师，为他的魔法汤收集原料。

要理解此示例，您需要先了解 UI 工具包和 Entities 的一些基本知识：

- [UI 工具包介绍材料](https://learn.unity.com/course/ui-toolkit-fundamentals)
- [Entities 介绍材料](https://github.com/Unity-Technologies/EntityComponentSystemSamples)

项目代码位于 `Assets/Scripts` 目录下，该目录下有 `Gameplay`、`ScriptableObjects`、`UI` 三个子目录。entity components 主要定义在 `Gameplay/Components` 子目录中。

有关该项目的演练，请观看[此视频](https://youtu.be/72EaJ1OW9Nw)（18 分钟）。

## 游戏代码

### `Gameplay/GameManagerSystem.cs`

控制宏观游戏状态的 system（表示为存储在 `GameData` 单例 component 中的 `GameState` 枚举）。

### `Gameplay/GameInput.cs`

包含 `InputAction` 对象的静态类，该对象由 `GameManagerSystem` 初始化。由 systems 用于读取输入。

### `Gameplay/PlayerMovementSystem.cs`

移动玩家角色的 system。由于角色是动态刚体，因此其运动是通过设置其 `PhysicsVelocity` component 来控制的。

### `Gameplay/CameraFollowSystem.cs`

一个 system，使相机跟随玩家。

### `Gameplay/QuestSystem.cs`

处理任务交互的 system：拾取原料、更新收集原料的 HUD 显示，以及在大锅处交出任务。

### `Gameplay/EnergyBallSystem.cs`

控制能量球拾音器的 system。当玩家走近能量球时，能量球会受到玩家的引力并绕着玩家旋转。

### `ScriptableObjects/CollectablesData.cs`

显示在库存中的一组成分名称及其关联的精灵。

### `ScriptableObjects/DialogueData.cs`

游戏开始和结束时显示的对话字符串。

### `ScriptableObjects/QuestData.cs`

完成任务所需收集的原料数量。

## UI 代码

### `UI/UIScreen.cs`

其他屏幕类的基类。（这里的“屏幕”实际上是指 UI 元素。我们将它们称为“屏幕”，以避免与 UI Toolkit 本身提供的元素类型混淆。）

`UIScreen` 继承自 `ScriptableObject`，因此它的实例可以存储在 `UnityObjectRef` 中。

每个 UIScreen 都有一个 `EntityCommandBuffer`，以便它可以记录要在下一帧中处理的“事件”entities。例如，当用户单击清单屏幕上的关闭按钮时，单击处理程序使用 `EntityCommandBuffer` 创建表示单击操作的事件 entity。

### `Gameplay/EventSystem.cs`

system，每一帧都会：

1. 销毁前一帧的事件 entities。
2. 播放屏幕的 `EntityCommandBuffer`。
3. 为屏幕创建一个新的 `EntityCommandBuffer`。

### `Gameplay/UISystem.cs`

system，处理 UI 屏幕生成的事件 entities。例如，对于表示单击屏幕关闭按钮的事件，`UISystem` 将为 query，如果存在这样的 entity，则 `UISystem` 将关闭屏幕。

### ZXQMSY 富雅卡 XLZXQ

游戏开始和结束时出现的对话 UI 元素的包装。

### `UI/HelpScreen.cs`

帮助 UI 元素的包装器（单击问号按钮时出现的帮助屏幕）。

### `UI/HintScreen.cs`

提示 UI 元素的包装（当成分靠近玩家时出现在成分旁边的文本）。

### `UI/HUDScreen.cs`

HUD UI 元素的包装器（用于打开帮助屏幕和清单屏幕的按钮的底部工具栏）。

### `UI/InventoryScreen.cs`

弹出清单 UI 元素的包装器。

### `UI/InventorySlot.cs`

代表库存槽位的 UI 元素的包装。

### `UI/QuestScreen.cs`

任务 UI 元素的包装器（右上角的任务状态信息）。

### `UI/SplashScreen.cs`

初始 UI 元素的包装（游戏一开始的“按任意键”文本）。



