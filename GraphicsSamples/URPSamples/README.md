# Entities Graphics URP 示例项目
该项目包括 URP Entities Graphics 的功能示例 Scenes。

## 特征示例 Scenes
功能示例 Scenes 位于 _SampleScenes_ 文件夹中，根据主题分为不同的文件夹。这些 Scenes 中的大多数包括 SubScene 中的 GameObjects。

当对应的 entity 存在时，Unity 使用 Entities Graphics 渲染 GameObjects；当对应的 entity 不存在时，Entities Graphics 不渲染 Entities Graphics。对于 SubScene 中的 GameObjects，这意味着 Unity 始终用 Entities Graphics 渲染它们；在 Edit Mode、Play Mode 以及内置播放器中。

### Scene 列表

| Scene | 描述 | 截屏 |
| --- | --- | - |
| AmbientAndBlendProbes | 演示如何使用光探头 | ![](READMEimages/AmbientAndBlendProbes.PNG) |
| 光照贴图 | 演示对 Entities 的光照贴图支持 | ![](READMEimages/Lightmaps.PNG) |
| 光探针 | 演示 lightprobe 对 Entities 的支持 | ![](READMEimages/Lightprobes.PNG) |
| BuiltInMaterialSHProperties | 演示内置材质 SH 属性值的覆盖 | ![](READMEimages/BuiltInMaterialSHProperties.PNG) |
| MaterialOverridesSample | 演示无需编写代码即可覆盖材质属性的设置 | ![](READMEimages/MaterialOverridesSample.PNG) |
| ShaderGraphProperties | 演示 Entities 上 Shader Graph 着色器的材质属性覆盖 | ![](READMEimages/ShaderGraphProperties.PNG) |
| URPLitProperties | 演示 Entities 上不同 URP Lit 材料属性的材料属性覆盖 | ![](READMEimages/URPLitProperties.PNG) |
| HybridEntitiesConversion | 演示可以放入子场景中的图形相关伴侣 components | ![](READMEimages/HybridEntitiesConversion.PNG) |
| TriggerParticles | 演示如何从 ECS System 演奏 ParticleSystem | ![](READMEimages/TriggerParticles.PNG) |
| EntityCreation | 演示如何在 run 时间高效创建通过 Entities Graphics 渲染的 entities | ![](READMEimages/EntityCreation.PNG) |
| MaterialMeshChange | 演示如何在运行时更改 Entities 上的材质和网格 | ![](READMEimages/MaterialMeshChange.PNG) |
| RenderMeshUtilityExample | 演示新的 RenderMeshUtility.AddComponents API | ![](READMEimages/RenderMeshUtilityExample.PNG) |
| MeshDeformations | 演示 BlendShape 和 SkinWeight entities | ![](READMEimages/MeshDeformations.PNG) |
| SkinnedCharacter | 演示 SkinnedMeshRenderer entities | ![](READMEimages/SkinnedCharacter.PNG) |
| DisabledEntities |  演示禁用的 entities | ![](READMEimages/DisabledEntities.PNG) |
| LODs | 在 Entities Graphics 中演示 LODs | ![](READMEimages/LODs.PNG) |
| SimpleDotsInstancingShader | 演示使用 DOTS 实例化进行渲染的简单无光照着色器 | ![](READMEimages/SimpleDotsInstancingShader.PNG) |
| 子网格 | 演示使用具有多个子网格的网格和 Entities Graphics | ![](READMEimages/Submesh.png) |
| TransparencyOrdering | 演示透明的 entities 排序 | ![](READMEimages/TransparencyOrdering.PNG) |
