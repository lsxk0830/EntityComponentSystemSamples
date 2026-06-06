# Systems

**system** 是属于 [world]() 的代码单元，并且在主线程上运行（通常每帧运行一次）。通常，system 只会访问其自己的 world 的 entities，但这不是强制限制。

&#x1F579; *[参见示例 systems](../Assets/ExampleCode/ComponentsSystems.cs)。*

system 被定义为实现 [`ISystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystem.html) 接口的结构体，该结构体具有三个关键方法：

| **`ISystemState` 方法** | **描述** |
|---|---|
| [`OnUpdate()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystem.OnUpdate.html) | 通常每帧调用一次，但这取决于 system 所属的 `SystemGroup`。 |
| [`OnCreate()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystem.OnCreate.html) | 在第一次调用 `OnUpdate` 之前以及每当 system 恢复运行时调用。 |
| [`OnDestroy()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystem.OnDestroy.html) | 当 system 被销毁时调用。 |

system 还可以实现 `ISystemStartStop`，它具有以下方法：

| **`ISystemStartStop` 方法** | **描述** |
|------|------|
| [`OnStartRunning()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystemStartStop.OnStartRunning.html) | 在第一次调用 `OnUpdate` 之前以及 system 的 [`Enabled`](xref:Unity.Entities.ComponentSystemBase.Enabled) 属性从 `false` 更改为 `true` 后调用。 |
| [`OnStopRunning()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISystemStartStop.OnStopRunning.html) | 在 `OnDestroy` 之前以及 system 的 [`Enabled`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.Enabled.html) 属性从 `true` 更改为 `false` 后调用。 |

<br>

## System 组和 system 更新顺序

world 的 systems 被组织成 **system 组**。每个 system 组都有一个由 systems 和其他 system 组组成的有序列表作为其子组，因此 system 组形成一个层次结构，该层次结构决定了更新顺序。system 组定义为继承自 [`ComponentSystemGroup`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentSystemGroup.html) 的类。

当更新 system 组时，该组通常按排序顺序更新其子组，但可以通过覆盖组的更新方法来覆盖此默认行为。

每次在组中添加或删除子项时，组的子项都会重新排序。

[`[UpdateBefore]`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.UpdateBeforeAttribute.html) 和 [`[UpdateAfter]`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.UpdateAfterAttribute.html) 属性可用于确定组中子项之间的相对排序顺序。例如，如果 *FooSystem* 具有属性 `UpdateBefore(typeof(BarSystem))]`，则 *FooSystem* 将按排序顺序放置在 *BarSystem* 之前。但是，如果 *FooSystem* 和 *BarSystem* 不属于同一组，则该属性将被忽略。如果组的子项的排序属性产生矛盾（*e.g.* *A* 被标记为在 *B* 之前更新，但 *B* 也被标记为在 *A* 之前更新），则在对组的子项进行排序时会引发异常。

<br>

## 创建 worlds 和 systems

默认情况下，自动引导过程会创建一个具有三个 system 组的默认 world：

- `InitializationSystemGroup`，在 Unity 播放器循环的 `Initialization` 阶段结束时更新。
- `SimulationSystemGroup`，在 Unity 播放器循环的 `Update` 阶段结束时更新。
- `PresentationSystemGroup`，在 Unity 播放器循环的 `PreLateUpdate` 阶段结束时更新。

自动引导会创建每个 system 和 system 组的实例（具有 [`[DisableAutoCreation]`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DisableAutoCreationAttribute.html) 属性的组除外）。这些实例将添加到 `SimulationSystemGroup` 中，除非被 [`[UpdateInGroup]`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.UpdateInGroupAttribute.html) 属性覆盖。例如，如果 system 具有属性 `UpdateInGroup(typeof(InitializationSystemGroup))]`，则 system 将添加到 `InitializationSystemGroup` 而不是 `SimulationSystemGroup`。

可以使用脚本定义禁用自动引导过程：

|**脚本定义**|**描述**|
|---|---|
|`#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD`| 禁用默认 world 的自动引导。 |
|`#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_EDITOR_WORLD`| 禁用 Editor world 的自动引导。 |
|`#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP`| 禁用默认 world 和 Editor world 的自动引导。 |

