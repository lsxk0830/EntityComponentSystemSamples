# Entities 和 Components

entity 是 GameObject 的轻量级非托管替代方案。Entities 在很多方面类似于 GameObjects，并且可以起到类似的作用，但它们有关键的区别：

* 与 GameObject 不同，entity 不是托管对象，而只是一个**唯一标识符号**
* entity 的 components 通常是结构体值
* entity 的 components 没有 MonoBehaviour“事件函数”（Update、Start）
* 尽管可以给 entity component 类型提供方法，但通常不鼓励这样做
* 一个实体（Entity）对于同一种组件类型只能拥有一个组件；例如，同一个实体不能同时拥有两个类型都为 Foo 的组件
* 实体没有内置的父子关系机制。取而代之的是，Parent 组件保存对父实体的引用，以此建立实体的 Transform 层级结构

基本 component 类型是通过创建实现 IComponentData 来定义的

```c#
// 具有两个字段的实体组件类型
public struct Health : IComponentData
{
    public int HitPoints;
    public float ArmourRating;
}
```

IComponentData 结构应该是非托管的，因此它不能包含任何托管字段类型。具体来说，允许的字段类型有：

* Blittable types（）
* bool（布尔值）
* char（字符）
* BlobAssetReference\<T\>，对 Blob 数据结构的引用
* Collections.FixedString，固定大小的字符缓冲区
* Collections.FixedList
* 固定数组（仅允许在不安全的上下文中）
* 符合这些相同限制的其他结构类型。

## Entity Worlds 和 EntityManagers

World 是 entities 的集合。entity 的 ID 编号仅在其自己的 world 中是唯一的。实体标识符（Entity ID）不是全局唯一的，而是在各自的 World 内唯一。因此，不同 World 中相同 ID 的实体并不表示同一个对象

一个 World 还管理着一组系统（System），它们是运行在主线程上的逻辑单元，通常会在每一帧执行一次。World 中的实体一般只由该 World 的Systems及其所调度的Jobs访问（但这只是约定俗成的使用方式，而非强制规定）

world 中的 entities 是通过 world 的 EntityManager 来创建、销毁和修改的，其方法包括：

* CreateEntity()：创建新的 entity。
* Instantiate()：使用现有 entity 的所有 components 的副本创建新的 entity。
* DestroyEntity()：销毁现有的 entity。
* AddComponent\<T\>()：将 T 类型的 component 添加到现有的 entity。
* RemoveComponent\<T\>()：从现有 entity 中删除类型 T 的 component。
* HasComponent\<T\>()：如果 entity 当前具有 T 类型的 component，则返回 true。
* GetComponent\<T\>()：检索类型 T 的 entity 的 component 的值。
* SetComponent\<T\>()：覆盖类型 T 的 entity 的 component 的值。

## Archetypes

archetype 表示 world 中 component 类型的特定唯一组合：world 中的所有 entities 与一组特定的 component 类型一起存储在同一个中 archetype。例如：

* 所有 world 的 entities 与 component 类型 A、B 和 C 一起存储在一个 archetype 中，
* 具有 component 类型 A 和 B（但不是 C）的 entities 一起存储在第二个 archetype 中，
* 具有 component 类型 B 和 D 的 entities 都存储在第三个 archetype 中。

当向实体添加或从实体移除组件时，EntityManager 会将实体迁移到合适的 Archetype。例如，假设某个实体具有 X、Y、Z 三种组件类型。当移除 Y 组件后，EntityManager 会将该实体迁移到包含 X 和 Z 组件类型的 Archetype，并复制对应的 X 和 Z 组件数据。如果该 Archetype 在当前 World 中尚不存在，EntityManager 会创建它。

**警告：频繁地将大量实体在不同 Archetype 之间迁移可能会产生较高的性能开销。**

Archetype 会在创建或修改实体时由 EntityManager 自动创建，因此开发者无需手动管理 Archetype 的创建。

即使**一个 Archetype 中已经没有任何实体存在，该 Archetype 仍会继续保留，直到其所在的 World 被销毁时才会被释放**。

## Chunks

Archetype 中的实体存储在属于该 Archetype 的 16KiB 内存块中，这些内存块称为 Chunk（数据块）。每个 Chunk 最多可存储 128 个实体。（如果单个实体所需的存储空间超过 16KiB / 128，那么每个 Chunk 能容纳的实体数量将少于 128 个。）

实体 ID 以及各类型组件的数据会分别存储在 Chunk 内各自独立的数组中。

例如，对于包含组件类型 A 和 B 的实体所对应的 Archetype，每个 Chunk 会维护三个数组：

```
Chunk
┌──────────────────────────────┐
│ Entity IDs │ [E1][E2][E3]... │
├──────────────────────────────┤
│ A          │ [A1][A2][A3]... │
├──────────────────────────────┤
│ B          │ [B1][B2][B3]... │
└──────────────────────────────┘
```

- 一个用于存储实体 ID 的数组
- 一个用于存储 A 组件的数组
- 一个用于存储 B 组件的数组

Chunk 中第一个实体的 ID 和组件数据存储在这些数组的索引 0 位置；第二个实体存储在索引 1；第三个实体存储在索引 2，以此类推。

Chunk 内的数组始终保持紧凑排列：

- 当一个新实体被添加到 Chunk 时，它会存放到数组中的第一个空闲索引位置
- 当一个实体从 Chunk 中移除时（无论是因为实体被销毁，还是因为它被移动到另一个 Archetype），Chunk 中最后一个实体会被移动过来填补空缺的位置

Chunk 的创建与销毁由 EntityManager 负责管理：

- 只有当向某个 Archetype 添加实体，而该 Archetype 现有的所有 Chunk 都已满时，EntityManager 才会创建新的 Chunk。
- **只有当 Chunk 中最后一个实体被移除时，EntityManager 才会销毁该 Chunk**。

任何会在 Chunk 中添加、移除或移动实体的 EntityManager 操作，都被称为结构性变更（Structural Change）。

这类变更只能在主线程（Main Thread）上执行，不能在 Job 中直接进行（不过正如后面将会介绍的那样，可以通过 EntityCommandBuffer 作为一种变通方案来实现）。

## 查询

EntityQuery 能够高效地筛选出满足指定组件条件的实体。例如，当一个查询要求实体同时具有组件类型 A 和 B 时，系统会遍历所有包含 A 和 B 组件的 Archetype，并收集这些 Archetype 中的 Chunk，而不考虑这些 Archetype 是否还包含其他组件类型。因此，该查询既能匹配组件集合为 {A, B} 的实体，也能匹配组件集合为 {A, B, C}、{A, B, D} 等包含 A 和 B 的实体。

> 注意：EntityQuery 所匹配的 Archetype 会被缓存，并持续复用，直到 World 中新增了新的 Archetype。
>
> 由于在大多数情况下，World 中的 Archetype 类型会在程序生命周期的早期基本确定下来，因此这种缓存机制通常可以大幅提高查询性能并减少查询成本

EntityQuery 不仅可以指定必须包含的组件类型，还可以指定必须排除的组件类型。

例如，若一个查询定义为“包含 A 和 B 组件，但不包含 C 组件”，则它会匹配组件集合为 {A, B} 的实体，而不会匹配组件集合为 {A, B, C} 的实体。

## Entity ID 的

实体标识符（Entity ID）由 `Entity` 结构体表示，其中包含两个整数字段：`Index` 和 `Version`。为了根据 Entity ID 快速定位实体，EntityManager 会维护一个实体元数据表。`Index` 用于指定实体在该元数据表中的位置。每个表项记录了：

- 实体所在 Chunk 的引用（或指针）；
- 实体在该 Chunk 中的索引。

如果某个 Index 当前没有对应的实体存在，则该表项中的 Chunk 引用为空（null）。例如，在下图所示的情况下，索引为 1、2 和 5 的实体已经不存在，因此对应表项中的 Chunk 引用均为空。

<img src=".\Texture\Entities101\EntityID.png" align="left" />

Version 字段的作用是支持 Entity Index 的复用。当一个实体被销毁后，EntityManager 会将该索引对应的版本号加 1。这样一来，即使之后有新的实体复用了同一个 Index，也能够通过 Version 区分新旧实体。

