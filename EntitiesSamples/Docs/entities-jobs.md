# 在 jobs 中访问 entities

&#x1F579;  *[参见示例 jobs](../Assets/ExampleCode/Jobs.cs)。*

您可以使用 [C# Job System](https://docs.unity3d.com/Manual/JobSystem.html) 将 entity 数据的处理卸载到工作线程。Entities package 有两个用于定义访问 entities 的 jobs 的接口：

- [`IJobChunk`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IJobChunk.html)，对于与 query 匹配的每个 chunk，都会调用一次其 `Execute()` 方法。
- [`IJobEntity`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IJobEntity.html)，对于每个与 ​​query 匹配的 entity entity 调用一次其 `Execute()` 方法。

虽然 `IJobEntity` 一般来说编写和使用起来更方便，但 `IJobChunk` 提供了更精确的控制。在大多数情况下，他们的表现在同等工作下是相同的。

| &#x1F4DD; NOTE |
| :- |
| `IJobEntity` 实际上并不是“真正的”job 类型：源代码生成使用 `IJobChunk` 的实现扩展了 `IJobEntity` 结构。所以事实上，`IJobEntity` 最终被安排为 `IJobChunk`。 |

在 job 中进行[结构更改](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html) 是不安全的，因此通常您应该只在主线程上进行结构更改。为了解决此限制，job 可以在 [`EntityCommandBuffer`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.html) 中记录结构更改命令，然后可以稍后在主线程上回放这些命令。

要跨多个线程拆分 `IJobChunk` 或 `IJobEntity` 的工作，schedule 通过调用 `ScheduleParallel()` 而不是 `Schedule()` 来实现 job。当您使用 `ScheduleParallel()` 时，与 query 匹配的 chunks 将被拆分为单独的批次，并且这些批次将被分配给工作线程。

<br>

## 同步点

**‘同步点’**操作是无法安全地与计划的 jobs 并发执行的操作，jobs 可能访问 entities 和 components，因此这些操作必须首先完成 jobs。例如，调用 `EntityManager.CreateEntity()` 将首先完成所有当前调度的 entities 和 components 的 jobs。同样，`EntityQuery` 方法 `ToComponentDataArray<T>()`、`ToEntityArray()` 和 `ToArchetypeChunkArray()` 必须首先完成任何当前调度的 jobs，这些 jobs 访问与 components 相同的任何一个 query。

在许多情况下，这些同步点也会使某些类型的现有实例“无效”，即 [`DynamicBuffer`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.html) 和 [`ComponentLookup<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentLookup-1.html)。当实例失效时，调用其方法将抛出安全检查异常。如果您仍需要使用的实例失效，您必须检索新实例来替换它。

<br>

## Component 安全手柄

就像本机集合一样，每个 component 类型对于每个 world 都有一个关联的 job 安全句柄。这意味着，对于在 world 中访问相同 component 类型的任何两个 jobs 来说，安全检查不会让 jobs 被同时调度。例如，当我们尝试调度访问 component 类型 *Foo* 的 job 时，如果已调度的 job 也访问 component 类型 *Foo*，则安全检查将引发异常。为了避免此异常，必须在调度新的 job 之前完成已调度的 job，或者新的 job 必须依赖于已调度的 job。

| &#x1F4DD; NOTE |
| :- |
| 如果两个 jobs 都具有相同 component 类型的“只读”访问权限，那么同时调度两个 jobs 是安全的。对于 job 中从未编写过的任何 component 类型，请务必通过使用 [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnlyAttribute.html) 属性标记 component 类型句柄来通知安全检查。 |

<br>

## SystemState.Dependency

当我们在 system 中使用 schedule 和 job 时，我们希望它依赖于可能与新的 job 冲突的任何当前调度的 jobs，即使这些 jobs 已调度在其他中 systems。为了安排这一点，我们使用 [`SystemState`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemState.html) 的 job 句柄属性 `Dependency`。

紧接着 system 更新之前：

1. ...system 的 `Dependency` 属性已完成
2. ...然后分配所有其他 systems 的 `Dependency` 句柄的组合，这些句柄访问与此 system 相同的 component 类型。例如，对于访问 *Foo* 和 *Bar* component 类型的 system，world 中也访问 *Foo* 或 *Bar* 的所有其他 systems 的 `Dependency` 将包含在组合中 job 手柄。

然后，您需要在每个 system 中执行两件事：

1. jobs 在 system 更新中计划的所有 jobs 应（直接或间接）取决于更新之前分配给 `Dependency` 的 job 句柄。
1. 在 system 更新返回之前，应为 `Dependency` 属性分配一个句柄，其中包含该更新中计划的所有 jobs。

只要您遵循这两条规则，system 更新中计划的每个 job 将取决于其他 systems 中计划的所有 jobs，这些 systems 可能访问任何相同的 component 类型。

| ⚠ IMPORTANT |
| :- |
| Systems 不跟踪它们使用的 [本机集合]()，因此 `Dependency` 属性仅考虑 component 类型，而不是本机集合。因此，如果两个 systems 均为 schedule jobs 使用相同的本机集合，则它们的 `Dependency` 属性不一定会合并到分配给另一个的 `Dependency` 属性的 job 句柄中，因此不同 systems 的 jobs 将不会像它们应该的那样相互依赖。在这些场景中，您*可以*在 systems 之间手动共享 job 句柄，但更好的解决方案是将本机集合存储在 component 中：如果两个 systems 通过相同的 component 类型访问该集合，则两个 systems 中安排的 jobs 应该相互依赖（只要您遵循上述 `Dependency` 规则）。 |

<br>

## ComponentLookup\<T\>

我们可以通过 `EntityManager` 随机访问单个 entities 的 ZXQFAQBNAYBKE​​UKZXQ，但我们通常不应该在 jobs 中使用 `EntityManager`。相反，我们应该使用名为 [`ComponentLookup<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentLookup-1.html) 的类型，它可以通过 entity ID 获取和设置 component 值。我们还可以使用 [`BufferLookup<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.BufferLookup-1.html) 通过 entity ID 获取动态缓冲区。

| ⚠ IMPORTANT |
| :- |
| 请记住，通过 ID 查找 entity 往往会产生缓存未命中的性能成本，因此通常最好避免查找。当然，有很多问题需要随机查找来解决，因此决不能完全避免随机查找。只是避免不小心使用它们！ |


如果指定的 entity 具有 component 类型 T，则 `ComponentLookup<T>` 和 `BufferLookup<T>` 方法 [`HasComponent()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentLookup-1.HasComponent.html) 返回 true。[`TryGetComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentLookup-1.TryGetComponent.html) 和 [`TryGetBuffer<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.BufferLookup-1.TryGetBuffer.html) 方法执行相同的操作，但还会输出 component 值或缓冲区（如果存在）。

要测试 entity 是否简单存在，我们可以调用 [`EntityStorageInfoLookup`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityStorageInfoLookup.html) 的 [`Exists()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityStorageInfoLookup.Exists.html)。对 `EntityStorageInfoLookup` 进行索引会返回 [`EntityStorageInfo`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityStorageInfo.html) 结构体，其中包括对 entity 的 chunk 及其在 chunk 中的索引的引用。


如果 job 只需要“读取”通过 `ComponentLookup<T>` 访问的 components，则 `ComponentLookup<T>` 字段应使用 `ReadOnly` 属性进行标记，以通知 job 安全检查。`BufferLookup<T>` 也是如此。

在并行调度的 job 中，从 `ComponentLookup<T>` 获取 component 值需要使用 `ReadOnly` 属性标记该字段。安全检查不允许通过并行调度的 job 中的 `ComponentLookup<T>` 设置 component 值，因为无法保证安全。但是，您可以通过使用 [`NativeDisableParallelForRestriction`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html) 属性标记 `ComponentLookup<T>` 来完全“禁用”`ComponentLookup<T>` 的安全检查。`BufferLookup<T>` 也是如此。只需确保您的代码以线程安全的方式设置 component 值即可！










