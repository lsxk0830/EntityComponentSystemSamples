# 创建自定义 chunk 序列化方法

此示例演示如何实现和注册自定义序列化方法，以序列化给定 archetype 的 ghost chunks。

## 为什么使用自定义序列化函数

只有一个原因：**性能**！当使用为特定 archetype 设计的自定义序列化函数时，
您可以做出（或放松）某些假设：I.e。
* 没有检查已删除的 components。
* 优化收集和复制数据的循环。
* 一般来说，编写东西的方式应该为编译器和突发事件提供更多的机会
  自动矢量化。

一般来说，在 ghost 具有大量小 components 的情况下，它还会带来 CPU 性能增益，从而减少函数指针调用开销（更重要的是单次调用设置开销）。

### 自定义序列化器 API 的当前限制
这是 Netcode package 的高级功能（我们可以说它是“预览版”），目前仅在使用 PrefabCreation API (i.e。手动创建的 ghost 时可用 entities 类型）。

# USING THE API
## 注册自定义序列化器
为了使用自定义的 chunk 序列化函数；**该函数必须先注册到 GhostCollection system，然后
收集并处理prefab。** 进行注册的好地方是在创建prefab的 system 中。

要注册自定义序列化函数，您需要检索 `GhostCollectionCustomSerializer` 单例，
并为给定的 archetype 添加条目。ghost archetype 由其 GhostType 哈希值标识。

```csharp
GhostPrefabCreation.ConvertToGhostPrefab(EntityManager, prefab, prefabConfig);
var hash = (Unity.Entities.Hash128)EntityManager.GetComponentData<GhostType>(prefab);
var customSerializers = SystemAPI.GetSingletonRW<GhostCollectionCustomSerializers>();
customSerializers.ValueRW.Serializers.Add(hash, new GhostPrefabCustomSerializer
{
    SerializeChunk = CustomChunkSerializer.SerializerFunc,
    PreSerializeChunk = CustomChunkSerializer.PreSerializerFunc
});
```

`SerializeChunk` 和 `PreSerializeChunk` 函数都是可选的，这意味着您可以拥有其中之一，或两者都拥有。

## 为 archetype 注册自定义 component 列表提供程序
为了能够序列化 component，需要从 chunk 访问和检索 component 数据。
这是通过访问为此 archetype 注册的序列化 components 列表，并从中检索每个 component 类型的类型句柄列表（`DynamicTypeHandle`）来实现的。

`GhostCollectionSystem` 处理的每个 prefab 都有：
- `GhostCollectionPrefabSerializer` 列表中的一个条目，包含一堆用于序列化类型的元数据信息。
- 每个序列化的 component 将在 `GhostCollectionComponentIndex` 中创建一个条目（按顺序用于根和子 entities），其中包含（除其他外）：
  - 使用哪个序列化器（SerializerIndex 字段）
  - `DynamicTypeList` (ComponentIndex) 中的索引可让您检索 `DynamicComponentTypeHandle`。

```csharp
var typeData = ghostCollectionPrefabSerializer[ghostType];
var componentIndex = typeData.FirstComponent;
var index = componentIndices[componentIndex];
var dynamicTypeHandle = dynamicTypeList[index.ComponentIndex];
```

编写自定义序列化方法时，查找特定 component 类型的索引可能很棘手
在 `componentIndices` 列表中，对于当前的 ghost archetype。这是因为 components 在列表中的位置取决于：
- 其稳定类型哈希
- 与该 archetype 的特定 component 关联的序列化器哈希。

因此，我们支持将 `CollectComponents` 函数指针传递给 GhostPrefabCreation.ConvertToGhostPrefab`（配置参数的一个字段），
这样您就可以准确地知道特定 component 类型的 `componentIndices` 列表中的索引。

例如：

```csharp
public static void CollectComponents(IntPtr componentTypesPtr, IntPtr componentCountPtr)
{
    ref var componentTypes = ref GhostComponentSerializer.TypeCast<NativeList<ComponentType>>(componentTypesPtr);
    ref var componentCount = ref GhostComponentSerializer.TypeCast<NativeArray<int>>(componentCountPtr);
    //Root
    componentTypes.Add(ComponentType.ReadWrite<GhostOwner>());
    componentTypes.Add(ComponentType.ReadWrite<LocalTransform>());
    componentTypes.Add(ComponentType.ReadWrite<IntCompo1>());
    componentTypes.Add(ComponentType.ReadWrite<IntCompo2>());
    componentTypes.Add(ComponentType.ReadWrite<IntCompo3>());
    componentTypes.Add(ComponentType.ReadWrite<FloatCompo1>());
    componentTypes.Add(ComponentType.ReadWrite<FloatCompo2>());
    componentTypes.Add(ComponentType.ReadWrite<FloatCompo3>());
    componentTypes.Add(ComponentType.ReadWrite<InterpolatedOnlyComp>());
    componentTypes.Add(ComponentType.ReadWrite<OwnerOnlyComp>());
    componentTypes.Add(ComponentType.ReadWrite<Buf1>());
    componentTypes.Add(ComponentType.ReadWrite<Buf2>());
    componentTypes.Add(ComponentType.ReadWrite<Buf3>());
    componentCount[0] = 13;
    //Child 1
    componentTypes.Add(ComponentType.ReadWrite<IntCompo1>());
    componentTypes.Add(ComponentType.ReadWrite<FloatCompo1>());
    componentTypes.Add(ComponentType.ReadWrite<Buf1>());
    componentCount[1] = 3;
    //Child 2
    componentTypes.Add(ComponentType.ReadWrite<IntCompo2>());
    componentTypes.Add(ComponentType.ReadWrite<FloatCompo2>());
    componentTypes.Add(ComponentType.ReadWrite<Buf2>());
    componentCount[2] = 3;
}
```

您可以在 `CustomChunkSerializer.cs` 文件中找到此函数。

当该函数存在时，该 archetype 的 components 列表使用该方法指定的顺序，使您可以
一致地访问 component 类型信息，并且还允许您定义 component 序列化顺序。

## 实现自定义 chunk 序列化器

我们提供了一系列实用方法，可用于序列化内部的使能位、缓冲区和 components
`CustomGhostSerializerHelpers` 类。
`CustomChunkSerializer.cs` 可以被认为是一种模板，您几乎可以重用它来编写或生成序列化器
自动（通过调用提供者帮助函数）。

为了使代码尽可能可重用（并减少错误），您必须重用生成的 component 序列化器结构
（i.e `MyAssembly.Generated.MyComponentGhostComponentSerializer`）和：
- 在该序列化器的实例上调用 `CopyToSnapshot<T>` 方法之一。
- 在该序列化器的实例上调用 `SerializeWithSingleBaseline` 或 `SerializeWithThreeBaseline` 或 `SerializeBuffer` 之一。

不幸的是，鉴于 `MyAssembly.Generated.MyComponentGhostComponentSerializer` 序列化器是自动生成的，大多数 IDE 不会帮助自动完成方法名称，也不会识别类的存在。但代码会正确编译。

序列化分为两步：

### STEP 1 - COPY TO SNAPSHOT
您只需通过复制使能位（如有必要）和复制 component 数据来实现 `CustomChunkSerializer.CopyComponentsToSnapshot` 方法。例如：

```csharp

new Unity.NetCode.Generated.GhostOwnerGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
    ghostChunkComponentTypesPtr, indices[0], snapshotPtr, ref snapshotOffset);
