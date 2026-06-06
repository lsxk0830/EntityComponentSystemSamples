# Entities baking 示例

## AutoAuthoring

一种方便创建 authoring components 的解决方案，只需将 authoring MonoBehaviour 的每个字段复制到 IComponentData 的相应字段即可。

## BakingDependencies 示例

此示例演示了 baker 和 baking system 如何对 authoring 数据所做的更改做出反应。

`ImageGeneratorAuthoring` component 引用图像和 `ScriptableObject` 资源，其中包含浮点值、网格和材质。在 baking 期间，此 component 为图像中的每个像素生成一个图元并相应地设置颜色。

修改 authoring component 的任何字段都将在 subscene 中重新烘焙必要的 GameObjects。例如，修改 float 字段将重新烘焙两个 GameObjects （因为它们都使用该资源），但修改“hello.png”图像只会重新烘焙依赖于它的一个 GameObject。

## BakingTypes 示例

此示例在运行时不执行任何操作，但它使用 baking 在每组立方体周围创建一个边界框（使 Gizmos 能够看到白色调试线）。当您在 Scene 窗口中拖动立方体时，您将看到边界框在拖动时更新，因为在编辑 subscene 时会重新触发 baking。

## BlobAssetBaker 示例

此示例在 Baking 期间创建 [BlobAsset](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/blob-assets-concept.html)。在运行时，存储在 BlobAsset 中的动画曲线用于为立方体的 y 位置设置动画。

## BlobAssetBakingSystem 示例

此示例演示如何使用 baking systems 以高效且可扩展的方式烘焙 BlobAssets。在代码中：

- subscene 包含 256 个 GameObjects，分为四种类型：胶囊、立方体、圆柱体和球体。
- subscene 中的每个 GameObject 都有一个 `MeshBBAuthoring` component，它定义了我们要存储在 Blob 资产中的信息。
- `MeshBBAuthoring` baker 将网格顶点存储在 `BakingType` 缓冲区中，并将附加信息存储在 `BakingType` component 中。
- 在编辑模式下更新的 `MeshBBRenderSystem` 使用 blob 资源在 256 个烘焙的 entities 周围绘制调试边界框。

`ComputeBlobAssetSystem` BakingSystem 的设置分为三个主要步骤：

1. 尚未存在于 BlobAssetStore 中的 BlobAssets 将添加到列表中进行处理。
2. 唯一的 BlobAssets 通过其哈希值进行标识。
3. `BlobAssetReference` 存储在 entity components 中。

请注意，bakers 跟踪 BlobAssets 被 entities 引用，并相应地更新 BlobAssetStore。但是，在 *baking system* 中创建的 BlobAssets 不会自动跟踪，因此 baking system 必须手动检查 entities 参考是否不同 BlobAssets 与上次烘焙相比并手动更新 BlobAssetStore。如果完全删除 Entity，则 baking systems 必须清除 entity 可能引用的任何烘焙 BlobAssets。

## PrefabReference 示例

此示例演示如何使用 `EntityPrefabReference`。虽然每个 SubScene 中的 baking 和 prefab 直接为每个 SubScene 创建一个烘焙的 entity，但 `EntityPrefabReference` 允许多个 SubScenes 引用单个烘焙的 entity。prefab entity。