如果某个 Entity 的 Version 与元数据表中该 Index 当前记录的 Version 不一致，则说明该 Entity 引用已经失效，它要么指向一个已被销毁的实体，要么从未对应过任何有效实体。

## 标签 components

没有任何成员字段的 `IComponentData` 组件被称为标签组件（Tag Component）。

标签组件不携带数据，其作用主要是标记实体的身份或状态。尽管如此，它们仍然属于组件，因此可以像普通组件一样被添加到或移除出实体。

标签组件最常见的用途是配合 EntityQuery 进行筛选。

例如，假设所有怪物实体都拥有一个 `Monster` 标签组件，那么通过查询 `Monster` 组件，就可以快速获取所有怪物实体。

```c#
// 一个Tag组件
public struct Monster : IComponentData
{
}
```

## DynamicBuffer 组件

DynamicBuffer 是 component 类型，它是可调整大小的数组。要定义 DynamicBuffer component 类型，请创建一个实现 IBufferElementData 接口的结构体

```c#
// 动态缓冲区组件类型
public struct Waypoint : IBufferElementData
{
	public float3 Value:
}
```

这个缓冲区（Buffer）会存储三个信息：Length（长度）、Capacity（容量）以及一个指针（Pointer）

* 长度是缓冲区中元素的数量。它从 0 开始，并在您向缓冲区添加值时递增。
* 容量是缓冲区中的存储量。它开始匹配内部缓冲区容量（默认为 128 / sizeof(T)，但可以通过 IBufferElementData 结构上的 InternalBufferCapacity 属性指定）。设置容量可调整缓冲区的大小。
* 该指针指示缓冲区内容的位置。最初为空，表示内容直接存储在 chunk 中。如果容量设置超过内部缓冲区容量，则在 chunk 外部分配一个新的更大数组，将内容复制到此外部数组，并将指针设置为指向此新数组。如果缓冲区的长度超过了外部数组的容量，则缓冲区的内容将被复制到 chunk 之外的另一个新的、更大的数组，并处理旧数组。缓冲区也可以缩小。

当 EntityManager 销毁 chunk 本身时，内部缓冲区容量和外部容量（如果存在）将被释放。

**注意**：当动态缓冲区存储在 chunk 外部时，内部容量实际上被浪费了，并且访问缓冲区内容需要遵循额外的指针。如果您确保永远不会超出内部容量，则可以避免这些成本。当然，在许多情况下，保持在这个限制内可能需要过大的内部容量。另一种选择是将内部容量设置为 0，这意味着任何非空缓冲区将始终存储在 chunk 之外。这会产生在访问缓冲区时始终遵循指针的成本，但它避免了浪费 chunk 中未使用的空间。

EntityManager 具有以下动态缓冲区的关键方法：

* AddComponent\<T\>()：将 T 类型的 component 添加到 entity，其中 T 可以是动态缓冲区 component 类型。
* AddBuffer\<T\>()：向 entity 添加类型 T 的动态缓冲区 component；以 DynamicBuffer\<T\> 形式返回新缓冲区。
* RemoveComponent\<T\>()：从 entity 中删除类型 T 的 component，其中 T 可以是动态缓冲区 component 类型。
* HasBuffer\<T\>()：如果 entity 当前具有类型 T 的动态缓冲区 component，则返回 true。
* GetBuffer\<T\>()：将 T 类型的 entity 动态缓冲区 component 返回为 DynamicBuffer\<T\>。

DynamicBuffer\<T\> 表示单个 entity 的类型 T 的动态缓冲区 component。其主要属性和方法包括：

* Length：获取或设置缓冲区的长度。
* Capacity：获取或设置缓冲区的容量。
* Item[Int32]：获取或设置指定索引处的元素。
* Add()：将一个元素添加到缓冲区的末尾，并根据需要调整其大小。
* Insert()：在指定索引处插入元素，必要时调整大小。
* RemoveAt()：删除指定索引处的元素。

  **注意**：执行任何结构更改操作都会使 DynamicBuffer 无效，这意味着如果随后使用 DynamicBuffer 将引发异常。要在结构更改后再次使用缓冲区，必须重新检索它。

### 总结

### 1. 什么是 DynamicBuffer？

`DynamicBuffer` 是 ECS 中的一种**可变长度数组组件**。

普通 `IComponentData`：

```c#
public struct Health : IComponentData
{
    public int Value;
}
```

一个实体只能存储一个 `Health` 值，而 DynamicBuffer：

```c#
public struct Waypoint : IBufferElementData
{
    public float3 Value;
}
```

一个实体可以存储任意数量的 `Waypoint`：

```
Entity
 └── DynamicBuffer<Waypoint>
      [P1, P2, P3, P4, ...]
```

适用于：

- 路径点（Waypoints）
- 背包物品（Inventory）
- 技能列表
- Buff列表
- 邻接节点
- 历史记录

------

### 2. 如何定义

定义一个实现 `IBufferElementData` 的结构体：

```c#
public struct Waypoint : IBufferElementData
{
    public float3 Value;
}
```

注意：

```
IComponentData      → 普通组件
IBufferElementData  → DynamicBuffer元素类型
```

------

### 3. Buffer内部结构

每个实体的 Buffer 维护：

```
Length
Capacity
Pointer
```

-  Length：当前元素数量

  ```
  [P1][P2][P3]
  
  Length = 3
  ```

- Capacity：当前最大可容纳数量

  ```
  [P1][P2][P3][ ][ ]
  
  Length = 3
  Capacity = 5
  ```

- Pointer：指向实际数据的位置

  ```
  Pointer ───► Buffer Data
  ```

------

### 4. Internal Buffer Capacity

默认情况下：Buffer数据直接存储在Chunk内部

类似：

```
Chunk

Entity
 ├── Health
 ├── Position
 └── Buffer数据
```

这样访问最快：

```
无需额外内存跳转
Cache友好
```

```
InternalBufferCapacity ≈ 128 / sizeof(T)
```

例如：

```
struct Waypoint
{
    float3 Value;
}
sizeof(float3)=12

128 / 12 ≈ 10
```

那么Chunk内大约可直接存：

```
10个Waypoint
```

------

### 5. 超出内部容量时会发生什么？

假设：

```
Internal Capacity = 10
```

当前：

```
Length = 10
```

再添加一个：

```
buffer.Add(...)
```

系统会：

1. 在Chunk外部分配更大的数组

   ```
   Heap Memory
   
   [P1][P2]...[P11]
   ```

2. 复制旧数据

   ```
   Chunk
    ↓ Copy
   External Buffer
   ```

3. Pointer指向新数组

```
Pointer ───► External Buffer
```

此后Buffer数据都在Chunk外部。

------

### 6. 超出外部容量怎么办？

例如：

```
Capacity = 16
Length   = 16
```

继续Add：

```
分配更大的数组
复制旧数据
释放旧数组
```

类似：List<T>的扩容机制。

------

### 7. 性能影响

最理想情况：Buffer始终在Chunk内部：

```
✓ 无额外指针
✓ Cache命中率高
✓ ECS最快状态
```

------

超出Internal Capacity：Buffer搬到Chunk外：

```
Chunk
  ↓
Pointer
  ↓
External Buffer
```

代价：

```
额外一次指针跳转
内存局部性下降
Chunk空间浪费
```

------

#### 优化方案1：增大InternalBufferCapacity

```c#
[InternalBufferCapacity(32)]
public struct Waypoint : IBufferElementData
{
    public float3 Value;
}
```

- 优点：大多数情况不扩容、性能最好
- 缺点：Chunk占用更多空间

------

#### 优化方案2：设为0

```c#
[InternalBufferCapacity(0)]
```

效果：

```
任何非空Buffer
都直接存到Chunk外
```

- 优点：Chunk不浪费空间
- 缺点：每次访问都要跟随Pointer

------

### 8. EntityManager相关API

- 添加Buffer

  ```
  entityManager.AddBuffer<Waypoint>(entity);
  ```
  返回：
  
  ```
  DynamicBuffer<Waypoint>
  ```
  
- 获取Buffer

  ```
  var buffer = entityManager.GetBuffer<Waypoint>(entity);
  ```

- 判断是否存在

  ```
  entityManager.HasBuffer<Waypoint>(entity);
  ```