new Unity.NetCode.Generated.TransformDefaultVariantGhostComponentSerializer().CopyComponentToSnapshot(chunk, ref context,
    ghostChunkComponentTypesPtr,indices[1], snapshotPtr, ref snapshotOffset);
CustomGhostSerializerHelpers.CopyEnableBits(chunk, context.startIndex, context.endIndex, context.snapshotStride,
    ref ghostChunkComponentTypesPtr[indices[2].ComponentIndex], enableBits, ref maskOffset);
```

同样，对于子 entity components，我们有类似的方法，只是构建一些实现细节和样板模板代码。

### STEP 2 - SERIALIZE TO THE DATASTREAM
将数据复制到 snapshot 缓冲区后，我们可以将 snapshot（entity by entity）序列化到数据流中。根据确认基线，我们应该通过以下任一方式进行序列化：
- 单个或默认基线
- 三个基线。

`CustomChunkSerializer` 类有两个方法需要实现：
- `SerializeWithSingleBaseline`
- `SerializeWithThreeBaseline`

所有代码生成的序列化器都提供三种静态方法可用于将 snapshot 数据写入流：
- `SerializeSingleBaseline`：仅使用一个基线（acked 或默认基线）序列化数据。
- `SerializeThreeBaseline`：使用最后三个基线（全部已确认）序列化数据并通过预测值来减少增量。
- `SerializeBuffer`：使用单个基线（默认基线或已确认的基线）将缓冲区序列化到流。

有关如何使用它们的示例，请参阅 `CustomChunkSerializer.cs`。但作为一个简短的例子：

```csharp

//With a single baseline
compBitSize[0*compBitSizeStride] = default(Unity.NetCode.Generated.GhostOwnerGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,
    baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);

//With three baseline
compBitSize[0*compBitSizeStride] = default(Unity.NetCode.Generated.GhostOwnerGhostComponentSerializer).SerializeComponentThreeBaseline(snapshotData,baseline0Ptr,
    baseline1Ptr, baseline2Ptr, changeMaskData, ref changeMaskOffset, ref snapshotOffset, ref predictor, ref writer, compressionModel);
```




