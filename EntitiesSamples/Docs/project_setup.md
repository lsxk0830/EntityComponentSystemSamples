# 新的 Entities 项目设置

使用 Unity 2022.2，从 HDRP 或 URP 模板创建一个新项目，然后从 package 管理器中按名称添加所需的 DOTS packages：

![添加 package](./images/add_package.png)

- `com.unity.entities`
- `com.unity.entities.graphics`（用于渲染 entities）
- `com.unity.collections`（非托管集合类型）
- `com.unity.physics`（用于 entities 的碰撞检测和物理模拟）
- `com.havok.physics`（替代 Havok 提供的物理“后端”）
- `com.unity.netcode`（适用于 entities 驱动的 server-client 多人游戏）

这些 packages 将包含 `com.unity.burst` 和 `com.unity.mathematics` 作为依赖项。

在 Entities 项目中工作时，您通常需要以下两个设置：

- 在 Unity 首选项的“Entities”部分中，将“Scene View 模式”设置为“运行时数据”。
- 在项目设置的“Editor”部分中，启用“输入 Play Mode 选项”，但禁用“重新加载域”和“重新加载 Scene”。（参见[Unity 手册 - 可配置输入 Play Mode](https://docs.unity3d.com/Manual/ConfigurableEnterPlayMode.html) 和[Unity 博客 - 在 Unity 中更快地输入 Play Mode 2019.3](https://blog.unity.com/technology/enter-play-mode-faster-in-unity-2019-3).)