# EntityCreation

此示例演示如何在 run 时间高效创建通过 Entities Graphics 渲染的 entities 以及如何通过 ECS components 进行颜色设置。进入 Play 模式即可看到生成的 entities。

<img src="../../../../READMEimages/EntityCreation.PNG" width="600">

## 它显示了什么？

TestEntityCreationAPI 脚本使用 RenderMeshDescription 和 RenderMeshUtility.AddComponent APIs 与实例化来高效创建 entities。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择 Spawner
2. 在 Inspector 中，配置设置
3. 点击播放

## 更多信息

有关 RenderMeshUtility 和 run 时间 entity 创建的更多信息，请参阅[文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/runtime-entity-creation.html)。

