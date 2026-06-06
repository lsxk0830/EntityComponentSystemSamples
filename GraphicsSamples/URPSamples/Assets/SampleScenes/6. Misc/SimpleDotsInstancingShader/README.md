# SimpleDotsInstancingShader

此示例演示了一个简单的无光照着色器，它使用 DOTS 实例进行渲染。

<img src="../../../../READMEimages/SimpleDotsInstancingShader.PNG" width="600">

## 它显示了什么？

scene 演示了如何编写支持 BatchRendererGroup 和 Entities Graphics 的自定义 HLSL 着色器。这
示例着色器可以在 `CustomDotsInstancingShader.shader` 资源中找到。scene 包含两套 Entities
其颜色要么来自其自己的材料（顶行），要么来自附加到的 MaterialColor component
MeshRenderers（底行）。所有的 entities 使用相同的 CustomDotsInstancingShader，因此所有八个对象
使用 DOTS_INSTANCING_ON 在一批 SRP 中进行渲染。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，确保子场景已关闭
2. 进入播放模式
3. 转到：**窗口 > 分析 > 帧调试器**
4. 在帧调试器中，单击启用
5. 选择 **DrawOpaqueObjects** 事件并将其展开
6. 观察到只有一个 SRP Batch，并且绘制调用的数量为 8。关键字显示 DOTS_INSTANCING_ON