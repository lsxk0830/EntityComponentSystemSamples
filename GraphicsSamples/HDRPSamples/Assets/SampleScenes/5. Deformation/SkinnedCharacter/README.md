# SkinnedCharacter

此示例演示了使用蒙皮网格的角色。

<img src="../../../../READMEimages/SkinnedCharacter.PNG" width="600">

## 它显示了什么？

scene 使用网格变形显示四个动画角色。它演示了计算着色器变形（粉色）和顶点着色器变形（蓝色）。顶点蒙皮必须使用线性混合蒙皮节点；计算蒙皮必须使用 Shader Graph 中的计算变形节点。所有角色均使用写入 ECS 变换 components 的简单动画 system 进行动画处理。这些依次用于计算网格变形 system 拾取的皮肤矩阵。

## 如何使用这个示例 scene？

1. 请注意，要使用此示例，您需要将 ENABLE_COMPUTE_DEFORMATIONS 定义符号添加到 **Edit > ProjectSettings > Player > Other Settings** 中的 **Scripting Define Symbols** 列表中
2. 在 Hierarchy 中，选择子场景之一
3. 在 Inspector 中，单击“打开”
4. 选择 **Geom_Body_LOD0** 对象，请注意，材质必须使用具有线性混合蒙皮或计算变形节点设置的着色器图。

## 更多信息

有关变形的更多信息，请参阅[网格变形](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/mesh_deformations.html) 文档。