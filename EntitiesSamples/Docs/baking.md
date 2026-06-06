# Baking 和 entity scenes

&#x1F579;  *[参见 baking 和 authoring components 的示例](../Assets/ExampleCode/Baking.cs)。*

**Baking** 是一个构建时过程，使用 **bakers** 和 **baking** 将 **sub scenes** 转换为 **entity scenes** systems**：

- **子 scene** 是 Unity scene 资产，由 [SubScene](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SubScene.html) MonoBehaviour 嵌入到另一个 scene 中。
- **entity scene** 是可在运行时加载的 entities 和 components 的序列化集。
- **baker** 是扩展 [`Baker<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.Baker-1.html) 的类，其中 T 是 MonoBehaviour。带有 Baker 的 MonoBehaviour 称为 **authoring component**。
- **baking system** 是标有 `[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]` 属性的普通 system。（Baking systems 是完全可选的，通常仅在高级用例中才需要。）

Baking 子 scene 通过几个主要步骤完成：

1. 对于子 scene 的每个 GameObject，创建对应的 entity。
2. 执行子 scene 中每个 authoring component 的 baker。每个 baker 都可以读取 authoring component 并将 components 添加到对应的 entity 中。
2. baking systems 执行。每个 system 都可以以任何方式读取和修改烘焙的 entities：设置 components，添加 components，删除 components，创建额外的 entities，或销毁 entities。与 bakers 不同，baking systems 不应访问子 scene 的原始 GameObjects。

修改后，会重新烘焙一个子 scene：

1. 仅重新执行读取修改后的 authoring components 的 bakers。
1. baking systems 始终完全重新执行。
1. 编辑模式或播放模式下的实时 entities 会更新以匹配 baking 的结果。（这是可能的，因为 baking 跟踪烘焙的 entities 与实时 entities 的对应关系。）

<br>

## 创建和编辑子 scenes

带有 `SubScene` MonoBehaviour 的 GameObject 有一个复选框，用于打开和关闭子 scene 进行编辑。当子 scene 打开时，其 GameObjects 会被加载并占用 Unity 编辑器中的资源，因此您可能需要关闭当前未编辑的子 scenes。

![](./images/open_subscene.png)

创建新子 scene 的便捷方法是在 Hierarchy 窗口中右键单击并选择 `New Subscene > Empty Scene...`。这将创建一个新的 scene 文件和一个 GameObject，其中 `SubScene` component 引用新的 scene 文件：

![](图片/create_subscene.png)

<br>

## 访问 baker 中的数据

增量 baking 需要 bakers 来跟踪它们读取的所有数据。baker 的 authoring component 的字段会自动跟踪，但 baker 读取的其他数据必须通过 `Baker` 方法添加到其依赖项列表中：

|**Baker 方法**|**描述**|
|---|---|
| [`GetComponent<T>()`]() | 访问子 Scene 中任何 GameObject 的任何 component。 |
| [`DependsOn()`]() | 跟踪此 `Baker` 的资产。 |
| [`GetEntity()`]() | 返回在子 scene 中烘焙的 entity 或从 prefab 烘焙的 entity 的 id。（entity 尚未完全烘焙，因此您不应尝试读取或修改 entity 的 components。） |

<br>

## 装卸 entity scenes

出于流式传输的目的，scene 的 entities 被分为由索引号标识的**部分**。entity 所属的段由其 [`SceneSection`]() 共享的 component 指定。默认情况下，entity 属于第 0 节，但这可以通过在 baking 期间设置 `SceneSection` 来更改。

| ⚠ IMPORTANT |
| :- |
| 在 baking 期间，子 scene 中的 entities 只能引用同一节或节 0 的其他 entities（这是一种特殊情况，因为节 0 总是在其他节之前加载，并且仅在 scene 本身卸载时*卸载*加载）。 |

当加载 scene 时，它由 entity 表示，其中包含有关 scene 的元数据，并且其每个部分也由 entity 表示。通过操作其 entity 的 [`RequestSceneLoaded`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html) component 来加载和卸载单个部分：`SceneSystemGroup` 中的 `SceneSectionStreamingSystem` 会响应此操作 component 更改。

[`SceneSystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SceneSystem.html) 包含用于加载和卸载 entity scenes 的静态方法：

|**`SceneSystem` 方法**|**描述**|
|---|---|
| [`LoadSceneAsync()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISharedComponent.html) | 启动 scene 的加载。返回表示加载的 scene 的 entity。 |
| [`LoadPrefabAsync()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 启动 prefab 的加载。返回引用加载的 prefab 的 entity。 |
| [`UnloadScene()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 销毁已加载的 scene 的所有 entities。 |
| [`IsSceneLoaded()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IComponentData.html) | 如果加载了 scene，则返回 true。 |
| [`IsSectionLoaded()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IBufferElementData.html) | 如果加载了某个部分，则返回 true。 |
| [`GetSceneGUID()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 返回表示 scene 资产（由其文件路径指定）的 GUID。 |
| [`GetScenePath()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 返回 scene 资产的路径（由其 GUID 指定）。 |
| [`GetSceneEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 返回表示 scene 的 entity（由其 GUID 指定）。 |

| ⚠ IMPORTANT |
| :- |
| Entity scene 和节加载始终是异步的，并且不能保证请求后需要多长时间才能加载数据。在大多数情况下，代码应该检查是否存在从 scenes 加载的特定数据，而不是检查 scenes 本身的加载状态。这种方法避免了将代码束缚到特定的 scenes：如果数据移动到不同的 scene、从网络下载或按程序生成，则代码仍将无需修改即可工作。 |

<br>