- 删除Buffer

  ```
  entityManager.RemoveComponent<Waypoint>(entity);
  ```

------

### 9. DynamicBuffer常用操作

- Add

  ```
  buffer.Add(new Waypoint());
  ```

- Insert：指定位置插入

  ```
  buffer.Insert(2, value);
  ```

- RemoveAt：删除指定位置

  ```
  buffer.RemoveAt(2);
  ```

- 访问元素

  ```
  var p = buffer[0];
  buffer[0] = value;
  ```

- Length

  ```
  buffer.Length
  ```

- Capacity

  ```
  buffer.Capacity
  ```

------

### 10. 最重要的坑：Structural Change

任何结构性变更都会使 DynamicBuffer 失效。

例如：

```c#
var buffer = entityManager.GetBuffer<Waypoint>(entity);

entityManager.AddComponent<Tag>(entity);
```

这里：

```
AddComponent
↓
Structural Change
↓
实体迁移Archetype
↓
Buffer失效
```

之后：

```
buffer.Add(...)
```

会抛异常。

------

#### 正确做法

结构变更后重新获取：

```
entityManager.AddComponent<Tag>(entity);

buffer = entityManager.GetBuffer<Waypoint>(entity);
```

然后再使用。

------

### 一张图记住

```
DynamicBuffer

Entity
  │
  └── Buffer Header
       ├── Length
       ├── Capacity
       └── Pointer
                │
                ▼
         Internal Storage (Chunk内)
             或
         External Storage (Chunk外)
```

### 核心结论

1. **DynamicBuffer = ECS中的动态数组组件。**
2. **通过实现 `IBufferElementData` 定义。**
3. **小数据存Chunk内，大数据自动搬到Chunk外。**
4. **超出Capacity会自动扩容并复制数据。**
5. **最好不要频繁超过 InternalBufferCapacity。**
6. **Structural Change 后所有已获取的 DynamicBuffer 都会失效，必须重新获取**

# Systems

System 是属于某个 World 的逻辑执行单元，运行在主线程中，并且通常会在每一帧执行一次。一般来说，System 只会操作其所在 World 中的实体，不过这只是约定俗成的使用方式，而非框架强制要求。

* OnUpdate()：通常每帧调用一次（尽管这取决于 system 所属的 system 组）。
* OnCreate()：在第一次调用 OnUpdate 之前以及每当 system 恢复运行时调用。
* OnDestroy()：当 system 被销毁时调用。


> 注意：这些方法都提供了默认的空实现（即什么都不做的实现），因此当你的 System 不需要用到它们时，可以直接省略。
>
> 例如，如果 `OnCreate` 方法的函数体为空，那么你完全可以不编写这个方法。

如果将某个 System 的 `Enabled` 属性设为 `false`，那么在更新阶段（通常是每帧）该 System 将不会被执行

system 还可以实现 ISystemStartStop，它具有以下方法：

* **`OnStartRunning()`**：当系统即将开始运行时调用。它会在第一次执行 `OnUpdate()` 之前触发；如果系统之前被禁用，之后又重新启用（`Enabled` 从 `false` 变为 `true`），该方法也会再次执行。

  **`OnStopRunning()`**：当系统即将停止运行时调用。它会在系统销毁前触发；如果系统被禁用（`Enabled` 从 `true` 变为 `false`），该方法同样会被调用。

## System 组和 System 更新顺序

一个 World 中的所有 System 都是通过 System Group 进行组织和管理的。

每个 System Group 都包含一个有序的子节点列表，其子节点既可以是 System，也可以是其他 System Group。默认情况下，System Group 会按照既定的排序顺序，依次执行其所有子节点的 `OnUpdate()`。

换句话说，所有 System Group 共同构成了一棵层级树，而这棵树决定了系统最终的执行顺序

* system 组被定义为继承自 ComponentSystemGroup 的类。
* 当更新 system 组时，该组通常按排序顺序更新其子项，但可以通过覆盖组的更新方法来覆盖此默认行为。例如，FixedStepSimulationGroup 具有自定义更新行为，每帧将更新其子项零次或多次，以近似固定的更新间隔。
* 每次在组中添加或删除子项时，组的子项都会重新排序。
* 子级将添加到具有 UpdateInGroup 属性的 system 组。如果没有此属性，systems 和 system Group将默认添加到 SimulationSystemGroup。
* UpdateBefore 和 UpdateAfter 属性可用于确定组中子项之间的相对排序顺序。例如，如果 FooSystem 具有属性 [UpdateBefore(typeof(BarSystem))]，则 FooSystem 将按排序顺序放置在 BarSystem 之前。但是，如果 FooSystem 和 BarSystem 不属于同一 system 组，则该属性除了 trigger 警告之外没有任何效果。
* 当一个 System Group 的子节点之间存在互相冲突的更新顺序约束时，在对这些子节点进行排序的过程中会抛出异常。例如，如果 System A 使用排序属性声明自己必须先于 System B 执行，而 System B 又声明自己必须先于 System A 执行，那么这两个约束无法同时满足，系统排序将失败并抛出异常。

```c#
// 一个 system group.因为此组不覆盖OnUpdate，所以它遵循默认值
// behaviour: 其子项将按排序顺序更新
public class MonsterSystemGroup : ComponentSystemGroup { }

// MonsterSystemGroup的一个子系统
[UpdateInGroup(typeof(MonsterSystemGroup)]
public struct VampireSystem : ISystem
{
	...
}
```

## 创建 worlds 和 systems

进入 Play Mode 时，默认的自动引导机制会创建一个默认 World，该 World 包含三个系统组。

* **InitializationSystemGroup（初始化系统组）**：在 Unity Player Loop 的初始化阶段结束时执行更新，通常用于执行各种初始化相关的逻辑。
* **SimulationSystemGroup（模拟系统组）**：在 Unity Player Loop 的 Update 阶段结束时执行更新，是放置大部分游戏逻辑的主要位置。
* **PresentationSystemGroup（表现系统组）**：在 Unity Player Loop 的 PreLateUpdate 阶段结束时执行更新，通常用于处理渲染和表现层相关的逻辑。

自动引导（Automatic bootstrapping）会为每一个系统（System）和系统组（System Group）创建实例，带有 **`DisableAutoCreation`** 特性的除外。
 这些实例默认会被添加到 **`SimulationSystemGroup`** 中，除非通过 **`UpdateInGroup`** 特性进行了覆盖。

可以使用脚本定义禁用自动引导过程：

* \#UNITY\_DISABLE\_AUTOMATIC\_SYSTEM\_BOOTSTRAP\_RUNTIME\_WORLD：禁用默认值的自动引导 world。
* \#UNITY\_DISABLE\_AUTOMATIC\_SYSTEM\_BOOTSTRAP\_EDITOR\_WORLD：禁用自动引导 Editor world。
* \#UNITY\_DISABLE\_AUTOMATIC\_SYSTEM\_BOOTSTRAP：禁用默认 world 和 Editor 的自动引导 world。

当自动引导被禁用时，您的代码负责：

* 创建 worlds。
* 调用 World.GetOrCreateSystem\<T\>() 将系统（System）和系统组（System Group）实例添加到各个 World 中
* 将顶层系统组（例如 `SimulationSystemGroup`）注册到 Unity Player Loop 中进行更新。

或者，您可以通过创建实现 ICustomBootstrap 的类来自定义引导逻辑。

## worlds 和 systems 的时间

world 具有 Time 属性，该属性返回 TimeData 结构，其中包含帧增量时间和经过的时间。时间值由 world 的 UpdateWorldTimeSystem 更新。可以使用以下 World 方法来操纵时间值：

* SetTime：设置时间值。
* PushTime：临时更改时间值。
* PopTime：恢复上次推送之前的时间值。

某些系统组（System Group），例如 **FixedStepSimulationSystemGroup**，在更新其子系统之前会先“压入”（push）一个时间值，完成更新后再将该时间值“弹出”（pop）。本质上，这些系统组向其子系统提供的是一种“伪造的”（false）时间值

## SystemState

system 的 OnUpdate()、OnCreate() 和 OnDestroy() 方法传递 SystemState 参数。SystemState 表示 system 实例的状态，具有重要的方法和属性，包括：

