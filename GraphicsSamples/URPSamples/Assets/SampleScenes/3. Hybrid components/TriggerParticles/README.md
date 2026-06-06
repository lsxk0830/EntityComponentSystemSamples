# TriggerParticles

此示例演示如何从 ECS System 播放 ParticleSystem。进入播放模式即可观看动画。

<img src="../../../../READMEimages/TriggerParticles.PNG" width="600">

## 它显示了什么？

scene 包含一个子场景，子场景中有一个球体和一个 ParticleSystem。JumpingSpherePSSystem 脚本制作跳跃球体的动画，并在球体撞击地面时播放 ParticleSystem。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择子场景
2. 在 Inspector 中，单击“打开”
3. 请注意，ParticleSystem 位于子场景中，并烘焙为配套 component，您可以从 ECS System 脚本访问它。

## 更多信息

有关同伴 components 的更多信息，请参阅[文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/companion-components.html)。