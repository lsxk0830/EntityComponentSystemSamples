# HelloNetcode 禁用 Bootstrap

安装 Entities package 时，它会通过其自定义引导自动创建“默认 World”（请参阅​​ `Unity.Entities.AutomaticWorldBootstrap` 类），
这样加载的 scenes 中的 authoring components 就可以通过快速路径注入（进入 Play Mode 后）。

类似地，Netcode for Entities 会覆盖它（通过 `ClientServerBootstrap` 实现 `ICustomBoostrap`）以默认创建两个 worlds；
[client world 和 server world](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/client-server-worlds.html)。
每个都会自动注入适当的 authoring 数据，
[这就是它们在启动时自动连接的方式](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html#connection-flow)。

## 要求

在某些情况下，不希望在启动时自动创建 worlds（e.g。当启动到 UI 前端，而不是游戏 scene 时）。
因此，netcode 提供了几种禁用自动 Entities 引导的方法：
1. 项目范围：通过继承 `ClientServerBootstrap` 来实现 `ICustomBoostrap`。有关示例，请参阅 [Bootstrap/FrontendBootstrap.cs](../01_BootstrapAndFrontend/Bootstrap/FrontendBootstrap.cs) 文件。
2. 项目范围：创建一个 `NetcodeConfig`，将其 `EnableClientServerBootstrap` 枚举设置为 `EnableBootstrapSetting.DisableAutomaticBootstrap`，然后通过“Netcode for Entities”项目设置将此 `ScriptableObject` 设置为默认值。
3. Per-Scene 覆盖：将 `OverrideAutomaticNetcodeBootstrap` 添加到活动 scene 中的根 ZXQOZUDYKTF​​WVESZXQ，并将其字段设置为 `EnableBootstrapSetting.DisableAutomaticBootstrap`。

>[!NOTE]
> 如果通过 `NetcodeConfig` 项目设置在项目范围内禁用，则每个 scene 覆盖 `OverrideAutomaticNetcodeBootstrap` 也可用于有选择地重新启用引导。

## 遵守用户代码 `ICustomBootstrap`/`ClientServerBootstrap` 实现中的 `EnableBootstrapSetting.DisableAutomaticBootstrap` 覆盖

如果您编写自己的 `ICustomBoostrap` 实现，它将**不会**自动尊重 `OverrideAutomaticNetcodeBootstrap` 或任何 `NetcodeConfig.Global` 设置。
通过您自己的引导程序对 query 这两个设置进行调用，如下所示：

```csharp
    // The preserve attribute is required to make sure the bootstrap is not stripped in il2cpp builds with stripping enabled.
    [UnityEngine.Scripting.Preserve]
    // The bootstrap needs to extend `ClientServerBootstrap`, there can only be one class extending it in the project.
    public class MyGameCustomBootstrap : ClientServerBootstrap
    {
        // The initialize method is what Entities calls to create the default worlds.
        public override bool Initialize(string defaultWorldName)
        {
            // If the user added an `OverrideDefaultNetcodeBootstrap` MonoBehaviour to their active scene,
            // or disabled Bootstrapping project-wide via a `NetcodeConfig.Global`, we should respect that here.
            if (!DetermineIfBootstrappingEnabled())
                return false;

            ...
        }
    }
```

或者，您可以通过调用 `DiscoverAutomaticNetcodeBootstrap` 仅为 `OverrideAutomaticNetcodeBootstrap` 提供 query，如果找到，则返回 `MonoBehaviour`（如果没有，则返回 `null`）。

## 示例描述

此 DisableBootstrap 示例利用 `OverrideAutomaticNetcodeBootstrap` 方法专门为此 scene 禁用引导。
I.e。您可以观察到 - 从 [DisableBootstrap scene](DisableBootstrap.unity) 输入 Play Mode - 没有创建Netcode worlds。
我们建议通过 [Netcode for Entities PlayMode 工具窗口](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/playmode-tool.html) 查看和调试Netcode worlds（及其连接），该窗口还包含许多其他Netcode配置选项，包括进一步的引导自定义。