* World：System所在的世界。
* EntityManager：该System所属 World 的 EntityManager。
* Dependency：用于在System之间传递 Job 依赖关系的 JobHandle。
* GetEntityQuery()：返回一个 EntityQuery。
* GetComponentTypeHandle<T>()：返回一个 ComponentTypeHandle<T>。
* GetComponentLookup<T>()：返回一个 ComponentLookup<T>

**重要**：尽管实体查询（entity queries）、组件类型句柄（component type handles）以及组件查找（component lookups）都可以直接从 EntityManager 获取，但通常一个系统更规范的做法是只通过 SystemState 来获取这些内容。通过 SystemState 获取时，系统所访问的组件类型会被追踪记录下来，这对于系统的 Dependency（依赖项）属性至关重要，使其能够在系统之间正确地传递 Job 依赖关系（这一点将在后面讨论）

## SystemAPI

SystemAPI 类具有许多静态便捷方法，涵盖与 World、EntityManager 和 SystemState 相同的大部分功能。

SystemAPI 方法依赖于源生成器，因此它们仅适用于 systems 和 IJobEntity（但不适用于 IJobChunk）。使用 SystemAPI 的优点是这些方法在两个上下文中产生相同的结果，因此使用 SystemAPI 的代码通常更容易在这两个上下文之间复制粘贴。

**注意**：如果您对在哪里寻找关键 Entities 功能感到困惑，一般规则是首先检查 SystemAPI。如果 SystemAPI 没有您要查找的内容，请查找 SystemState；如果您要查找的内容不存在，请查找 EntityManager 和 World。

SystemAPI 还提供了一种特别方便的 Query() 方法，该方法通过源代码生成，帮助在与 query 匹配的 entities 和 components 上创建 foreach 循环（在下面的示例中演示）。

# 在 jobs 中访问 entities

可以通过 C# Job System 将实体数据的处理卸载到工作线程中。Entities 包提供了两个用于定义访问实体的 Job 接口：

* IJobChunk，其 Execute() 方法对于与 query 匹配的每个单独的 chunk 调用一次。
* IJobEntity，对于每个与 ​​query 匹配的 entity 调用一次其 Execute() 方法。

虽然 IJobEntity 一般来说编写和使用起来更方便，但 IJobChunk 提供了更精确的控制。在大多数情况下，他们的表现在同等工作下是相同的。

**注意**：IJobEntity 实际上并不是“真正的”job 类型：源生成使用 IJobChunk 的实现扩展了 IJobEntity 结构，因此实际上，IJobEntity 最终被安排为 IJobChunk。

要跨多个线程拆分 IJobChunk 或 IJobEntity 的工作，schedule 通过调用 ScheduleParallel() 而不是 Schedule() 来实现 job。当您使用 ScheduleParallel() 时，与 query 匹配的 chunks 将被放入单独的批次中，并且这些批次将被外包给工作线程。

无法在 job 内部进行结构更改，因此您应该仅在主线程上进行结构更改。但是，job 可以在 EntityCommandBuffer（稍后讨论）中记录结构更改命令，然后可以稍后在主线程上回放这些命令。

## Sync points

### 1️⃣ 什么是 synchronization point（同步点）

在 ECS + Job System 里：

- 你可能在后台线程跑 Job（并行处理实体）
- 主线程也可能在同时修改实体数据

👉 为了避免数据冲突，某些主线程 API **必须强制等待所有相关 Job 完成**

这类操作就叫：

> synchronization point（同步点）

#### 举例

```c#
EntityManager.AddComponent<T>(entity);
```

这行代码会导致：👉 先把所有“正在运行 + 会访问 T 组件”的 Job 全部执行完（Complete）

然后才允许你继续修改结构。

------

同理：

```c#
entityQuery.ToEntityArray();
entityQuery.ToComponentDataArray<T>();
entityQuery.ToArchetypeChunkArray();
```

这些方法在执行前也会：👉 强制完成所有“可能读写该 Query 涉及组件”的 Job

### 2️⃣ 为什么会 “invalidate”（失效）

一些 DOTS 类型是“基于当前世界状态的缓存视图”，比如：

- DynamicBuffer
- ComponentLookup<T>

它们本质是：“某一时刻 ECS 数据结构的访问快照/索引器”

当发生同步点 + 结构变化时，比如：

- AddComponent
- RemoveComponent
- DestroyEntity
- Query 强制读取

👉 ECS 的底层存储结构可能已经变了

于是：旧的 `DynamicBuffer` / `ComponentLookup<T>` 就不再安全

### 3️⃣ 为什么会这样设计

因为 DOTS 保证：

- ✔ Job 安全
- ✔ 数据结构一致性
- ✔ 并行无冲突

代价是：任何“结构变化 + 同步点”都可能让旧访问句柄过期

### 4️⃣ 如果失效了怎么办？

如果你还需要使用这个实例，必须重新获取一个新的

比如：

```
var lookup = SystemAPI.GetComponentLookup<MyComponent>();
```

或者：

```c#
var buffer = SystemAPI.GetBufferLookup<MyBuffer>();
```

### 5️⃣一句话总结

在 DOTS 里，某些主线程操作会强制等待所有 Job 完成（同步点），而这些同步点可能改变 ECS 内部结构，从而导致 DynamicBuffer 和 ComponentLookup 这类“缓存访问句柄”失效，失效后必须重新获取，否则会触发安全检查异常

### 6️⃣DOTS 同步点 & 句柄失效流程图

```
            ┌──────────────────────────┐
            │   你在调度 Jobs            │
            │ (IJob / Entities.ForEach)│
            └──────────┬───────────────┘
                       │ 并行运行中
                       ▼
        ┌──────────────────────────────┐
        │ Job System 正在访问 ECS 数据    │
        │ (Component / Buffer / Query) │
        └──────────┬───────────────────┘
                   │ 主线程调用某些 API
                   ▼
     ┌────────────────────────────────────┐
     │      🚨 Synchronization Point      │
     │------------------------------------│
     │ EntityManager.AddComponent<T>()    │
     │ EntityQuery.ToEntityArray()        │
     │ EntityQuery.ToComponentDataArray() │
     └──────────┬─────────────────────────┘
                │ 强制发生：
                ▼
     ┌────────────────────────────────────┐
     │ 1. 等待所有相关 Jobs 完成 (Complete)  │
     │ 2. 可能发生 ECS 结构变化              │
     │    - Archetype 变化                 │
     │    - Chunk 重排                     │
     │    - Component 结构更新              │
     └──────────┬─────────────────────────┘
                │
                ▼
     ┌────────────────────────────────────┐
     │ ⚠ 旧的访问句柄可能“过期”              │
     │                                    │
     │ DynamicBuffer                      │
     │ ComponentLookup<T>                 │
     │ EntityQuery 缓存视图                │
     └──────────┬─────────────────────────┘
                │
                ▼
     ┌────────────────────────────────────┐
     │ ❌ Safety System 标记为 INVALID     │
     │                                    │
     │ 再调用它的方法 → 直接抛异常             │
     └──────────┬─────────────────────────┘
                │
                ▼
     ┌────────────────────────────────────┐
     │ ✔ 正确做法                          │
     │ 重新 GetComponentLookup / Buffer    │
     │ 或重新获取 SystemState / Query       │
     └────────────────────────────────────┘
```

------

## Component 安全句柄

和主线程上的普通 native collection 一样，每一种组件类型在每个 World 中都对应一个“Job 安全句柄（job safety handle）”。这意味着：对于任何两个访问同一组件类型的 Job，安全检查系统不会允许它们被同时调度并行执行。例如，当我们尝试调度一个访问组件类型 Foo 的 Job 时，如果当前已经存在另一个已调度的 Job 同样在访问 Foo，那么安全检查就会抛出异常。为了避免这个异常：

* 已调度的 job 必须在调度新的 job 之前完成
* 或者新的 job 必须取决于已安排的 job。

  **注意**：如果两个 jobs 都具有相同 component 类型的**只读**访问权限，那么同时调度它们是安全的。对于 job 中从未写入的任何 component 类型，请务必通过使用 **ReadOnly** 属性标记 component 类型句柄来通知安全检查。

DynamicBuffer\<T\> 实例本身拥有一个安全句柄：

