# Entity 命令缓冲区

我们可以通过将命令记录到 [`EntityCommandBuffer`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.html) 来推迟对 entities 的更改。录制的命令稍后在主线程调用 [`Playback()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.Playback.html) 时执行。

使用 `EntityCommandBuffer` 推迟更改在 jobs 中特别有用，因为 jobs 通常不应直接进行[结构更改](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html) (*i.e.* create entities, destroy entities，添加 components，或删除 components）。相反，jobs 应该在 job 完成后记录要在主线程上播放的命令。`EntityCommandBuffer` 还可以通过将结构更改推迟到框架的几个合并点而不是分散在整个框架中来帮助我们避免不必要的[同步点](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/concepts-structural-changes.html#sync-points)。

`EntityCommandBuffer` 有许多（但不是全部）与 `EntityManager` 相同的方法。这些方法包括：

|**`EntityCommandBuffer` 方法**|**描述**|
|---|---|
| [`CreateEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.CreateEntity.html) | 记录创建新 entity 的命令。返回[临时](#temporary-entities) entity ID。 |
| [`DestroyEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.DestroyEntity.html) | 记录销毁 entity 的命令。 |
| [`AddComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.AddComponent.html) | 记录将 T 类型的 component 添加到 entity 的命令。 |
| [`RemoveComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.RemoveComponent.html) | 记录从 entity 移动 T 类型的 component 的命令。 |
| [`SetComponent<T>()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.SetComponent.html) | 记录设置 T 类型的 component 值的命令。 |
| [`AppendToBuffer()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.AppendToBuffer.html) | 记录一个命令，该命令会将单个值附加到 entity 现有缓冲区的末尾。 |
| [`AddBuffer()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.AddBuffer.html) | 返回一个 `DynamicBuffer`，它存储在录制的命令中，并且在播放时创建 entity 时，该缓冲区的内容将被复制到 entity 的实际缓冲区中。实际上，写入返回的缓冲区允许您设置 component 的初始内容。 |
| [`SetBuffer()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.SetBuffer.html)  | 与 `AddBuffer()` 类似，但它假设 entity 已经具有 component 类型的缓冲区。在播放时，entity 已存在的缓冲区内容将被返回的缓冲区内容覆盖。 |

| &#x1F4DD; NOTE |
| :- |
| 某些 `EntityManager` 方法没有 `EntityCommandBuffer` 等效方法，因为等效方法不可行或没有意义。例如，没有用于获取 component 值的 `EntityCommandBuffer` 方法，因为*读取*数据不是可以有效延迟的事情。 |
| 播放完毕后，`EntityCommandBuffer` 实例不能用于额外录制。如果您需要记录更多命令，请创建一个新的、单独的 `EntityCommandBuffer` 实例。 |

<br>

## Job 安全

每个 `EntityCommandBuffer` 都有一个 job 安全句柄，因此如果您执行以下操作，安全检查将引发异常：

- ...在主线程上调用 `EntityCommandBuffer` 的方法，同时 `EntityCommandBuffer` 仍在由任何当前调度的 jobs 使用。
- ...或 schedule 一个 job，它访问已被其他当前安排的 jobs 使用的 `EntityCommandBuffer` （*除非*新的 job 取决于其他 jobs）。

| ⚠ IMPORTANT |
| :- |
| 您可能想在多个 jobs 之间共享单个 `EntityCommandBuffer` 实例，但强烈建议不要这样做。在某些情况下它可以正常工作，但在许多情况下却不能。例如，在多个并行 jobs 中使用相同的 `EntityCommandBuffer.ParallelWriter` 可能会导致命令的意外播放顺序。相反，**实际上最好为每个 job 创建和使用一个 `EntityCommandBuffer`**。不必担心性能差异：记录和回放分散在多个 `EntityCommandBuffer` 中的一组命令实际上并不比将同一组命令全部记录到一个 `EntityCommandBuffer` 中更昂贵。 |

<br>

## 临时 entities

当您调用 `EntityCommandBuffer` 的 [`CreateEntity()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.CreateEntity.html) 或 [`Instantiate()`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.Instantiate.html) 方法时，在播放中执行该命令之前不会创建新的 entity，因此这些方法返回的 entity ID 是“临时”ID，其索引号为负。相同 `EntityCommandBuffer` 的后续 `AddComponent`、`SetComponent` 和 `SetBuffer` 命令可以使用这些临时 ID。在播放时，录制命令中的任何临时 ID 将被重新映射到实际的现有 entities。

| ⚠ IMPORTANT |
| :- |
| 由于临时 entity ID 在创建它的 `EntityCommandBuffer` 实例之外没有任何意义，因此它应该“仅”在同一 `EntityCommandBuffer` 实例的后续方法调用中使用。例如，请勿使用临时 ID 来记录不同 `EntityCommandBuffer` 实例的命令。 |

<br>

## EntityCommandBuffer.ParallelWriter

为了安全地记录来自并行 job 的命令，我们需要一个 [`EntityCommandBuffer.ParallelWriter`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBuffer.ParallelWriter.html)，它是底层 `EntityCommandBuffer` 的包装器。

`ParallelWriter` 具有与 `EntityCommandBuffer` 本身相同的大部分方法，但为了确定性，`ParallelWriter` 方法都采用额外的“排序键”参数：

当 `EntityCommandBuffer.ParallelWriter` 在并行 job 中记录命令时，从不同线程记录的命令的顺序取决于线程调度，使得顺序不确定。这并不理想，因为：

- 确定性代码通常更容易调试。
- 一些Netcode解决方案依赖于确定性来在不同的机器上产生一致的结果。

虽然命令的记录顺序无法确定，但*播放顺序*可以通过一个简单的技巧来确定：

1. 每个命令都会记录一个“排序键”整数，作为每个命令方法的第一个参数传递。
1. `Playback()` 方法在执行命令之前按排序键对命令进行排序。

只要使用的排序键确定地映射到每个记录的命令，排序就会使播放顺序确定。

因此，在 `IJobEntity` 中，我们通常要使用的排序键是 [`ChunkIndexInQuery`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.ChunkIndexInQuery.html)，它是每个 chunk 的唯一值。由于排序是稳定的，并且单个 chunk 的所有 entities 都在单个线程中一起处理，因此该索引值适合作为记录命令的排序键。在 `IJobChunk` 中，我们可以使用 `Execute` 方法的等效 `unfilteredChunkIndex` 参数。

<br>

## 多重播放

如果使用 `PlaybackPolicy.MultiPlayback` 选项创建 `EntityCommandBuffer`，则可以多次调用它的 `Playback` 方法。否则，多次调用 `Playback` 将引发异常。当您想要重复生成一组 entities 时，多重播放主要有用。


<br>

## EntityCommandBufferSystem

[`EntityCommandBufferSystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Entities.EntityCommandBufferSystem.html) 是一个 system，它提供了一种推迟 `EntityCommandBuffer` 播放的便捷方法。从 `EntityCommandBufferSystem` 创建的 `EntityCommandBuffer` 实例将在下次 `EntityCommandBufferSystem` 更新时播放并处置。

您很少需要自己创建任何 `EntityCommandBufferSystem`，因为自动引导过程会将这五个放入默认的 world 中：

- `BeginInitializationEntityCommandBufferSystem`
- `EndInitializationEntityCommandBufferSystem`
- `BeginSimulationEntityCommandBufferSystem`
- `EndSimulationEntityCommandBufferSystem`
- `BeginPresentationEntityCommandBufferSystem`

例如，`EndSimulationEntityCommandBufferSystem` 在 `SimulationSystemGroup` 末尾更新。（请注意，帧末尾没有 *EndPresentationEntityCommandBufferSystem*，但您可以使用 `BeginInitializationEntityCommandBufferSystem` 代替：一帧的结束和下一帧的开始在逻辑上是相同的时间点）。

| ⚠ IMPORTANT |
| :- |
| 不要手动回放和处置由 `EntityCommandBufferSystem` 创建的 `EntityCommandBuffer` 实例：`EntityCommandBufferSystem` 将为您回放和处置该实例。 |