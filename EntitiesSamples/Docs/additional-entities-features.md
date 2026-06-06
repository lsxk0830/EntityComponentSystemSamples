
在此页面中：

- [启用 components](#enableable-components)
- [共享 components](#shared-components)
- [清理 components](#cleanup-components)
- [Chunk components](#chunk-components)
- [Blob 资产](#blob-assets)
- [版本号](#version-numbers)

<br>

# 启用 components

实现 `IComponentData` 或 `IBufferElementData` 的结构也可以实现 [`IEnableableComponent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IEnableableComponent.html)。可以根据 entity 启用和禁用实现此接口的 component 类型。

**当 entity 的 component 被禁用时，查询会认为 entity 没有 component 类型。** 如果 chunk 中没有 entities 与 query 匹配，因为或多个 components 被禁用，chunk 将不会包含在 `EntityQuery` 的 [`ToArchetypeChunkArray()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityQuery.ToArchetypeChunkArray.html) 方法返回的数组中。

请注意，禁用 component 不会删除或修改 component：相反，与特定 entity 的特定 component 相关的位会被清除。另请注意，禁用的 component 仅影响查询：禁用的 component 仍可以正常读取和修改，例如*通过* `EntityManager` 方法。

所有可启用的 components 在新创建的 entity 上默认启用。当复制 entity 进行序列化、复制到另一个 world 或通过 `EntityManager` 的 `Instantiate` 方法复制时，新 entity 中 components 的使能状态与原始 entity 中的状态匹配。

可以通过以下方式检查和设置 entity 的 components 的启用状态：

- [`EntityManager`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.html)
- [`ComponentLookup<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ComponentLookup-1.html)
- [`BufferLookup<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.BufferLookup-1.html)（对于动态缓冲区）
- [`EnabledRefRW<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EnabledRefRW-1.html)（用于 `SystemAPI.Query` foreach 或 `IJobEntity`）
- [`ArchetypeChunk`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ArchetypeChunk.html)

例如，`EntityManager` 有以下关键方法：

|**方法**|**描述**|
|----|---|
| [`IsComponentEnabled<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.IsComponentEnabled.html) | 如果 entity 具有当前启用的 T component，则返回 true。 |
| [`SetComponentEnabled<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.SetComponentEnabled.html) | 启用或禁用 entity 的可启用 T component。 |

| &#x1F4DD; NOTE |
| :- |
| 为了进行 job 安全检查，对 component 启用状态的读或写访问需要对 component 类型本身进行读或写访问。 |

在 `IJobChunk` 中，`Execute` 方法参数表示 chunk 中的 entities 与 query 匹配：

- 如果 `useEnableMask` 参数为 false，则 chunk 中的所有 entities 都与 query 匹配。
- 否则，如果 `useEnableMask` 参数为 true，则 `chunkEnabledMask` 参数的位表示 chunk 中的 entities 与 query 匹配，考虑到 component 的所有可启用类型 query。您可以使用 [`ChunkEntityEnumerator`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ChunkEntityEnumerator.html) 来更方便地迭代匹配的 entities，而不是手动检查这些掩码位。

| &#x1F4DD; NOTE |
| :- |
| `chunkEnabledMask` 是 job 的 query 中包含的可启用 components 的所有启用状态的“组合”。要检查各个 components 的启用状态，请使用 `ArchetypeChunk` 的 `IsComponentEnabled()` 和 `SetComponentEnabled()` 方法。 |

<br>

# 共享 components

对于共享 component 类型，chunk 中的所有 entities 共享相同的 component 值，而不是每个 entity 都有自己的值。因此，**设置 entity 的共享 component 值会执行[结构更改](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html)**：entity 被移动到具有新值的 chunk。

例如，如果 entity 具有 *Foo* 共享 component 值 *X*，则 entity 存储在具有 *Foo* 值 *X* 的 chunk 中；如果随后将 entity 设置为具有 *Foo* 值 *Y*，则 entity 会移动到具有值 *Y* 的 chunk；如果不存在这样的 chunk，则创建一个新的 chunk。

共享 components 的主要用途来自这样一个事实：**查询可以过滤特定的共享 component 值**。

world 不是将共享的 component 值直接存储在 chunks 中，而是将它们存储在一组数组中，而 chunks 存储只是对这些数组进行索引。这意味着**每个唯一的共享 component 值在 world 中仅存储一次**。

共享 component 类型被声明为实现 `ISharedComponentData` 的结构。如果结构体包含任何托管类型字段，则共享 component 本身将是托管 component 类型，具有与托管 `IComponentData` 相同的优点和限制。

`EntityManager` 具有共享 components 的以下关键方法：

|**方法**|**描述**|
|----|---|
| [`AddComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.AddComponent.html) | 将 T component 添加到 entity，其中 T 可以是共享 component 类型。 |
| [`AddSharedComponent()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.AddSharedComponent.html) | 将非托管共享 component 添加到 entity 并设置其初始值。 |
| [`AddSharedComponentManaged()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.AddSharedComponentManaged.html) | 将托管共享 component 添加到 entity 并设置其初始值。 |
| [`RemoveComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.RemoveComponent.html) | 从 entity 中删除 T component，其中 T 可以是共享 component 类型。 |
| [`HasComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.HasComponent.html) | 如果 entity 当前具有 T component，则返回 true，其中类型 T 可以是共享 component 类型。 |
| [`GetSharedComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetSharedComponent.html) | 检索 entity 的非托管共享 T component 的值。 |
| [`SetSharedComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.SetSharedComponent.html) | 覆盖 entity 的非托管共享 T component 的值。 |
| [`GetSharedComponentManaged<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetSharedComponentManaged.html) | 检索 entity 的托管共享 T component 的值。 |
| [`SetSharedComponentManaged<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.SetSharedComponentManaged.html) | 覆盖 entity 的托管共享 T component 的值。 |

可以通过实现 [`IEquatable<T>`](https://docs.microsoft.com/en-us/dotnet/api/system.iequatable-1.equals) 来自定义 `EntityManager` 如何比较共享 component 类型是否相等。

| ⚠ IMPORTANT |
| :- |
| 由于 `EntityManager` 依赖于相等性来识别唯一且匹配的共享 component 值，因此您应该避免修改共享 components 引用的任何可变对象。例如，如果要修改存储在特定 entity 的共享 component 中的数组，则不应直接修改该数组，而应更新该 entity 的 component 以获得该数组的新的、修改后的副本。 |

如果共享 component 类型实现 `IRefCounted`，则可以使用引用计数来检测任何 world 何时不再存储该类型的值。例如，如果实现 `IRefCounted` 的共享 component 值包含 `NativeArray`，则当任何 world 不再存储该值时，您可以处置该数组。

如果共享 component 类型是非托管的，则 `IEquatable<T>` 和 `IRefCounted` 的方法可以通过将 [`[BurstCompile]`](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/api/Unity.Burst.BurstCompileAttribute.html) 属性添加到方法和结构本身来进行 Burst 编译。


| ⚠ IMPORTANT |
| :- |
| **拥有太多唯一的共享 component 值可能会导致 chunk 碎片。** <br> 因为 chunk 中的所有 entities 必须共享相同的共享 component 值，如果您提供唯一的共享值如果 component 值达到大量 entities，则 entities 最终将分散在许多 chunks 中。例如，如果 archetype 有 500 个 entities 与共享 component，并且每个 entity 都有唯一的共享 component 值，则每个 entity 都单独存储在单独的 entities 中。chunk。这浪费了每个 chunk 中的大部分空间，也意味着循环遍历 archetype 的所有 entities 需要访问 500 个 chunks。这种碎片很大程度上抵消了 ECS 结构的性能优势。为了避免此问题，请尝试使用尽可能少的唯一共享 component 值。例如，如果 500 个 entities 仅共享 10 个唯一的共享 component 值，则它们可以存储在少至 10 个 chunks 中。 |

<br>

# 清理 components

清理 components 有两个特殊之处：

- 当具有清理 components 的 entity 被销毁时，非清理 components 将被删除，但 entity 实际上继续存在，直到您单独删除其所有清理 components。
- 当 entity 复制到另一个 world、以序列化方式复制或通过 `EntityManager` 的 `Instantiate` 方法复制时，原始 components 的任何清理都不会添加到新的 entity 中。

清理 components 的主要用例是在 entities 创建后帮助初始化，或在 entities 销毁后清理 entities。例如，假设我们有 entities 代表怪物，它们都有一个 *Monster* 标签 component：

1. 我们可以通过查询所有具有 *Monster* component 但*不*具有 *MonsterCleanup* component 的 entities 来找到所有需要初始化的怪物 entities。对于与此 query 匹配的所有 entities，我们执行任何所需的初始化并添加 *MonsterCleanup*。
2. 我们可以通过查询所有具有 *MonsterCleanup* component 但*不**怪物* component 的 entities 来找到所有需要清理的怪物 entities。对于与此 query 匹配的所有 entities，我们执行任何所需的清理并删除 *MonsterCleanup*。除非 entities 有额外的剩余清理 components，否则这将破坏 entities。

| &#x1F4DD; NOTE |
| :- |
| 在某些情况下，您需要在清理 components 中存储清理所需的信息，但在许多情况下，空的清理标记 component 就足够了。 |

清理 components 有四种类型：

|**清理类型 component**|**描述**|
|---|---|
| 实现 `ICleanupComponentData` 的结构 | 非托管 `IComponentData` 类型的清理变体。|
| 实现 `ICleanupComponentData` 的类 | 托管 `IComponentData` 类型的清理变体。|
| 实现 `ICleanupBufferElementData` 的结构体 | 动态缓冲区类型的清理变体。|
| 实现 `ICleanupSharedComponentData` 的结构 | 共享 component 类型的清理变体。|

<br>

# Chunk components

与常规 component 不同，chunk component 是属于整个 chunk 的单个值，而不是 chunk 中的任何 entity。

就像常规的 component 一样，chunk component 被定义为实现 `IComponentData` 的结构或类，但 chunk component 是用这些添加、删除、获取和设置的 `EntityManager` 方法：

|**方法**|**描述**|
|----|---|
| [`AddChunkComponentData<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.AddChunkComponentData.html) | 将 T 类型的 chunk component 添加到 chunk，其中 T 是托管或非托管 `IComponentData`。 |
| [`RemoveChunkComponentData<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.RemoveChunkComponentData.html) | 从 chunk 中删除类型 T 的 chunk component，其中 T 是托管或非托管 `IComponentData`。 |
| [`HasChunkComponent<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.HasChunkComponent.html) | 如果 chunk 具有 T 类型的 chunk component，则返回 true。 |
| [`GetChunkComponentData<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.GetChunkComponentData.html) | 检索类型 T 的 chunk 的 chunk component 的值。 |
| [`SetChunkComponentData<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.SetChunkComponentData.html) | 设置 T 类型的 chunk 的 chunk component 的值。 |

| &#x1F4DD; NOTE |
| :- |
| 共享 components 还为每个 chunk 存储一个值，但共享 component 值逻辑上属于 entities，而不是 chunk（这就是为什么设置 entity 的共享值） component 值将 entity 移动到另一个 chunk，而不是修改存储在 chunk 中的值。Chunk components 真正属于 chunk 本身，并且与非托管共享 components 不同，非托管 chunk components 直接存储在 chunk。|

<br>

# 斑点资产

Blob（二进制大型对象）资产是存储在连续字节块中的不可变（不变）、非托管的二进制数据：

- Blob 资源的复制和加载效率很高，因为它们完全可重定位：所有内部指针都表示为相对偏移量而不是绝对地址，因此复制整个 Blob 就像复制每个字节一样简单。
- 尽管它们独立于 entities 存储，但 Blob 资源可能会从 entity components _ 引用 _。
- 由于 Blob 资产是不可变的，因此从多个线程访问它们本质上是安全的。

| &#x1F4DD; NOTE |
| :- |
| Blob“资产”这个名称有点误导：Blob 资产是内存中的一段数据，而不是项目资产文件！然而，Blob 资产可以高效且轻松地序列化为磁盘上的文件，因此将它们称为“资产”是有意义的。 |

要创建 Blob 资源：

1. 创建一个 [BlobBuilder](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.html)。
1. 调用构建器的 [`ConstructRoot<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.ConstructRoot.html) 来设置 Blob 的“根”（T 类型的结构体）。
1. 调用构建器的 [`Allocate<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.Allocate.html)、[`Construct<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.Construct.html) 和 [`SetPointer<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.SetPointer.html) 方法来填充其余的 Blob 数据（包括[`BlobArray`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobArray-1.html)、[`BlobString`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobString.html) 和 [`BlobPtr`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobPtr-1.html))。
1. 调用构建器的 [CreateBlobAssetReference](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobBuilder.CreateBlobAssetReference.html)，它会复制构建器中的所有数据以创建实际的 Blob 资源并返回 [BlobAssetReference](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.BlobAssetReference-1.html)。
1. 废弃 `BlobBuilder`。

当不再需要 Blob 资产时，应通过在 `BlobAssetReference` 上调用 `Dispose` 来处置它。

烘焙的 entity scene 中引用的 Blob 资源将被序列化并与 scene 一起加载。这些 Blob 资产不应*手动处置：它们将与 scene 一起自动处置。

| ⚠ IMPORTANT |
| :- |
| 包含内部指针的 Blob 资源的所有部分都必须始终通过引用进行访问。例如，BlobString 结构中的偏移值仅相对于 BlobString 结构在 Blob 内的存储位置而言才是正确的；相对于结构的*副本*，偏移量不正确。 |

<br>

# 版本号

world、其 systems 及其 chunks 维护多个**“版本号”**（通过某些操作递增的数字）。通过比较版本号，您可以确定某些数据是否已更改。

所有版本号都是 32 位有符号整数，因此当递增时，它们最终会回绕。比较版本号的正确方法依赖于 C# 如何定义有符号整数溢出的微妙怪癖：

```csharp
// true if VersionB is more recent than VersionA
// false if VersionB is equal or less than VersionA
bool changed = (VersionB - VersionA) > 0;
```

|**版本号**|**描述**|
|---|---|
| [`World.Version`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.World.Version.html) | 每次 world 添加或删除 system 或 system 组时都会增加。 |
| [`EntityManager.GlobalSystemVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.GlobalSystemVersion.html) | 在 world 中的每次 system 更新之前增加。 |
| [`SystemState.LastSystemVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.SystemState.LastSystemVersion.html) | 每次更新 system 后立即分配 `GlobalSystemVersion` 的值。 |
| [`EntityManager.EntityOrderVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/api/Unity.Entities.EntityManager.EntityOrderVersion.html) | 每次 world 中进行结构更改时都会增加。 |

每个 component 类型都有自己的版本号，该版本号通过任何获得对 component 类型的写访问权限的操作来递增。该号码可以通过调用方法[`EntityManager.GetComponentOrderVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetComponentOrderVersion.html) 来检索。

每个共享的 component *值*还具有一个版本号，每次结构更改影响具有该值的 chunk 时，该版本号都会增加。

chunk 在 chunk 中存储每个 component 类型的版本号。当 chunk 中的 component 类型被访问以进行写入时，其版本号将被分配为 `EntityManager.GlobalSystemVersion` 的值，无论是否实际修改了任何 component 值。这些 chunk 版本号可以通过调用 [`ArchetypeChunk.GetChangeVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ArchetypeChunk.GetChangeVersion.html) 方法来检索。

chunk 还存储每个 component 类型的版本号，每次结构更改影响 chunk 时，都会为该类型分配 `EntityManager.GlobalSystemVersion` 的值。这些 chunk 版本号可以通过调用 [`ArchetypeChunk.GetOrderVersion`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ArchetypeChunk.GetOrderVersion.html) 方法来检索。

<br>