当自动引导被禁用时，您的代码负责：

- 创建您需要的任何 worlds。
- 调用 [`World.GetOrCreateSystem<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.World.GetOrCreateSystem.html) 将 system 和 system 组实例添加到 worlds。
- 注册顶级 system 组（如 `SimulationSystemGroup`）以在 Unity [PlayerLoop](https://docs.unity3d.com/ScriptReference/LowLevel.PlayerLoop.html) 中更新。

或者，可以通过创建实现 [`ICustomBootstrap`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICustomBootstrap.html) 的类来自定义自动引导。

<br>

## worlds 和 systems 的时间

world 具有 `Time` 属性，该属性返回 [`TimeData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Core.TimeData.html) 结构体，其中包含帧增量时间和已用时间。时间值由 world 的[`UpdateWorldTimeSystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.UpdateWorldTimeSystem.html) 更新。可以使用以下 `World` 方法来操作时间值：

|**`World` 方法**|**描述**|
|---|---|
| [`SetTime`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.World.SetTime.html) | 设置时间值。 |
| [`PushTime`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.World.PushTime.html) | 暂时更改时间值。 |
| [`PopTime`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.World.PopTime.html) | 恢复上次推送之前的时间值。 |

某些 system 组（例如 [`FixedStepSimulationSystemGroup`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.FixedStepSimulationSystemGroup.html)）会在更新其子项之前推送时间值，然后在更新完成后弹出该值。

<br>

## SystemState

system 的 `OnUpdate()`、`OnCreate()` 和 `OnDestroy()` 方法会传递 [`SystemState`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.html) 参数。`SystemState` 表示 system 实例的状态，具有重要的方法和属性，包括：

|**方法或属性**|**描述**|
|---|---|
| [`World`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.World.html) | system 的 world。 |
| [`EntityManager`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.EntityManager.html) | system 的 `EntityManager` 的 world。 |
| [`Dependency`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.Dependency.html) | `JobHandle` 用于在 systems 之间传递 job 依赖关系。 |
| [`GetEntityQuery()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.GetEntityQuery.html) | 返回 `EntityQuery`。 |
| [`GetComponentTypeHandle<T>()`](ZXQ 索加迪 QVYQXZXQ) | 返回 `ComponentTypeHandle<T>`。 |
| [`GetComponentLookup<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.GetComponentLookup.html) | 返回 `ComponentLookup<T>`。|

| ⚠ IMPORTANT |
| :- |
| 虽然 entity 查询、component 类型句柄和 component 查找可以直接从 `EntityManager` 获取，但 system 通常只从 `SystemState` 获取这些内容是正确的。通过通过 `SystemState`，访问的 component 类型由 system 进行跟踪，这对于 `Dependency` 属性正确传递 job 之间的 systems 依赖关系至关重要。*[查看有关访问 entities 的 jobs 的更多信息](./entities-jobs.md)。* |

<br>

## SystemAPI

[`SystemAPI`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemAPI.html) 类具有许多静态便捷方法，涵盖与 `World`、`EntityManager` 和 `SystemState` 相同的大部分功能。

`SystemAPI` 方法依赖于[源生成器](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)，因此它们仅适用于 systems 和 `IJobEntity`（但不适用于 `IJobChunk`）。使用 `SystemAPI` 的优点是这些方法在两个上下文中产生相同的结果，因此使用 `SystemAPI` 的代码通常更容易在这两个上下文之间复制粘贴。

| &#x1F4DD; NOTE |
| :- |
| 如果您对在哪里查找关键 Entities 功能感到困惑，一般规则是首先检查 `SystemAPI`。如果 `SystemAPI` 没有您要查找的内容，请查找 `SystemState`，如果您要查找的内容不存在，请查找 `EntityManager` 和 `World`。 |

`SystemAPI` 还提供了一种特殊的 [`Query()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemAPI.Query.html) 方法，该方法通过源代码生成，有助于方便地在 entities 和 components 上创建与 query 匹配的 foreach 循环。

<br>