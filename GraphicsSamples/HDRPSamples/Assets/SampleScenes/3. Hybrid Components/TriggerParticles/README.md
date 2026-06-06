# TriggerParticles

此示例演示了如何从 ECS System 播放 VFX 图表。进入播放模式即可观看动画。

<img src="../../../../READMEimages/TriggerParticles.PNG" width="600">

## 它显示了什么？

scene 包含一个子场景，子场景中有一个球体和一个 VFX 图。JumpingSpherePSSystem 脚本对跳跃的球体进行动画处理，并在球体撞击地面时播放 VFX 图形。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择子场景
2. 在 Inspector 中，单击“打开”
3. 请注意，VFX 图表位于子场景中，并烘焙为配套组件，您可以从 ECS System 脚本访问该组件。

## 更多信息

有关配套 components 的更多信息，请参阅[配套 Components 文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/companion-components.html)。