* 当任何访问相同缓冲区 component 类型的调度 jobs 仍未完成时，无法访问 DynamicBuffer\<T\> 的内容。
* 然而，如果未完成的 jobs 都对 component 类型的缓冲区仅有只读访问权限，则允许主线程读取该缓冲区。

## SystemState.Dependency

当我们在 system 中使用 schedule 和 job 时，我们希望它依赖于可能与新的 job 冲突的任何当前调度的 jobs，即使这些 jobs 已调度在其他中 systems。为了帮助安排这些依赖关系，SystemState 有一个名为“依赖关系”的 JobHandle 属性。

Dependency 表示：**当前系统在开始执行之前，需要等待哪些 Job 完成。**Unity 会自动分析系统之间访问的组件类型。如果两个系统都读写同一种组件（例如都访问 `Foo`），Unity 就会认为它们存在依赖关系。当前系统更新前，Unity 会把这些相关系统尚未完成的 Job 全部合并

然后，您需要在每个 system 中遵循两条规则：

1. 在 system 更新中安排的所有 jobs 应（直接或间接）依赖于更新之前分配给依赖项的 job 句柄。
2. 在 system 更新返回之前，应为 Dependency 属性分配一个句柄，其中包含该更新中计划的所有 jobs。

只要您遵循这两条规则，system 更新中计划的每个 job 将取决于其他 systems 中计划的所有 jobs，这些 systems 可能访问任何相同的 component 类型。

**重要**：Systems 不跟踪它们使用的本机集合，因此 Dependency 属性仅考虑 component 类型，而不考虑本机集合。因此，如果两个 systems 和 schedule jobs 使用相同的本机集合，则它们的 Dependency job 句柄不一定包含在分配给另一个的 Dependency 属性的 job 句柄中，因此不同 systems 的 jobs 将不会像它们应该的那样相互依赖。在这些情况下，您可以通过在 systems 之间手动共享 job 句柄来安排依赖关系，但通常更好的解决方案是将本机集合存储在 component 中：如果遵循这两个规则，并且两个 systems 通过相同的 component 访问集合类型，则两个 systems 中调度的 jobs 应该相互依赖。

> ECS 只认识 Component，不认识 NativeCollection。
>
> 共享 NativeCollection 时，要么自己管理 JobHandle，要么把它包装到 Component 中，让 ECS 自动帮你处理依赖

## ComponentLookup\<T\>

单个实体（Entity）的组件可以通过 `EntityManager` 进行随机访问（即通过实体 ID 直接获取或修改组件）。但通常情况下，我们**不能在 Job 内部使用 `EntityManager`**。作为替代方案，我们可以使用 `ComponentLookup<T>`。它允许我们通过实体 ID 来获取或设置某种组件的值。同样地，如果需要通过实体 ID 获取动态缓冲区（Dynamic Buffer），则可以使用 `BufferLookup<T>`。

> **重要**：在 ECS 中，根据实体 ID 随机读取组件数据，往往会破坏数据的连续访问模式，从而导致 CPU 缓存未命中，降低性能。因此，只要能够通过顺序遍历（例如 `IJobEntity`、`Entities.ForEach`、Chunk 遍历等）完成工作，就应优先采用这些方式。不过，有些场景确实离不开随机查找，例如根据目标实体获取其状态、处理父子关系、图结构遍历等。因此，随机查找并非不能使用，只是应当谨慎使用，避免无意义或过度频繁的查找

如果指定的 entity 具有 component 类型 T，则 ComponentLookup\<T\> 和 BufferLookup\<T\> 方法 HasComponent() 返回 true。TryGetComponent\<T\>() 和 TryGetBuffer\<T\>() 方法执行相同的操作，但也会输出 component 值或缓冲区（如果存在）。

**注意**：为了测试 entity 是否简单存在，我们可以调用 EntityStorageInfoLookup 的 Exists()。对 EntityStorageInfoLookup 进行索引会返回 EntityStorageInfo 结构，其中包括对 entity 的 chunk 的引用及其在 chunk 中的索引。

如果 job 只需要读取通过 ComponentLookup\<T\> 访问的 components，则 ComponentLookup\<T\> 字段应标记为 ReadOnly 属性，以通知 job 安全检查。对于 BufferLookup\<T\> 也是如此。

在并行调度的 job 中，从 ComponentLookup\<T\> 获取 component 值需要使用 ReadOnly 属性标记该字段。安全检查不允许通过并行调度的 job 中的 ComponentLookup\<T\> 设置 component 值，因为无法保证安全。但是，您可以通过使用 NativeDisableParallelForRestriction 属性对其进行标记来完全禁用对 ComponentLookup\<T\> 的安全检查。对于 BufferLookup\<T\> 也是如此。只需确保您的代码以线程安全的方式设置 component 值即可！

# Entity 命令缓冲区

可以通过将命令记录到 EntityCommandBuffer 来推迟对 entities 的更改。当我们在主线程调用 Playback() 方法时，就会执行记录的命令。

使用 EntityCommandBuffer 推迟更改在 jobs 中特别有用，因为 jobs 无法直接进行结构更改（创建 entities，销毁 entities，添加 components，或删除 components）。相反，jobs 可以将命令记录在 EntityCommandBuffer 中，以便在 job 完成后在主线程上回放。EntityCommandBuffers 还可以通过将结构更改推迟到帧的几个合并点而不是分散在整个帧中来帮助我们避免不必要的同步点。

EntityCommandBuffer 有许多（但不是全部）与 EntityManager 相同的方法。这些方法包括：

* CreateEntity()：记录创建新 entity 的命令。返回临时 entity ID。
* DestroyEntity()：记录销毁 entity 的命令。
* AddComponent\<T\>()：记录将 T 类型的 component 添加到 entity 的命令。
* RemoveComponent\<T\>()：记录从 entity 中删除类型 T 的 component 的命令。
* SetComponent\<T\>()：记录设置 T 类型的 component 值的命令。
* AppendToBuffer()：记录将单个值附加到现有缓冲区 component 末尾的命令。
* AddBuffer()：返回一个 DynamicBuffer，该 DynamicBuffer 存储在录制的命令中，并且在播放时创建该缓冲区时，该缓冲区的内容将被复制到 entity 的实际缓冲区中。实际上，写入返回的缓冲区允许您设置缓冲区 component 的初始内容。
* SetBuffer()：与 AddBuffer() 类似，但它假设 entity 已经具有 component 类型的缓冲区。播放时，entity 已存在的缓冲区内容将被返回的缓冲区内容覆盖。

  **注意**：某些 EntityManager 方法没有 EntityCommandBuffer 等效方法，因为等效方法不可行或没有意义。例如，没有用于获取 component 值的 EntityCommandBuffer 方法，因为读取数据不是可以有效延迟的事情。

  **注意**：EntityCommandBuffer 实例播放完毕后，不能再用于追加录制。如果您需要记录更多命令，请创建一个新的、单独的 EntityCommandBuffer 实例。

每个 EntityCommandBuffer 都有一个 job 安全句柄，因此如果您执行以下操作，安全检查将引发异常：

* 在主线程上调用 EntityCommandBuffer 的方法，同时 EntityCommandBuffer 仍在由任何当前调度的 jobs 使用。
* schedule 一个 job，它访问已被其他当前安排的 jobs 使用的 EntityCommandBuffer（除非新的 job 取决于其他 jobs）。

  **重要**：您可能会想在多个 jobs 之间共享单个 EntityCommandBuffer 实例，但强烈建议不要这样做。在某些情况下它可以正常工作，但在许多情况下却不能。例如，在多个并行 jobs 中使用相同的 EntityCommandBuffer.ParallelWriter 可能会导致命令的意外播放顺序。相反，实际上最好为每个 job 创建和使用一个 EntityCommandBuffer。不必担心性能差异：记录和回放分散在多个 EntityCommandBuffer 中的一组命令实际上并不比将同一组命令全部记录到一个 EntityCommandBuffer 中更昂贵。

## 临时 entity IDs

当您调用 EntityCommandBuffer 的 CreateEntity() 或 Instantiate() 方法时，在播放中执行命令之前不会创建新的 entity，因此这些方法返回的 entity ID 是*临时的 ID's*，其索引号为负。相同 EntityCommandBuffer 的后续 AddComponent、SetComponent 和 SetBuffer 命令可以使用这些临时 ID。在播放时，录制命令中的任何临时 ID 将被重新映射到实际的现有 entities。

