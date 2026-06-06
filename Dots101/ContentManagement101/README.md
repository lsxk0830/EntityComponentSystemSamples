<img src="Images/Header.gif" alt="header" height="500"/>

# 内容管理

Entities package 的内容管理 API 允许您在 runtime.<br> 下载、加载和卸载 Unity 资产和子 scenes
此示例项目演示了一些用例所需的基本 APIs 弱引用：

- 通过**本地内容**工作流程加载备用网格和材质。
- 展示使用**本地和远程内容管道**在运行时下载、加载和卸载 scenes。

    | 内容类型          | 优点                                                                                           | 缺点                                                                                                   |
    |-----------------------|------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------|
    | **本地内容**     | - 即使远程内容为 unreachable.<br> 也提供后备 - 无需预加载。               | - 增加整体构建 size.<br> - 即使更新单个参考也需要重建整个播放器。    |
    | **远程内容**    | - 减少构建 size.<br> - 允许更新内容而不重建播放器。                    | - 没有后备文件，除非以前是 cached.<br> - 使用前需要初始下载。                     |
    |                                                                                                                                                                                                                                                |


有关更多详细信息，请参阅有关[内容管理](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/content-management.html) 的 Entities 文档。

## 入门
- 请参阅 [ProjectVersion.txt](./ProjectSettings/ProjectVersion.txt) 了解支持的最低 Unity version.<br>
    该项目当前的目标是 [Unity 6](https://unity.com/releases/editor/whats-new/6000.2.6#installs)。
- 打开 1. WeakObjectLoading/WeakObjectLoading.scene 文件开始探索该项目。
- 导航到 **文件 > 构建配置文件**，或按 **Ctrl + Shift + B / Cmd + Shift + B**，以**激活** [平台] WeakObjectLoading 或 [平台] WeakSceneLoading 构建配置文件。
---

| 部分 | 描述 |
|---------|-------------|
| [运行和构建示例 Scenes](#running-and-building-the-sample-scenes) | 在 Unity Editor 中切换构建配置文件和 scenes 的说明。 |
| [脚本定义符号](#scripting-define-symbols-for-content-management) | 通过项目设置启用内容交付和调试符号的步骤。 |
| [强引用 vs 弱引用](#strong-vs-weak-references) | 强引用与弱引用及其运行时行为的解释。 |
| [弱引用对象](#weakly-referencing-an-object) | 使用 `WeakObjectReference<T>` 和 `UntypedWeakReferenceId` 在运行时加载网格和材质的指南。 |
| [WeakObjectReference](#weakobjectreference) | 用于检查和加载类型化弱引用的代码片段。 |
| [UntypedWeakReferenceId](#untypedweakreferenceid) | 用于检查和加载非类型化弱引用的代码片段。 |
| [弱引用 Scene](#weakly-referencing-a-scene) | 通过具有本地或远程内容选项的弱引用加载 subscenes 的概述。 |
| [加载远程子场景的步骤（选项 1）](#steps-to-load-remote-subscenes-option-1) | 使用本地打包的 subscene 参考逐步构建播放器。 |
| [Unity Editor 中的行为](#behavior-in-the-unity-editor) | Editor 中低保真和高保真 scene 切换的说明。 |
| [弱 Scene 参考](#weak-scene-references) | 使用 `UntypedWeakReferenceId` 和 `WeakObjectSceneReference` 来弱引用 scenes。 |
| [构建播放器](#building-the-player) | 在本地内容的播放器构建中包含 subscene 引用的说明。 |
| [加载远程子场景的步骤（选项 2）](#steps-to-load-remote-subscenes-option-2) | 通过构建单独的 subscene 引用来远程加载内容的说明。 |
| [构建内容目录](#build-content-catalog) | 如何使用 `RemoteContentCatalogBuildUtility` 生成和发布内容目录。 |
| [StreamingAssets 路径](#streamingassets-paths-in-unity) | 针对 Windows、macOS、Android 和 iOS 存储 `StreamingAssets` 的平台特定路径。 |
| [从 Web 提供目录 Server](#serving-a-catalog-from-a-web-server) | 从远程 Web server 提供内容目录以进行运行时加载的说明。 |

## 运行并构建示例 scenes

在编辑器中运行或构建每个示例 scene 之前，您可能需要切换活动构建配置文件。例如，在运行 WeakObject scene 之前，您应该：

1. 打开文件 > 构建配置文件菜单
1. 将活动构建配置文件设置为“Windows WeakObject”（如果您使用的是 Mac，则为“Mac WeakObject”）。

## 脚本定义内容管理符号

要在项目中启用内容交付，项目必须具有脚本定义符号 `ENABLE_CONTENT_DELIVERY`。<br>
对于更详细的内容管理调试消息，您还可以添加脚本定义符号 `ENABLE_CONTENT_DIAGNOSTICS` 和 `ENABLE_CONTENT_BUILD_DIAGNOSTICS`.<br>
设置或删除定义符号：

1. 转到 **编辑 > 项目设置 > 播放器**。
1. 在边栏中选择**玩家**。
1. 导航到 **其他设置 > 脚本编译**。

每个配置文件资源都包含一个脚本定义符号部分，您可以在其中直接设置与该配置文件关联的脚本定义。

<img src="Images/scripting-define-symbols.png" alt="define-scripting-symbols"  width="600"/>

有关脚本定义的更多信息，请参阅[自定义脚本符号](https://docs.unity3d.com/Manual/custom-scripting-symbols.html)。

## 强引用与弱引用

当资产直接分配给 GameObject component 的属性时，这会创建“强引用”。例如，如果将网格过滤器 component 设置为直接引用网格资源，则网格资源将被网格 filter.<br> 强引用
在运行时，Unity 在需要时自动加载强引用对象，并在不再需要时卸载它们。实际上，当您*通过*强引用访问对象时，您可以假设它始终位于内存中并准备好 use.<br>
例如，下面的 subscene 中的所有资产都被强引用：

<img src="Images/strongly-reference.png" desc="The assets in this subscene are strongly referenced." width="600"/>

相反，当您通过弱引用访问对象时，该对象可能不一定已加载并准备好用于 use.<br>
因此，您必须始终检查弱引用对象是否确实已加载并准备好，如果没有，则必须 trigger 加载并等待其准备好，然后再使用 it.<br>

尽管弱引用会带来更多麻烦，但它们提供了更大的灵活性。例如，弱引用可以指向尚未从磁盘加载或者甚至尚未下载到本地 system 的资产。<br>
内容管理 API 允许您使用弱 references.<br> 来引用未包含在构建中的资源

## 弱引用对象

*WeakObjectLoading* scene 演示了如何通过弱引用加载对象。当您播放 scene 时，system 会从本地 path.<br> 加载弱引用网格体和材质
这是通过调用 `RuntimeContentSystem.LoadContentCatalog(null, null, null, true)` （[参见 WeakObjectLoadingSystem.cs](Assets/1.%20WeakObjectLoading/WeakObjectLoadingSystem.cs)）来完成的，它在运行时从 `StreamingAssets` 文件夹下载所有资源引用，并在加载完成后创建渲染的 entities。

要弱引用网格和材质等对象，我们需要：

- `UntypedWeakReferenceId`，存储 Unity 对象的全局 id
-  `WeakObjectReference<T>`，`UntypedWeakReferenceId` 的包装器，其中 T 是 Unity 对象的类型

一般来说，`WeakObjectReference<T>` 是首选，除非我们需要一个可以改变引用对象类型的弱引用。该示例演示了两者的使用。

在 `WeakObjectLoading.unity` scene 中，一棵树 GameObject 和房子 GameObject 都有 [WeakRenderedObjectAuthoring](Assets/1.%20WeakObjectLoading/WeakRenderedObjectAuthoring.cs) component。如果设置了 authoring component 的 `UseUntypedId` 标志，则其 baker 添加：

- `WeakMeshUntyped`，一个 `IComponentData`，存储网格的 `UntypedWeakReferenceId`
- `WeakMaterialUntyped`，存储材质 `UntypedWeakReferenceId` 的 `IBufferElementData`

如果未设置 `UseUntypedId` 标志，则 baker 会添加：

- `WeakMesh`，存储 `WeakObjectReference<Mesh>` 的 `IComponentData`
- `WeakMaterial`，存储 `WeakObjectReference<Material>` 的 `IBufferElementData`

> NOTE：材质存储在动态缓冲区中，因为单个渲染网格可能具有多种材质，如本例中的树和房子的情况。

[WeakObjectLoadingSystem](Assets/1.%20WeakObjectLoading/WeakObjectLoadingSystem.cs) system 查找这些弱引用 components，加载弱引用资源，然后添加 components 以使 entity 可渲染。<br>
在 system 的 `OnUpdate` 中，这里是处理类型化弱引用的代码，请查看 system 中的 `WeakObjectReference` 和 `UntypedWeakReferenceId` 区域以验证不同的用途。

### WeakObjectReference
```csharp
#region WeakObjectReference
// mesh load status
var meshStatus = mesh.Value.LoadingStatus;
if (meshStatus == ObjectLoadingStatus.None)
{
    Debug.Log("Initiate mesh LOAD");
    mesh.Value.LoadAsync(); // trigger load
}
if (meshStatus != ObjectLoadingStatus.Completed)
{
    loaded = false;
}
#endregion
```

### UntypedWeakReferenceId
```csharp
#region WeakObjectReference
// mesh load status
var meshStatus = RuntimeContentManager.GetObjectLoadingStatus(mesh.Value);
if (meshStatus == ObjectLoadingStatus.None)
{
    RuntimeContentManager.LoadObjectAsync(mesh.Value);  // trigger load
}
if (meshStatus != ObjectLoadingStatus.Completed)
{
    loaded = false;
}
#endregion
```

> **注意**，这使用本地设置，它会自动从构建过程中生成的 StreamingAssets 文件夹中检索资源并在运行时加载它们。<br>
> 有关更多信息，请查看本节的[文档](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-load-an-object.html)。

## 弱引用 scene

*WeakSceneLoading* 示例 scene 演示了如何通过弱引用加载 scenes。
此外，还有两种途径可以实现此目的：

- **选项 1：** 正常构建项目，并在项目构建期间将所有引用打包到 `StreamingAssets` 文件夹内。
- **选项 2：** 构建单独的 subscene 引用以远程下载内容。

### 加载远程子场景的步骤（选项 1）
1. **配置目标：**
   导航至
   ```
   /Assets/2. WeakSceneLoading/Content Settings/WeakSceneList.asset
   ```
   添加所需的 subscenes 并将其 `Content Source` 参数设置为 **Local**。这会将正确的 component 分配给 Scene 中的 Entity。

2. **定位本地内容目录：**
   与 WeakObject 示例类似，调用
   ```csharp
   RuntimeContentSystem.LoadContentCatalog(null, null, null, true);
   ```
   在项目中，一旦附加了 `LocalContent` component 的 entity，脚本 [`Assets/1. WeakObjectLoading/LoadingLocalCatalogSystem.cs`] 就会自动执行此操作。

---

### Unity Editor 中的行为

当您播放示例 scene 时，一旦 `LoadingLocalCatalogSystem` 将目录源设置为 **local** 并创建了 `ContentReady`，scene 就会由 `WeakSceneLoadingSystem` 加载环境 scene `Assets/2. WeakSceneLoading/Subscenes/LowFidelitySubscene.unity` 的低保真版本 entity.<br> 按 **Enter** 可在 scene 的低保真版本和高保真版本之间切换：`Assets/2. WeakSceneLoading/Subscenes/HighFidelitySubscene.unity`

> **注：** 低保真和高保真 scenes 均经过烘焙 entity subscenes。但是，此处介绍的解决方案也适用于弱引用的常规 GameObject scenes。

---

### 弱 Scene 参考文献

要弱引用 scene，您可以使用：

- `UntypedWeakReferenceId` — 存储 Unity 对象的全局 ID，例如 scene 或网格。
- `WeakObjectSceneReference` — `UntypedWeakReferenceId` 的包装器，专门用于 scenes。

一般来说，除非需要不同对象类型的弱引用，否则首选 `WeakObjectSceneReference`。
此示例演示了 `UntypedWeakReferenceId` 和 `WeakObjectSceneReference` 的使用。

在[`WeakSceneAuthoring.cs`](Assets/2.%20WeakSceneLoading/WeakSceneAuthoring.cs) 中，baker 添加了一个名为 `HighLowWeakScene` 的 `IComponentData`，其中存储：

- `UntypedWeakReferenceId` 中的高保真 scene
- `WeakObjectSceneReference` 中的低保真 scene

当在构建中弱引用 scene 时，原始 GameObject scene 及其资产仍包含在构建中。为了在构建中包含烘焙的 entity scenes，我们将它们作为 subscenes 嵌套在另一个 scene“ContainerScene”中，我们将其包含在示例构建配置文件的 scene 列表中。

---

### 构建播放器

构建播放器时，请确保包含 subscene 引用。
在示例中，有一个名为 `ContainerScene` 的 scene，它引用了 `LowFidelitySubscene` 和 `HighFidelitySubscene`。

在构建过程中，Unity 包含这些引用并将它们打包到播放器的 **StreamingAssets** 文件夹中。

<img src="Images/build-profile.png" desc="The scene contains needed subscenes to be built in the player." width="600"/>

### 加载远程子场景的步骤（选项 2）

在加载远程 subscenes 之前，请执行以下步骤：

1. **设置脚本定义：**
   在构建配置文件中定义[脚本定义内容管理符号](#scripting-define-symbols-for-content-management)。
2. **配置目标：**
   导航至
   ```
   /Assets/2. WeakSceneLoading/Content Settings/WeakSceneList.asset
   ```
   添加所需的 subscenes 并将其 `Content Source` 参数设置为 **Remote**，并在 Editor 中保持选中状态。
3. **建立内容目录：**
   打开菜单 `Assets > Publish > Publish Catalog from a WeakSceneListScriptableObject` 并构建内容。

4. **定位远程内容目录：**
   项目中脚本[`Assets/2. WeakSceneLoading/LoadingRemoteCatalogSystem.cs`]根据 parameters.<br>初始化内容
   请仔细查看[LoadingRemoteCatalogSystem](Assets/2.%20WeakSceneLoading/LoadingRemoteCatalogSystem.cs) 以了解如何使用它。

---

### 建立内容目录
由于内容管理 API 依赖于 subscenes，因此您可以直接从它们创建内容和引用。
请查看 [`ContentBuilder`](Assets/2.%20WeakSceneLoading/Content%20Settings/Editor/ContentBuilder.cs) 脚本以了解上述内容构建过程的工作原理。

根据需要填写以下函数参数即可生成内容：

```csharp
// Builds the subscenes and stores them in tempPath
RemoteContentCatalogBuildUtility.BuildContent(
    subSceneGuids,
    playerGuid,
    EditorUserBuildSettings.activeBuildTarget,
    tempPath
);

// Copies from tempPath to the target folder and renames assets using their content hashes
RemoteContentCatalogBuildUtility.PublishContent(
    tempPath,
    contentPath,
    f => new string[] { contentSetName }
);
```

---

### 弱 Scene 参考文献

[WeakSceneLoadingSystem](Assets/2.%20WeakSceneLoading/WeakSceneLoadingSystem.cs) system 最初将加载低保真 scene：

```csharp
var weakScene = SystemAPI.GetSingletonRW<WeakSceneRefs>();

var sceneParams = new Unity.Loading.ContentSceneParameters
{
    loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Additive,
    autoIntegrate = true,
};

// initial load of the low-fidelity scene
// (we store the loaded Scene object
// so that we can later unload the scene)
weakScene.ValueRW.Scene =
    weakScene.ValueRW.LowSceneRef.LoadAsync(sceneParams);
```

当用户按下回车键时，system 卸载当前的 scene 并加载其他 scene：

```csharp
// toggle between the two scenes
if (weakScene.ValueRO.IsHighLoaded)
{
    // unload high fidelity
    RuntimeContentManager.UnloadScene(ref weakScene.ValueRW.Scene);

    // load low fidelity
    weakScene.ValueRW.Scene =
        weakScene.ValueRW.LowSceneRef.LoadAsync(sceneParams);

    weakScene.ValueRW.IsHighLoaded = false;
}
else
{
    // unload low fidelity
    // (UntypedWeakReferenceId does not have this Unload method, but
    // this method simply calls RuntimeContentManager.UnloadScene,
    // so you could alternatively call it yourself directly)
    weakScene.ValueRO.LowSceneRef.Unload(ref weakScene.ValueRW.Scene);

    // load high fidelity
    weakScene.ValueRW.Scene = RuntimeContentManager.LoadSceneAsync(
        weakScene.ValueRO.HighSceneRef, sceneParams);

    weakScene.ValueRW.IsHighLoaded = true;
}
```
---
### 构建远程播放器

与[本地内容构建](#steps-to-load-remote-subscenes-option-1) 不同，播放器**不需要**需要包含在[目录构建过程](#build-content-catalog) 期间已打包的 subscene 引用。

<img src="Images/build-profile-remote.png" desc="The scene contains needed subscenes to be built in the player." width="600"/>

> **笔记：**
> 以这种方式构建会导致玩家构建尺寸更小。
> 但是，如果远程路径变得无法访问，则不会有后备数据。
> 为了确保内容始终可以加载，无论远程源是否可访问，请使用引用 scene 将内容嵌入到 `StreamingAssets` 中，类似于[本地内容方法](#building-the-player)。
> 否则，如果您优先考虑构建大小，请采用[远程内容部分](#building-the-player-for-remote) 中解释的方法。


## 在运行时下载并加载 Scenes

构建播放器后，所有依赖项数据、对象引用和 scene 引用都存储在 `StreamingAssets` 文件夹中（在示例中也称为 **本地内容**）。<br>The 该文件夹的确切路径因平台而异，但它始终作为播放器的一部分包含在内 build.<br>
在此文件夹内，有一个生成的文件夹列表，类似于以下内容：

<img src="Images/streamingassets.png" desc="The scene contains needed subscenes to be built in the player." width="600"/><br>
<img src="Images/local-content-archives.png" desc="The scene contains needed subscenes to be built in the player." width="600"/><br>
<img src="Images/local-content-entitiescenes.png" desc="The scene contains needed subscenes to be built in the player." width="600"/><br>


---

如果项目配置为使用远程内容，则生成的文件夹将类似于以下内容：

<img src="Images/catalog.png" desc="The scene contains needed subscenes to be built in the player." width="600"/><br>

### StreamingAssets Unity 中的路径

本文档概述了在构建播放器后，Unity 为每个支持的平台放置 **`StreamingAssets`** 文件夹的位置。

- **Windows（独立播放器）**
  ```
  <BuildFolder>/<GameName>_Data/StreamingAssets/
  ```

- **macOS（独立播放器）**
  ```
  <AppName>.app/Contents/Resources/Data/StreamingAssets/
  ```

- **安卓**
  Unity 将 `StreamingAssets` 打包到 APK 中的 **`assets`** 文件夹中。
  ```
  assets/
  ```
    运行时访问路径（只读）：
    ```
    jar:file://<path_to_apk>!/assets/
    ```
    当使用 Unity 的 API 时：
    ```csharp
    Application.streamingAssetsPath
    // Typically resolves to:
    // jar:file:///data/app/com.company.mygame/base.apk!/assets
    ```

- **iOS**
  ```
  <AppName>.app/Data/Raw/
  ```
    运行时访问路径（通过 API）：
    ```csharp
    Application.streamingAssetsPath
    // Typically resolves to:
    // /var/containers/Bundle/Application/<GUID>/MyProject.app/Data/Raw
    ```

---
>注意：请查看[后处理构建脚本](Assets/2.%20WeakSceneLoading/Content%20Settings/Editor/BuildPostProcessing.cs)(`Assets\2. WeakSceneLoading\Content Settings\Editor\BuildPostProcessing.cs`)。
>它包括一个简单示例，说明如何根据 Catalog 和 StreamingAsset 文件夹过滤和删除重复的依赖项。

### 从网络提供目录 server

在实际用例中，目录并不存储在与播放器相同的本地计算机上，而是通过 Web server 提供。例如，使用 Python，这就像在 server 上运行 `python3 -m http.server --directory /path/to/your/catalog 8000` 一样简单。在您的 Unity 项目中，然后将 server 的 URI 而不是文件路径传递给 `RuntimeContentSystem.LoadContentCatalog`, *e.g.* `https://example.com:8000`.









