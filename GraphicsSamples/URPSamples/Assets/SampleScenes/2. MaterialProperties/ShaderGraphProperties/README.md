# ShaderGraphProperties

此示例演示了 Entities 上 Shader Graph 着色器的材质属性覆盖。

<img src="../../../../READMEimages/ShaderGraphProperties.PNG" width="600">

## 它显示了什么？

scene 包含使用 Shader Graph PBR 输出的立方体。其中一些立方体的 MeshRenderers 附加了 **材质覆盖 authoring components**。这些 component 覆盖立方体的颜色。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择 **CubeRed** 立方体
2. 在 Inspector 中，记下材质颜色 component。如果您想要覆盖其他自定义 Shader Graph 材质属性，您可以引用 MaterialColor 脚本并为每个自定义材质属性创建一个脚本
3. 单击 **Edit** 编辑 Shader Graph，请注意，Color 属性的 Node Settings 已启用 **Override Property statements**，并且 **ShaderDeclaration** 设置为 **Hybrid Per Instance**


## 更多信息

有关自定义 Shader Graph 材质覆盖的更多信息，请参阅[文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/material-overrides-code.html)。
