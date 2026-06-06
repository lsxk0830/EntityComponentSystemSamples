# Entities 和 components

在此页面中：

- [Entities 和 components](#entities-and-components)
- [Archetypes 和 chunks](#archetypes)
- [查询](#queries)
- [`IComponentData` components](#icomponentdata)
- [DynamicBuffer components](#dynamicbuffer-components)
- [Aspects](#aspects)

<br/>


# Entities 和 components

**entity** 是 GameObject 的轻量级非托管替代方案。Entities 在很多方面类似于 GameObjects，并且可以起到类似的作用，但它们有关键的区别：

- 与 GameObject 不同，entity 不是托管对象，而只是一个唯一标识符号。
- 与 entity 关联的 **components** 通常是结构值。
- 单个 entity 只能具有任何给定类型的一个 component。例如，单个 entity 不能有两个类型均为 *Foo* 的 components。
- 尽管可以给 entity component 类型提供方法，但通常不鼓励这样做。
- entity 没有内置的养育概念。相反，标准 [`Parent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.Parent.html) component 包含对另一个 entity 的引用，允许形成 entity 转换层次结构。

Entity component 类型是通过实现以下接口来定义的：

|**类似 component**|**描述**|
|---|---|
| [`IComponentData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IComponentData.html) | 定义最常见、基本的 component 类型。|
| [`IBufferElementData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IBufferElementData.html) | 定义动态缓冲区（可增长数组）component 类型。|
| [`ISharedComponent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ISharedComponent.html) | 定义一个共享的 component 类型，其值可以被多个 entities 共享。|
| [`ICleanupComponent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupComponent.html)  | 定义清理 component 类型，有助于正确设置和拆卸资源。|

还有两个附加接口（[`ICleanupSharedComponent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupSharedComponent.html) 和 [`ICleanupBufferElementData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ICleanupBufferElementData.html)) 和 [chunk components](additional-entities-features.md#chunk-components)（用 `IComponentData` 定义，但通过一组不同的方法从 entities 添加和删除）。

使用 `IComponentData` 或 `IBufferElementData` 定义的 component 类型也可以通过实现 [`IEnableableComponent`](additional-entities-features.md#enableable-components) 来“启用”。

<br>

# Worlds 和 EntityManagers

[`World`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.World.html) 是 entities 的集​​合。entity 的 ID 编号仅在其自己的 world 内是唯一的，*i.e。* entity 与一个 world 中的特定 ID 是与 entity 完全无关，在不同的 world 中具有相同的 ID。

world 还拥有一组 [systems](concepts-systems.md)，它们是 run 在主线程上的代码单元，通常每帧一次。world 的 entities 通常仅由 world 的 systems 和由这些 systems 调度的 jobs 访问，但这不是强制限制。

world 中的 entities 是通过 world 的 [`EntityManager`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.html) 创建、销毁和修改的。`EntityManager` 的关键方法包括：

|**方法**|**描述**|
|---|---|
| [`CreateEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.CreateEntity.html) | 创建新的 entity。 |
| [`Instantiate()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.Instantiate.html) | 使用现有 entity 的所有 components 的副本创建新的 entity。 |
| [`DestroyEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.DestroyEntity.html) | 销毁现有的 entity。 |
| [`AddComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.AddComponent.html) | 将 T 类型的 component 添加到现有 entity。 |
| [`RemoveComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.RemoveComponent.html) | 从现有 entity 中删除类型 T 的 component。 |
| [`HasComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.HasComponent.html) | 如果 entity 当前具有 T 类型的 component，则返回 true。 |
| [`GetComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetComponent.html) | 检索类型 T 的 entity 的 component 的值。 |
| [`SetComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.SetComponent.html) | 覆盖类型 T 的 entity 的 component 的值。 |

| &#x1F4DD; NOTE |
| :- |
| `CreateEntity`、`Instantiate`、`DestroyEntity`、`AddComponent` 和 `RemoveComponent` 是[结构更改](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html) 操作。 |

<br>

#  Archetypes

**archetype** 表示 world 中 component 类型的特定组合：world 中的所有 entities 与一组特定的 component 类型一起存储在同一个中 archetype。例如：

 - 所有 world 的 entities 以及 component 类型 *A*、*B* 和 *C* 都一起存储在一个 archetype 中，
 - ...仅具有 component 类型 *A* 和 *B*（但不是 *C*）的 entities 一起存储在第二个 archetype 中，
 - ...并且具有 component 类型 *B* 和 *D* 的 entities 存储在第三个 archetype 中。

实际上，添加或删除 entity 的 components 会更改 entity 所属的 archetype，从而需要 `EntityManager` 实际移动 entity 及其 components 从旧的 archetype 到新的。

当您从 entity 添加或删除 components 时，`EntityManager` 会将 entity 移动到相应的 archetype。例如，如果 entity 具有 component 类型 *X*、*Y* 和 *Z*，并且您删除其 *Y* component，则 `EntityManager` 将 entity 移动到具有以下类型的 archetype：component 类型 *X* 和 *Z*。如果 world 中不存在此类 archetype，则 `EntityManager` 创建它。

| ⚠ IMPORTANT |
| :- |
| 在 archetypes 之间过于频繁地移动太多 entities 可能会导致[重大成本](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html)。 |

Archetypes 由 `EntityManager` 在您创建和修改 entities 时创建，因此您不必担心显式创建 archetypes。即使从 archetype 中删除所有 entities，archetype 仅在其 world 被销毁时才被销毁。

<br>

# Chunks

archetype 的 entities 存储在属于 archetype 的 16KiB 内存块中，称为 *chunks*。每个 chunk 最多存储 128 个 entities（具体数量取决于 archetype 中 component 类型的数量和大小）。

每种类型的 entity ID 和 components 存储在 chunk 内各自单独的数组中。例如，在具有 component 类型 *A* 和 *B* 的 entities 的 archetype 中，每个 chunk 将存储三个数组：

- entity ID 的一个数组
- ...*A* components 的第二个数组
- ...以及 *B* components 的第三个数组。

chunk 中第一个 entity 的 ID 和 components 存储在这些数组的索引 0 处，第二个 entity 存储在索引 1 处，第三个 entity 存储在索引 2 处，依此类推。

chunk 的数组始终保持紧密排列：

- 当新的 entity 添加到 chunk 时，它存储在数组的第一个空闲索引中。
- 当 entity 从 chunk 中删除时（发生这种情况是因为 entity 正在被销毁，或者因为它正在被移动到另一个 archetype），chunk 中的最后一个 entity 会被移动以填充间隙中。

chunks 的创建和销毁由 `EntityManager` 处理：

- 仅当 entity 添加到已存在的 chunks 已满的 archetype 时，`EntityManager` 才会创建新的 chunk。
- 当 chunk 的最后一个 entity 被删除时，`EntityManager` 仅销毁 chunk。

在 chunk 内添加、删除或移动 entities 的任何 `EntityManager` 操作都称为“结构更改”。此类更改通常应仅在主线程上进行，而不是在 jobs 中进行（尽管可以使用 [`EntityCommandBuffer`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.html) 作为解决方法）。

<br>

# 查询

[`EntityQuery`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityQuery.html) 有效地查找具有指定的 component 类型集的所有 entities。例如，如果 query 查找具有 component 类型 *A* 和 *B* 的所有 entities，则 query 将收集所有包含 *A* 和 *B* 的 chunks，无论 archetypes 可能具有的任何其他 component 类型。这样的 query 将匹配 entities 与 component 类型 *A* 和 *B*，但 query 也会匹配 entities 与 component 类型 *A*、*B* 和*C*。

| &#x1F4DD; NOTE |
| :- |
| 与 query 匹配的 archetypes 将被缓存，直到下次将新的 archetype 添加到 world 为止。由于 world 中的现有 archetypes 集往往会在程序生命周期的早期稳定下来，因此这种缓存通常有助于使查询成本降低得多。 |

query 还可以指定 component 类型以从匹配的 archetypes 中“排除”。例如，如果 query 查找具有 component 类型 *A* 和 *B* 但*不*具有 component 类型 *C* 的所有 entities，则 query 将与 entities 匹配 component 类型 *A* 和 *B*，但 query *不会* 将 entities 与 component 类型 *A*、*B* 和 *C* 匹配。

<br>

# Entity ID 的

entity ID 由结构体 [`Entity`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.Entity.html) 表示。

为了通过 ID 查找 entities，world 的 `EntityManager` 维护了一个 entity 元数据数组。每个 entity ID 都有一个索引值，表示该元数据数组中的一个槽，该槽存储指向存储 entity 的 chunk 的指针，以及 entity 的索引。chunk。当特定索引不存在 entity 时，该索引处的 chunk 指针为空。例如，当前不存在索引为 1、2、5 的 entities，因此这些槽中的 chunk 指针均为空：

![entity 元数据](./images/entities_metadata.png)

为了允许 entity 索引在 entity 被销毁后能够被重用，每个 entity ID 还包含一个*版本号*。当 entity 被销毁时，存储在其索引处的版本号会递增，因此如果 ID 的版本号与当前存储的版本号不匹配，则 ID 必须引用已被销毁或可能从未存在的 entity。

<br>

# `IComponentData`

最常见、基本的 component 类型被定义为实现 [`IComponentData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IComponentData.html) 的结构。

`IComponentData` 结构预计是非托管的，因此它不能包含任何托管字段类型。具体来说，允许的字段类型有：

* [Blittable 类型](https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types)
* `bool`
* `char`
* `BlobAssetReference<T>`，对 Blob 数据结构的引用
* `Collections.FixedString`，固定大小的字符缓冲区
* `Collections.FixedList`
* [固定数组](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/fixed-statement)（仅允许在[不安全](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/unsafe) 上下文中）
* 符合这些相同限制的结构类型。

没有字段的 `IComponentData` 结构称为***标签 component***。虽然标签 components 不存储任何数据，但它们仍然可以像任何其他 component 类型一样从 entities 中添加和删除，这对于查询很有用。例如，如果我们所有代表怪物的 entities 都有 *Monster* 标签 component，则 *Monster* component 类型的 query 将匹配所有怪物 entities。

<br>

## 托管 `IComponentData` components

实现 `IComponentData` 的类是“托管”component 类型。与非托管 `IComponentData` 结构不同，这些托管 components 可以存储任何托管对象。

一般来说，托管 component 类型应仅在真正需要时使用，因为与*非托管* components 相比，它们会产生一些沉重的成本：

- 与所有托管对象一样，托管 components *不能* 在 [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest) 编译的代码中使用。
- 托管对象通常无法在 [jobs](https://docs.unity3d.com/Manual/JobSystem.html) 中安全使用。
- 托管的 components 不直接存储在 chunks 中：相反，world 的所有托管 components 都存储在一个大数组中，而 chunks 仅存储该数组的索引。
- 与所有托管对象一样，创建托管 components 会产生垃圾收集开销。

如果托管 component 类型实现 `ICloneable`，则在复制实例本身时，可以正确复制它包含的任何资源。同样，如果托管 component 类型实现 `IDisposable`，则当从 entity 中删除实例或 entity 被销毁时，它可以正确处置它可能包含的任何资源。

<br>

# DynamicBuffer components

[`DynamicBuffer`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.html) 是 component 类型，它是一个可调整大小的数组。要定义 `DynamicBuffer` component 类型，请创建一个实现 [`IBufferElementData`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IBufferElementData.html) 接口的结构体。

每个 entity 的缓冲区存储了一个 `Length`、一个 `Capacity` 和一个指针：

* `Length` 是缓冲区中元素的数量。它从 `0` 开始，并在您将值附加到缓冲区时递增。
* `Capacity` 是缓冲区中的存储量。它开始匹配内部缓冲区容量（[默认](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.TypeManager.DefaultBufferCapacityNumerator.html) 到 `128 / sizeof(Waypoint)`，但可以通过 `IBufferElementData` 结构上的 [`InternalBufferCapacity`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.InternalBufferCapacityAttribute.html) 属性指定）。设置 `Capacity` 调整缓冲区大小。
* 该指针指示缓冲区内容的位置。最初是 `null`，表示内容直接存储在 chunk 中。如果容量设置超过内部缓冲区容量，则在 chunk 外部分配一个新的更大数组，将内容复制到此外部数组，并将指针设置为指向此新数组。如果缓冲区的长度超过了外部数组的容量，则缓冲区的内容将被复制到 chunk 之外的另一个新的更大数组，并处理旧数组。缓冲区也可以缩小。

当 `EntityManager` 销毁 chunk 本身时，内部缓冲区容量和外部容量（如果存在）将被释放。

| &#x1F4DD; NOTE |
| :- |
|当动态缓冲区存储在 chunk 外部时，内部容量实际上被浪费了，并且访问缓冲区内容需要遵循额外的指针。如果您确保永远不会超出内部容量，则可以避免这些成本。当然，在许多情况下，保持在这个限制内可能需要过大的内部容量。另一种选择是将内部容量设置为 0，这意味着任何非空缓冲区将始终存储在 chunk 之外。这会产生在访问缓冲区时始终遵循指针的成本，但它避免了浪费 chunk 中未使用的空间。|


`EntityManager` 具有使用动态缓冲区的以下关键方法：

|**方法**|**描述**|
|----|---|
| [`AddComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.AddComponent.html) | 将类型 T 的 component 添加到 entity，其中 T 可以是动态缓冲区 component 类型。 |
| [`AddBuffer<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.AddBuffer.html) | 将 T 类型的动态缓冲区 component 添加到 entity；以 `DynamicBuffer<T>` 形式返回新缓冲区。 |
| [`RemoveComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.RemoveComponent.html) | 从 entity 中删除类型 T 的 component，其中 T 可以是动态缓冲区 component 类型。 |
| [`HasBuffer<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.HasBuffer.html) | 如果 entity 当前具有类型 T 的动态缓冲区 component，则返回 true。 |
| [`GetBuffer<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.GetBuffer.html) | 返回 T 类型的 entity 动态缓冲区 component 作为 `DynamicBuffer<T>`。 |

`DynamicBuffer<T>` 表示单个 entity 的类型 T 的动态缓冲区 component。其主要属性和方法包括：

|**属性或方法**|**描述**|
|----|---|
| [`Length`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.Length.html) | 获取或设置缓冲区的长度。 |
| [`Capacity`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.Capacity.html) | 获取或设置缓冲区的容量。 |
| [`Item[Int32]`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.Item.html) | 获取或设置指定索引处的元素。 |
| [`Add()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.Add.html) | 将一个元素添加到缓冲区的末尾，并根据需要调整其大小。 |
| [`Insert()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.Insert.html) | 在指定索引处插入元素，并根据需要调整大小。 |
| [`RemoveAt()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.RemoveAt.html) | 删除指定索引处的元素。 |

为了强制 job 安全，`DynamicBuffer<T>` 值保存一个安全句柄。当访问相同缓冲区 component 类型的任何预定 jobs 仍未完成时，无法访问 `DynamicBuffer` 的内容。但是，如果未完成的 jobs 都仅具有 component 类型缓冲区的只读访问权限，则允许主线程读取缓冲区。

任何结构更改操作都会使 `DynamicBuffer` 安全句柄无效，这意味着所有方法如果被调用，随后都会抛出异常。要在结构更改后再次使用缓冲区，必须重新检索它。

`DynamicBuffer<T>` 可以是“[重新解释](https://docs.unity3d.com/Packages/com.unity.collections@2.1/manual/allocation.html#array-reinterpretation)”。目标重新解释元素类型必须具有与 T 相同的大小。


<br/>

# Aspects

方面是 entity 的 components 子集上的类似对象的包装器。Aspects 对于简化查询和 component 相关代码很有用。例如，[`TransformAspect`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.TransformAspect.html) 将标准变换 components ([`LocalTransform`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.LocalTransform.html) 组合在一起，[`ParentTransform`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.ParentTransform.html) 和 [`WorldTransform`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.WorldTransform.html))。

在 query 中包含某个方面与包含由该方面包裹的 components 相同，*e.g。* 包含 `TransformAspect` 的 query 包含标准变换矩阵 components。

方面被定义为实现 [`IAspect`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IAspect.html) 的只读部分结构。该结构可以包含以下类型的字段：

|**字段类型**|**描述**|
|----|---|
| `Entity` | 包裹后的 entity 的 entity ID。 |
|[`RefRW<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.RefRW-1.html) 或 [`RefRO<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.RefRO-1.html)|对包装的 entity 的 T component 的引用。|
|[`EnabledRefRW<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EnabledRefRW-1.html) 和 [`EnabledRefRO<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EnabledRefRO-1.html)| 对包装的 entity 的 T component 的启用状态的引用。|
|`DynamicBuffer<T>`|包装后的 entity 的动态缓冲区 T component。|
|另一种方面类型| 包含方面将包含“嵌入”方面的所有字段。 |

这些 `EntityManager` 方法创建方面的实例：

|**方法**|**描述**|
|----|---|
| [`GetAspect<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetAspect.html) | 返回包装 entity 的类型 T 的一个方面。 |
| [`GetAspectRO<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityManager.GetAspectRO.html) | 返回包装 entity 的类型 T 的只读方面。如果您使用任何尝试修改基础 components 的方法或属性，只读方面会引发异常。 |

Aspect 实例也可以通过 [`SystemAPI.GetAspectRW<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemAPI.GetAspectRW.html) 或 [`SystemAPI.GetAspectRO<T>`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemAPI.GetAspectRO.html) 检索，并在 [`IJobEntity`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.IJobEntity.html) 或[`SystemAPI.Query`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.SystemAPI.Query.html) 循环。

| ⚠ IMPORTANT |
| :- |
| 您通常应该*通过* `SystemAPI` 而不是 `EntityManager` 获取方面实例：与 `EntityManager` 方法不同，`SystemAPI` 方法使用 system 注册方面的底层 component 类型，这对于 systems 正确地 schedule jobs 及其所需的每个依赖项](./entities-jobs.md#systemstatedependency)。 |

<br>

