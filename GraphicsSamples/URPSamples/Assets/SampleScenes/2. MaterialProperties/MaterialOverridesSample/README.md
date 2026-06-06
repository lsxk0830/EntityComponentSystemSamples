# MaterialOverridesSample

此示例演示了无需编写代码即可覆盖材质属性的设置。

<img src="../../../../READMEimages/MaterialOverridesSample.PNG" width="600">

## 它显示了什么？

scene 包含使用 URP Lit 和 URP Unlit 着色器和着色器图的球体。附加在 MeshRenderer 上的球体 MaterialOverride component 会覆盖材质属性值。MaterialOverride component 引用 MaterialOverride 资产，您可以使用该资产来配置要覆盖的属性。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择名称中包含 **PerInstance** 的任何球体
2. 在 Inspector 中，请注意附加了 MaterialOverride component
3. 单击 MaterialOverride 资产，观察配置的属性

## 更多信息

有关材质覆盖的更多信息，请参阅[材质覆盖](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/material-overrides.html) 文档。