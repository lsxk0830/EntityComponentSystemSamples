# 网格变形

此示例演示了 BlendShape 和 SkinWeight entities。进入播放模式以查看简单的网格变形。

<img src="../../../../READMEimages/MeshDeformations.PNG" width="600">

## 它显示了什么？

scene 使用常见网格变形的组合显示各种网格。红色网格使用混合形状进行变形。蓝色网格仅使用蒙皮进行变形。在紫色中，您可以看到两种方法都应用于同一网格。所有值和变换都使用写入 ECS 变换 components 的简单动画 system 进行动画处理。这些依次用于计算网格变形 system 拾取的皮肤矩阵。


## 如何使用这个示例 scene？

1. 请注意，要使用此示例，您需要将 ENABLE_COMPUTE_DEFORMATIONS 定义符号添加到 **Edit > ProjectSettings > Player > Other Settings** 中的 **Scripting Define Symbols** 列表中
2. 在 Hierarchy 中，选择子场景之一
3. 在 Inspector 中，单击“打开”
4. 选择**cube_test_mesh**对象，注意材质必须使用包含 Compute Deformation 节点的 Shader Graph

## 更多信息

有关变形的更多信息，请参阅[网格变形](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/mesh_deformations.html) 文档。
