# HDRPLitProperties

此示例演示了 Entities 上不同 HDRP Lit 材质属性的材质属性覆盖。

<img src="../../../../READMEimages/HDRPLitProperties.PNG" width="600">

## 它显示了什么？

scene 包含使用 HDRP Lit 着色器的球体。球体位于子场景中。

附加到球体 MeshRenderers 的材质覆盖 authoring components 覆盖球体颜色、平滑度和金属属性的值。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择子场景
2. 在 Inspector 中，单击“打开”
3. 在 Hierarchy 中，选择一个球体
4. 在 Inspector 中，注意有几个 HDRP 材质属性 Authoring components。如果要覆盖其他 HDRP 材质属性，可以添加其他 HDRP 材质属性 Authoring components

## 更多信息

有关材质属性覆盖的更多信息，请参阅[文档](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/material-overrides-code.html)。