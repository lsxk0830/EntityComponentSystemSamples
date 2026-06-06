# MaterialMeshChange

此示例演示了如何在运行时更改 Entities 上的材质和网格。进入播放模式以查看不同帧上的渲染变化。

<img src="../../../../READMEimages/MaterialMeshChange.PNG" width="600">

## 它显示了什么？

scene 包含两个 entities。一个更改其网格，另一个更改其材质。MeshChanger 和 MaterialChanger 是执行相应网格/材质交换的 ECS 脚本。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择子场景
2. 在 Inspector 中，单击“打开”
3. 在 Hierarchy 中，选择 **MeshChange** 或 **MaterialChange** GameObject
4. 在 Inspector 中，请注意附加了 Authoring components
5. 关闭子场景并进入播放模式。这两个 GameObjects 被烘焙到 Entities 并使用 ECS System 更改其网格和材质