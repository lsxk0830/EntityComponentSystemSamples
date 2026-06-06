# HDRPShaders

此示例演示了 Entities 上的 HDRP Lit、LitTessellation、Unlit、LayeredLit、LayeredLitTessellation 着色器的材质属性覆盖。

<img src="../../../../READMEimages/HDRPShaders.PNG" width="600">

## 它显示了什么？

scene 包含使用 Lit、LitTessellation、Unlit、LayeredLit、LayeredLitTessellation 和 Shader Graph PBR 输出的球体。

附加到球体 MeshRenderers 的材质覆盖 authoring components 覆盖球体颜色和发射值的值。

进入“播放”模式以查看球体 GameObjects 烘焙到 Entities 并让材质覆盖 components 相应地更改颜色。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择 **LitRedEmissive** 或 **UnlitRedEmissive** 球体
2. 在 Inspector 中，请注意有一个 HDRP 材质属性基色 Authoring 和发光颜色 Authoring components。如果要覆盖其他 HDRP 材质属性，可以添加其他 HDRP 材质属性 Authoring ZXQFAQBNAYBKE​​UKZXQ
3. 选择 **LayeredLit** 或 **LayeredLitTessellationMagenta** 并在 Inspector 中观察到有“Layered Lit Base Color 0 Authoring”component，这是 HDRPShaders 目录中的脚本，使用 HDRP LayeredLit“_BaseColor0”关键字。
4. 在 Hierarchy 中，选择 **PBRBlue** 球体
5. 在 Inspector 中，请注意有材质颜色 component。如果您想要覆盖其他自定义 Shader Graph 材质属性，您可以引用 MaterialColor 脚本并为每个自定义材质属性创建一个脚本
6. 点击**Edit**编辑 Shader Graph，注意 Color 属性的 Node Settings 启用了 Hybrid Per Instanced 属性

## 更多信息

有关材质属性覆盖的更多信息，请参阅[文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest/index.html)。