**重要**：因为临时 entity ID 在创建它的 EntityCommandBuffer 实例之外没有任何意义，所以临时 entity ID 只能在相同的后续方法调用中使用 EntityCommandBuffer 实例。例如，在将命令记录到不同的 EntityCommandBuffer 实例时，请勿使用从一个 EntityCommandBuffer 获得的临时 ID。

## EntityCommandBuffer.ParallelWriter

为了安全地记录来自并行 job 的命令，我们需要一个 EntityCommandBuffer.ParallelWriter，它是底层 EntityCommandBuffer 的包装器。ParallelWriter 具有与 EntityCommandBuffer 本身相同的大部分方法，但为了确定性，ParallelWriter 方法都采用额外的“排序键”参数。

当 EntityCommandBuffer.ParallelWriter 在并行 job 中记录命令时，从不同线程记录的命令在缓冲区中的顺序取决于线程调度，使得顺序不确定。这并不理想，因为：

* 确定性代码通常更容易调试。
* 一些Netcode解决方案依赖于确定性来在不同的机器上产生一致的结果。

虽然命令的记录顺序无法确定，但可以通过一个简单的技巧来确定播放顺序：

1. 每个命令都会记录一个“排序键”整数，作为每个命令方法的第一个参数传递。
2. Playback() 方法在执行命令之前按排序键对命令进行排序。

只要排序键值确定性地映射到每个记录的命令，排序就会使播放顺序确定。

在 IJobEntity 中，我们通常要使用的排序键是 ChunkIndexInQuery，它对于每个 chunk 来说都是唯一的值。由于排序是稳定的，并且单个 chunk 的所有 entities 都在单个线程中一起处理，因此该索引值适合作为记录命令的排序键。在 IJobChunk 中，我们可以使用 Execute 方法的等效 unfilteredChunkIndex 参数。

## 多重播放

如果使用 PlaybackPolicy.MultiPlayback 选项创建 EntityCommandBuffer，则可以多次调用其 Playback 方法。否则，多次调用 Playback 将引发异常。当您想要重复生成一组 entities 时，多重播放主要有用。

## EntityCommandBufferSystem

EntityCommandBufferSystem 是 system，它提供了推迟 EntityCommandBuffer 播放的便捷方法。从 EntityCommandBufferSystem 创建的 EntityCommandBuffer 实例将在下次 EntityCommandBufferSystem 更新时播放并处置。

**重要**：不要手动回放和处置由 EntityCommandBufferSystem 创建的 EntityCommandBuffer 实例：EntityCommandBufferSystem 将为您回放和处置该实例。

您很少需要自己创建任何 EntityCommandBufferSystems，因为自动引导过程会将这五个放入默认的 world 中：

* BeginInitializationEntityCommandBufferSystem
* EndInitializationEntityCommandBufferSystem
* BeginSimulationEntityCommandBufferSystem
* EndSimulationEntityCommandBufferSystem
* BeginPresentationEntityCommandBufferSystem

例如，EndSimulationEntityCommandBufferSystem 在 SimulationSystemGroup 末尾更新。

**注意**：帧末尾没有“EndPresentationEntityCommandBufferSystem”，但您可以使用 BeginInitializationEntityCommandBufferSystem 代替：一帧的结束和下一帧的开始在逻辑上是相同的时间点。

# Transform components 和 systems

LocalTransform 是主要标准 component，表示 entity 的变换。Transform 层次结构可以通过三个附加 components 形成：

* Parent component 存储 entity 的父级的 id。
* 子动态缓冲区 component 存储 entity 子项的 id。
* PreviousParent component 存储 entity 的父级 id 的副本。

要修改转换层次结构：

* 将父级 ​​component 添加为 entity 的父级。
* 删除 entity 的父级 component 以取消其父级。
* 设置 entity 的父级 component 以更改其父级。

ParentSystem 将确保：

* 每个具有父级的 entity 都有一个引用父级的 PreviousParent component。
* 每个具有一个或多个子项的 entity 都有一个引用其所有子项的子缓冲区 component。

  **重要**：虽然您可以安全地读取 entity 的子缓冲区和 PreviousParent components，但您不应该直接修改它们。您只能通过设置 entities 的父级 components 来修改转换层次结构。

每一帧，LocalToWorldSystem 计算每个 entity 的 world 空间变换（来自 entity 及其祖先的 LocalTransform components）并将其分配给 entity 的 LocalToWorld component。

**注意**：Entity.Graphics systems 读取 LocalToWorld component，但不读取任何其他转换 components，因此 LocalToWorld 是唯一的转换 component entity 需要渲染。

# Baking 和 entity scenes

*Baking* 是一个构建时进程，通过执行 *bakers* 和 *baking* 从 *sub scenes* 创建 *entity scenes* systems*：

* **entity 场景**e 是可在运行时加载的 entities 和 components 的序列化集。
* **子 scene** 是 Unity scene 资产，由 SubScene MonoBehaviour 嵌入到另一个 scene 中。
* **baker** 是扩展 Baker\<T\> 的类，其中 T 是 MonoBehaviour。带有 Baker 的 MonoBehaviour 称为“authoring component”。
* **baking system** 是一个带有 \[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)\] 属性标记的 system。（Baking systems 通常仅在高级用例中需要。）

子 scene 的烘焙分为几个主要步骤：

1. 对于子 scene 的每个 GameObject，创建对应的 entity。
2. 执行子 scene 中每个 authoring component 的 baker。每个 baker 都可以读取 authoring component 并将 components 添加到对应的 entity 中。
3. 执行 baking systems。每个 system 都可以读取和修改烘焙后的 entities。与 bakers 不同，baking systems 不应访问子 scene 的原始 GameObjects。

修改后，会重新烘焙一个子 scene：

1. 仅重新执行修改后的 authoring components 的 bakers。
2. baking systems 始终完全重新执行。
3. 编辑模式或播放模式下的实时 entities 会更新以匹配 baking 的结果。（这是可能的，因为 baking 跟踪烘焙的 entities 与实时 entities 的对应关系。）

## 创建和编辑子 scenes

带有 SubScene MonoBehaviour 的 GameObject 有一个复选框，用于打开和关闭子 scene 进行编辑。当子 scene 打开时，其 GameObjects 会被加载并占用 Unity 编辑器中的资源，因此您可能需要关闭当前未编辑的大型子 scenes。

![][图片 2]

创建新子 scene 的便捷方法是在 Hierarchy 窗口中右键单击并选择 *New Subscene \> Empty Scene...* 这将创建一个新的 scene 文件和一个带有 SubScene component 引用新的 scene 文件：

![][图片 3]

## 访问 baker 中的数据

增量 baking 需要 bakers 来跟踪它们读取的所有数据。baker 的 authoring component 的字段会自动跟踪，但 baker 读取的其他数据必须通过 Baker 方法添加到其依赖项列表中：

* GetComponent\<T\>()：访问子 scene 中 GameObject 的 component。
* DependsOn()：声明应为此 baker 跟踪资产。
* GetEntity()：返回在子 scene 中烘焙的 entity 或从 prefab 烘焙的 entity 的 id。（entity 尚未完全烘焙，因此您不应尝试通过此 id 读取或修改 entity 的 components。）

## 装卸 entity scenes

出于流传输的目的，scene 的 entities 被分为由索引号标识的部分。entity 所属的段由其 SceneSection 共享 component 指定。默认情况下，entity 属于第 0 节，但这可以通过在 baking 期间设置 SceneSection 来更改。

**重要**：在 baking 期间，子 scene 中的 entities 只能引用同一节或节 0\的其他 entities。（第 0 节是一个特殊情况，因为它总是在其他节之前加载，并且仅在 scene 本身卸载时才卸载）。

当加载 scene 时，它由 entity 表示，其中包含有关 scene 的元数据，并且其每个部分也由 entity 表示。通过操作其 entity 的 RequestSceneLoaded component 来加载和卸载单个部分：当此 component 更改时，SceneSystemGroup 中的 SceneSectionStreamingSystem 将做出响应。

