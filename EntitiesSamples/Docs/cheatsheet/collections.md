# Unity.Collections 备忘单

本 package 提供的集合分为三类：

- `Unity.Collections` 中名称以 `Native-` 开头的集合类型具有安全检查，以确保它们被正确处置并以线程安全的方式使用。
- `Unity.Collections.LowLevel.Unsafe` 中名称以 `Unsafe-` 开头的集合类型没有这些安全检查。
- 其余的集合类型未分配且不包含指针，因此实际上它们的处置和线程安全从来都不是问题。这些类型仅保存少量数据。

## 分配器

- `Allocator.Temp`：最快的分配器。对于非常短暂的分配。临时分配*不能*传递到 jobs 中。
- `Allocator.TempJob`：下一个最快的分配器。对于短期分配（4 帧生命周期）。TempJob 分配可以传递到 jobs 中。
- `Allocator.Persistent`：最慢的分配器。对于无限期的生命周期分配。持久分配可以传递到 jobs 中。

## 类似数组的类型

[核心模块](https://docs.unity3d.com/ScriptReference/UnityEngine.CoreModule) 提供了一些关键的类似数组的类型，包括[`Unity.Collections.NativeArray<T>`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1) 和[`Unity.Collections.NativeSlice<T>`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeSlice_1)。这个 package 本身提供：

|||
----------------------------------------------------- | -----------
[NativeList](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeList-1.html) |可调整大小的列表。
[UnsafeList](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeList-1.html) |可调整大小的列表。
[UnsafePtrList](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafePtrList-1.html) |可调整大小的指针列表。
[NativeStream](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeStream.html) |一组仅附加、无类型的缓冲区。
[UnsafeStream](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeStream.html) |一组仅附加、无类型的缓冲区。
[UnsafeAppendBuffer](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAppendBuffer.html) |仅附加的无类型缓冲区。
[NativeQueue](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeQueue-1.html) |可调整大小的队列。
[UnsafeRingQueue](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeRingQueue-1.html) |固定大小的循环缓冲区。
[FixedList32Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList32Bytes-1.html) |一个 32 字节的列表，包括 2 字节的开销，因此有 30 字节可用于存储。最大容量取决于类型参数。
[FixedList64Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList64Bytes-1.html) |一个 64 字节的列表，包括 2 字节的开销，因此 62 字节可用于存储。最大容量取决于类型参数。
[FixedList128Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList128Bytes-1.html) |一个 128 字节的列表，包括 2 字节的开销，因此有 126 字节可用于存储。最大容量取决于类型参数。
[FixedList512Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList512Bytes-1.html) |一个 512 字节的列表，包括 2 字节的开销，因此有 510 字节可用于存储。最大容量取决于类型参数。
[FixedList4096Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedList4096Bytes-1.html) |一个 4096 字节的列表，包括 2 字节的开销，因此有 4094 字节可用于存储。最大容量取决于类型参数。

没有多维数组类型，但您可以简单地将多维数据打包到一维中：例如，对于 `int[4][5]` 数组，请使用 `int[20]` 数组（因为 `4 * 5` 是 `20`）。

使用 Entities package 时，[DynamicBuffer](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Entities.DynamicBuffer-1.html) component 通常是类似数组或列表的集合的最佳选择。

另请参见 [NativeArrayExtensions](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeArrayExtensions.html)、[ListExtensions](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.ListExtensions.html)、[NativeSortExtension](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeSortExtension.html)。

## 地图和集合类型

|||
---------------------------------------------------------------| -----------
[NativeParallelHashMap](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeParallelHashMap-2.html) |键值对的无序关联数组。
[UnsafeParallelHashMap](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMap-2.html) |键值对的无序关联数组。
[NativeParallelHashSet](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeParallelHashSet-1.html) |一组独特的价值观。
[UnsafeParallelHashSet](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMap-2.html) |一组独特的价值观。
[NativeMultiHashMap](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeMultiHashMap-2.html) |键值对的无序关联数组。键不必是唯一的，*i.e.* 两对可以具有相同的键。
[UnsafeMultiHashMap](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeMultiHashMap-2.html) |键值对的无序关联数组。键不必是唯一的，*i.e.* 两对可以具有相同的键。

另请参见 [HashSetExtensions](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.HashSetExtensions.html)、[Unity.Collections.NotBurstCompatible.Extensions](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NotBurstCompatible.html) 和 [Unity.Collections.LowLevel.Unsafe.NotBurstCompatible.Extensions](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.NotBurstCompatible.Extensions.html)

## 位数组和位域

|||
------------------------------------------------- | -----------
[BitField32](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.BitField32.html) | 32 位的固定大小数组。
[BitField64](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.BitField64.html) | 64 位的固定大小数组。
[NativeBitArray](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeBitArray.html) |任意大小的位数组。
[UnsafeBitArray](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeBitArray.html) |任意大小的位数组。

## 字符串类型

|||
------------------------------------- | -----------
[NativeText](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeText.html) | UTF-8 编码的字符串。可变且可调整大小。
[FixedString32Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString32Bytes.html) |一个 32 字节的 UTF-8 编码字符串，包括 3 字节的开销，因此有 29 字节可用于存储。
[FixedString64Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString64Bytes.html) | 64 字节的 UTF-8 编码的字符串，包括 3 字节的开销，因此有 61 字节可用于存储。
[FixedString128Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString128Bytes.html) |一个 128 字节的 UTF-8 编码字符串，包括 3 字节的开销，因此有 125 字节可用于存储。
[FixedString512Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString512Bytes.html) |一个 512 字节的 UTF-8 编码字符串，包括 3 字节的开销，因此有 509 字节可用于存储。
[FixedString4096Bytes](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString4096Bytes.html) |一个 4096 字节的 UTF-8 编码字符串，包括 3 字节的开销，因此有 4093 字节可用于存储。

另请参见 [FixedString](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedString.html) 和 [FixedStringMethods](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.FixedStringMethods.html)。


## 其他类型

|||
-------------------------------------------------------- | -----------
[NativeReference](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.NativeReference-1.html) |对单个值的引用。功能上相当于长度为 1 的数组。
[UnsafeAtomicCounter32](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter32.html) | 32 位原子计数器。
[UnsafeAtomicCounter64](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/api/Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter64.html) | 64 位原子计数器。


## 枚举器

大多数集合都有一个 `GetEnumerator` 方法，该方法返回 `IEnumerator<T>` 的实现。枚举器的 `MoveNext` 方法将其 `Current` 属性前进到下一个元素。

## 并行的读者和作者

一些集合类型具有用于从并行 jobs 读取和写入的嵌套类型。例如，要从并行 job 安全写入 `NativeList<T>`，您需要 `NativeList<T>.ParallelWriter`。
