# Entities 流媒体示例

## AssetManagement 示例

此示例使用 `WeakObjectReference` 在运行时加载和卸载资源。

## BindingRegistry 示例

BindingRegistry 跟踪 GameObject component 字段和相应的 entity component 字段之间的关联。通过使用 `RegisterBinding` 属性，authoring components 可以与相应 entity components 的更改同步实时更新。（此效果仅在检查器窗口的“混合模式”中可见。）

## PrefabAndSceneReferences 示例

此示例使用 `EntityPrefabReference` 和 `EntitySceneReference` 在运行时加载 entity prefab和 entity scenes。在烘焙时，引用被分配为资产的 GUID，并且这些 GUID 在运行时解析。

## RuntimeContentManager 示例

此示例演示如何使用 [RuntimeContentManager](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/content-management.html) 在运行时加载和释放资源。

## SceneManagement 示例

这是一系列演示 scene 管理 API 的示例。