要从代码中加载和卸载 entity scenes，请使用 SceneSystem 的静态方法：

* LoadSceneAsync()：启动 scene 的加载。返回表示加载的 scene 的 entity。
* LoadPrefabAsync()：启动 prefab 的加载。返回引用加载的 prefab 的 entity。
* UnloadScene()：销毁已加载的 scene 的所有 entities。
* IsSceneLoaded()：如果加载了 scene，则返回 true。
* IsSectionLoaded()：如果加载了某个部分，则返回 true。
* GetSceneGUID()：返回表示 scene 资产（由其文件路径指定）的 GUID。
* GetScenePath()：返回 scene 资产的路径（由其 GUID 指定）。
* GetSceneEntity()：返回表示 scene（由其 GUID 指定）的 entity。

  **重要**：Entity scene 和节加载始终是异步的，并且不能保证请求后需要多长时间才能加载数据。在大多数情况下，代码应该检查是否存在从 scenes 加载的特定数据，而不是检查 scenes 本身的加载状态。这种方法避免了将代码束缚到特定的 scenes：如果数据移动到不同的 scene、从网络下载或按程序生成，则代码仍将无需修改即可工作。

# 附加功能

## 托管 IComponentData components

实现 IComponentData 的类是托管 component 类型。与非托管的 IComponentData 结构不同，这些托管 components 可以存储任何托管对象。

一般来说，托管 component 类型应仅在真正需要时使用，因为与非托管 components 相比，它们会产生一些沉重的成本：

* 与所有托管对象一样，托管 components 不能在 Burst 编译的代码中使用。
* 托管对象通常无法在 jobs 中安全使用。
* 托管的 components 不直接存储在 chunks 中：相反，world 的所有托管 components 都存储在一个大数组中，而 chunks 仅存储该数组的索引。
* 与所有托管对象一样，创建托管 components 会产生垃圾收集开销。

为了确保在复制 component 本身时复制托管 component 包含的任何资源，component 应实现 ICloneable。为了确保当托管 component 被销毁时，托管 component 包含的任何资源都得到正确处置，component 应实现 IDisposable。

## 启用 components

实现 IComponentData 或 IBufferElementData 的结构也可以实现 IEnableableComponent。实现该接口的 component 类型可以根据 entity 启用和禁用。

当 entity 的 component 被禁用时，查询会认为 entity 不具有 component 类型：

* 在 SystemAPI.Query()、IJobEntity 以及迭代与 query 匹配的 chunk 的 entities 的其他上下文中，与 query 不匹配的单独 entities 因为它们的 components 的启用或禁用状态将被跳过。
* 如果由于 components 的启用或禁用状态，chunk 中没有 query 与 query 匹配，则 chunk 将不会包含在 ToArchetypeChunkArray() 方法返回的数组中 EntityQuery。

请注意，禁用 component 不会删除或修改 component：相反，与特定 entity 的特定 component 关联的位会被切换。另请注意，禁用的 component 仅影响查询：禁用的 component 仍可以正常读取和修改，例如*通过* GetComponent\<T\>() 的 EntityManager 方法。

所有可启用的 components 在添加到 entity 时默认启用。当复制 entity 进行序列化、复制到另一个 world 或通过 EntityManager 的 Instantiate 方法复制时，components 的使能状态也会被复制。

可以通过以下方式检查和设置 entity 的 components 的启用状态：

* EntityManager
* ComponentLookup\<T\>
* BufferLookup\<T\>
* EnabledRefRW\<T\>
* ArchetypeChunk

例如，EntityManager 包括以下方法：

* IsComponentEnabled\<T\>()：如果 entity 具有当前启用的 T component，则返回 true。
* SetComponentEnabled\<T\>()：启用或禁用 entity 的可启用 T component。

  **注意**：为了进行 job 安全检查，对 component 启用状态的读或写访问需要对 component 类型本身进行读或写访问。

在 IJobChunk 中，执行方法参数指示 chunk 中的 entities 与 query 匹配：

* 如果 useEnableMask 参数为 false，则 chunk 中的所有 entities 都与 query 匹配。
* 否则，如果 useEnableMask 参数为 true，则 chunkEnabledMask 参数的位表示 chunk 中的 entities 与 query 匹配，考虑到所有可启用的 component 类型 query。您可以使用 ChunkEntityEnumerator 更方便地迭代匹配的 entities，而不是手动检查这些掩码位。

  **注意**：chunkEnabledMask 是 job 的 query 中包含的可启用 components 的所有启用状态的组合。要检查各个 components 的启用状态，请使用 ArchetypeChunk 的 IsComponentEnabled() 和 SetComponentEnabled() 方法。

## Aspects

方面是 entity 的 components 子集上的类似对象的包装器。Aspects 对于简化查询和 component 相关代码很有用。例如，我们可以定义一个“MonsterAspect”，将包含怪物 entity 的 components 组合在一起。

方面被定义为实现 IAspect 的只读部分结构。该结构可以包含以下类型的字段：

* Entity：包装后的 entity 的 entity ID。
* RefRW\<T\> 或 RefRO\<T\>：对包装的 entity 的 T component 的引用。
* EnabledRefRW\<T\> 和 EnabledRefRO\<T\>：对包装的 entity 的 T component 的启用状态的引用。
* DynamicBuffer\<T\>：包装的 entity 的动态缓冲区 T component。
* 另一种方面类型：包含方面将涵盖“嵌入”方面的所有字段。

在 query 中包含一个方面与包含由该方面包装的所有单独的 components 相同。

这些 EntityManager 方法创建方面的实例：

* GetAspect\<T\>：返回包装 entity 的类型 T 的一个方面。
* GetAspectRO\<T\>：返回包装 entity 的类型 T 的只读方面。如果您使用任何尝试修改基础 components 的方法或属性，只读方面会引发异常。

Aspect 实例还可以通过 SystemAPI.GetAspectRW\<T\> 或 SystemAPI.GetAspectRO\<T\> 检索，并在 IJobEntity 或 SystemAPI.Query() 循环中访问。

**重要**：您通常应该*通过* SystemAPI 而不是 EntityManager 获取方面实例：与 EntityManager 方法不同，SystemAPI 方法使用 system 注册方面的底层 component 类型，这对于 systems 正确地 schedule jobs 与他们需要的每个依赖项。

## 共享 components

对于*共享 component* 类型，chunk 中的所有 entities 共享相同的 component 值，而不是每个 entity 都有自己的值。因此，设置 entity 的共享 component 值会执行结构更改：entity 被移动到具有新值的 chunk。例如，如果 entity 具有 *Foo* 共享 component 值 X，则 entity 存储在具有 *Foo* 值 X 的 chunk 中；如果随后将 entity 设置为具有 *Foo* 值 Y，则 entity 会移动到具有值 Y 的 chunk；如果不存在这样的 chunk，则创建一个新的 chunk。

共享 components 的主要用途来自于查询可以筛选特定共享 component 值的事实。例如，包含共享 component *Foo* 的 query 可以包含一个过滤器，指定它应仅与 *Foo* 值 X 的 entities 匹配。

world 不是将共享的 component 值直接存储在 chunks 中，而是将它们存储在一组数组中，而 chunks 存储只是对这些数组进行索引。这意味着每个唯一的共享 component 值在 world 中仅存储一次。

**注意**：可以通过为该类型实现 IEquatable\<T\> 来自定义如何比较共享 component 类型是否相等以确定唯一性。

共享 component 类型被声明为实现 ISharedComponentData 的结构。如果结构体包含任何托管类型字段，则共享 component 本身将被视为托管 component 类型，具有与托管 IComponentData 相同的优点和限制。

EntityManager 具有共享 components 的以下关键方法：

