# AmbientAndBlendProbes

此示例演示了环境和混合探头如何在 subscene 中工作。

<img src="../../../../READMEimages/AmbientAndBlendProbes.PNG" width="600">

## 它显示了什么？

AmbientAndBlendProbes scene 显示如何使用 `Light Probes`。有 2 个游戏对象与 2 个 entities 配对，在 `MeshRenderer.Probes.LightProbes` component 配置中具有相同的设置。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，请注意有 2 个 **SphereAmbient** 和 2 个 **SphereBlend** 对象。subscene 外部 2 个，内部 2 个
2. 选择一个并转到检查员
3. 在“网格渲染器 - 探针 - 光探针”下，请注意，根据所选对象，设置设置为“关闭”或“混合探针”

## 更多信息

有关混合探针的更多信息，请参阅[使用反射探针](https://docs.unity3d.com/Manual/UsingReflectionProbes.html) 文档。