* AddComponent\<T\>()：将 T component 添加到 entity，其中 T 可以是共享 component 类型。
* AddSharedComponent()：将非托管共享 component 添加到 entity 并设置其初始值。
* AddSharedComponentManaged()：将托管共享 component 添加到 entity 并设置其初始值。
* RemoveComponent\<T\>()：从 entity 中删除 T component，其中 T 可以是共享的 component 类型。
* HasComponent\<T\>()：如果 entity 当前具有 T component，则返回 true，其中类型 T 可以是共享 component 类型。
* GetSharedComponent\<T\>()：检索 entity 的非托管共享 T component 的值。
* SetSharedComponent\<T\>()：覆盖 entity 的非托管共享 T component 的值。
* GetSharedComponentManaged\<T\>()：检索 entity 的托管共享 T component 的值。
* SetSharedComponentManaged\<T\>()：覆盖 entity 的托管共享 T component 的值。

  **重要**：由于 EntityManager 依赖于相等性来识别唯一且匹配的共享 component 值，因此您应该避免修改共享 components 引用的任何可变对象。例如，如果要修改存储在特定 entity 的共享 component 中的数组，则不应直接修改该数组，而应更新该 entity 的 component 以获得该数组的新的、修改后的副本。

  **重要**：拥有太多唯一共享 component 值可能会导致 chunk 碎片\！由于 chunk 中的所有 entities 必须共享相同的共享 component 值，因此如果您将唯一的共享 component 值赋予大量 entities，则 entities 最终将在许多 chunks。例如，如果 archetype 有 500 个 entities 具有共享的 component，并且每个 entity 都有唯一的共享 component 值，则每个 entity 都单独存储在单独的 chunk。这浪费了每个 chunk 中的大部分空间，并且还意味着循环遍历 archetype 的所有 entities 需要访问 500 个 chunks。这种碎片很大程度上抵消了 ECS 结构的性能优势。为了避免此问题，请尝试使用尽可能少的唯一共享 component 值。例如，如果 500 个 entities 仅共享 10 个唯一的共享 component 值，则它们可以存储在少至 10 个 chunks 中。

## 清理 components

*Cleanup components* 有两个特殊之处：

* 当具有清理 components 的 entity 被销毁时，非清理 components 将被删除，但 entity 实际上继续存在，直到您单独删除其所有清理 components。
* 当一个 entity 复制到另一个 world、以序列化方式复制或通过 EntityManager 的 Instantiate 方法复制时，原始 components 的任何清理都不会添加到新的 entity 中。

清理 components 的主要用例是在 entities 创建后帮助初始化，或在 entities 销毁后清理 entities。例如，假设我们有 entities 代表怪物，它们都具有 *Monster* 标签 component：

1. 我们可以通过查询所有具有 *Monster* component 但没有 *MonsterCleanup* component 的 entities 来找到所有需要初始化的怪物 entities。对于与此 query 匹配的所有 entities，我们执行任何所需的初始化并添加 *MonsterCleanup*。
2. 通过查询所有具有 *MonsterCleanup* component 但不具有 *Monster* component 的 entities，我们可以找到所有需要清理的怪物 entities。对于与此 query 匹配的所有 entities，我们执行任何所需的清理并删除 *MonsterCleanup*。除非 entities 有额外的剩余清理 components，否则这将破坏 entities。

   **注意**：在某些情况下，您需要在清理 components 中存储清理所需的信息，但在许多情况下，空的清理标记 component 就足够了。

清理 components 有四种类型：

* 实现 ICleanupComponentData 的结构：非托管 IComponentData 类型的清理变体。
* 实现 ICleanupComponentData 的类：托管 IComponentData 类型的清理变体。
* 实现 ICleanupBufferElementData 的结构：动态缓冲区类型的清理变体。
* 实现 ICleanupSharedComponentData 的结构：共享 component 类型的清理变体。

## Chunk components

*chunk component* 是属于 chunk 的单个值。

**注意**：共享 components 还为每个 chunk 存储一个值，但共享 component 值逻辑上属于 entities，而不是 chunk（这就是为什么设置 entity 的共享值） component 值将 entity 移动到另一个 chunk，而不是修改存储在 chunk 中的值。Chunk components 真正属于 chunk 本身，并且与非托管共享 components 不同，非托管 chunk components 直接存储在 chunk。

chunk component 被定义为实现 IComponentData 的结构或类，但使用以下 EntityManager 方法添加、删除、获取和设置 chunk component：

* AddChunkComponentData\<T\>：将 T 类型的 chunk component 添加到 chunk，其中 T 是托管或非托管 IComponentData。
* RemoveChunkComponentData\<T\>：从 chunk 中删除类型 T 的 chunk component，其中 T 是托管或非托管 IComponentData。
* HasChunkComponent\<T\>：如果 chunk 具有 T 类型的 chunk component，则返回 true。
* GetChunkComponentData\<T\>：检索类型 T 的 chunk 的 chunk component 的值。
* SetChunkComponentData\<T\>：设置类型 T 的 chunk 的 chunk component 的值。

## 斑点资产

Blob（二进制大型对象）资产是存储在连续字节块中的不可变（不变）、非托管的二进制数据：

* Blob 资源的复制和加载效率很高，因为它们是完全可重定位的：所有内部指针都表示为相对偏移量而不是绝对地址，因此复制整个 Blob 就像复制每个字节一样简单。
* 尽管它们独立于 entities 存储，但 Blob 资源可以从 entity components 引用。
* 由于 Blob 资产是不可变的，因此从多个线程访问它们本质上是安全的。

  **注意**：Blob“资产”这个名称有点误导：Blob 资产是内存中的一段数据，而不是项目资产文件\！然而，Blob 资产可以高效且轻松地序列化为磁盘上的文件，因此从这个意义上说，将它们称为“资产”是合适的。

要创建 Blob 资源：

1. 创建 BlobBuilder。
2. 调用构建器的 ConstructRoot\<T\> 来设置 Blob 的“根”（T 类型的结构体）。
3. 调用构建器的 Allocate\<T\>、Construct\<T\> 和 SetPointer\<T\> 方法来填充其余的 Blob 数据（包括 BlobArrays、BlobStrings 和 BlobPtr)。
4. 调用构建器的 CreateBlobAssetReference，它会复制构建器中的所有数据以创建实际的 Blob 资产并返回 BlobAssetReference。
5. 废弃 BlobBuilder。

当不再需要 Blob 资产时，应通过调用 BlobAssetReference 上的 Dispose 来处置它。

烘焙的 entity scene 中引用的 Blob 资源将被序列化并与 scene 一起加载。这些 Blob 资产不应手动处置：它们将与 scene 一起自动处置。

**重要**：包含内部指针的 Blob 资源的所有部分都必须始终通过引用进行访问。例如，BlobString 结构中的偏移值仅相对于 BlobString 结构在 Blob 内的存储位置而言才是正确的；相对于结构副本的偏移量不正确。

## 版本号

world、其 systems 及其 chunks 维护多个“版本号”（通过某些操作递增的数字）。通过比较版本号，您可以确定某些数据是否已更改。

所有版本号都是 32 位有符号整数，因此当递增时，它们最终可能会在程序的生命周期中回绕。比较版本号的正确方法依赖于 C# 如何定义有符号整数溢出的微妙怪癖：

```java
// true if VersionB is more recent than VersionA
// false if VersionB is equal or less recent than VersionA
bool changed = (VersionB - VersionA) > 0;
```

版本号包括：

* World.Version：每次 world 添加或删除 system 或 system 组时都会增加的版本号。
* EntityManager.GlobalSystemVersion：在 world 中每次 system 更新之前增加的版本号。
* SystemState.LastSystemVersion：system 的版本号，每次更新 system 后立即分配 GlobalSystemVersion 的值。
* EntityManager.EntityOrderVersion：每次 world 中进行结构更改时版本号都会增加。
* 每个 component 类型都有自己的版本号，该版本号会通过获得对 component 类型的写访问权限的任何操作来递增。该号码可以通过调用方法 EntityManager.GetComponentOrderVersion 来检索。
* 每个共享 component 值还具有版本号，每次结构更改影响具有该值的 chunk 时，版本号都会增加。
* chunk 在 chunk 中存储每个 component 类型的版本号。当 chunk 中的 component 类型被访问进行写入时，其版本号将被分配为 EntityManager.GlobalSystemVersion 的值，无论是否实际修改了任何 component 值。这些 chunk 版本号可以通过调用 ArchetypeChunk.GetChangeVersion 方法来检索。
* chunk 还存储版本号，每次结构更改影响 chunk 时，该版本号都会被分配 EntityManager.GlobalSystemVersion 的值。可以通过调用 ArchetypeChunk.GetOrderVersion 方法来检索此版本号。
