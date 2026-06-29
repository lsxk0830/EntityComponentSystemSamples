# DOTS 新手开发指南

本文面向刚开始使用 DOTS / Entities 的开发者，按常见开发动作组织。示例以本仓库使用的 Unity 6.2 和 Entities 1.4 系列包为基准，优先采用 `ISystem`、`SystemAPI`、`LocalTransform`、`EntityCommandBuffer` 等当前常用写法。

> 相关示例可参考 [HelloCube](./Dots101/Entities101/Assets/HelloCube/) 和 [Entities 示例文档](./EntitiesSamples/Docs/)。

## 1. 基础模型

DOTS 的核心思路是把数据和逻辑拆开：

| 概念 | 说明 |
| --- | --- |
| `Entity` | 实体，只是一个 ID，不直接保存逻辑 |
| `IComponentData` | 组件，保存实体的数据 |
| `ISystem` | 系统，查询组件并修改数据 |
| `World` | 一组实体和系统的运行环境 |
| `Archetype` | 拥有相同组件组合的一类实体 |
| `Chunk` | 同一 Archetype 下连续存储实体数据的内存块 |

最小组件示例：

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct MoveSpeed : IComponentData
{
    public float Value;
}

public struct MoveDirection : IComponentData
{
    public float3 Value;
}
```

没有字段的组件通常称为标签组件，用来表达状态或身份：

```csharp
using Unity.Entities;

public struct PlayerTag : IComponentData
{
}
```

## 2. Authoring 与 Baker

运行时 Entity 的数据通常来自场景中的 GameObject。Authoring 组件负责让设计师在 Inspector 中填值，Baker 负责把这些值烘焙成 ECS 组件。

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MoverAuthoring : MonoBehaviour
{
    public float Speed = 3f;
    public Vector3 Direction = Vector3.forward;

    class Baker : Baker<MoverAuthoring>
    {
        public override void Bake(MoverAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new MoveSpeed
            {
                Value = authoring.Speed
            });

            AddComponent(entity, new MoveDirection
            {
                Value = math.normalizesafe(authoring.Direction)
            });
        }
    }
}
```

常用 `TransformUsageFlags`：

| 标记 | 常见用途 |
| --- | --- |
| `None` | 只需要数据，不需要变换 |
| `Dynamic` | 运行时会移动、旋转或缩放 |
| `Renderable` | 需要渲染相关变换 |
| `WorldSpace` | 以世界空间方式处理变换 |
| `ManualOverride` | 自己完全接管变换组件 |

## 3. System 基础写法

`ISystem` 是值类型系统，适合 Burst 编译。常见生命周期是 `OnCreate` 初始化、`OnUpdate` 每帧更新。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // 没有 MoveSpeed 时跳过更新
        state.RequireForUpdate<MoveSpeed>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, speed, direction) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>, RefRO<MoveDirection>>())
        {
            transform.ValueRW.Position += direction.ValueRO.Value * speed.ValueRO.Value * deltaTime;
        }
    }
}
```

查询参数习惯：

| 写法 | 含义 |
| --- | --- |
| `RefRO<T>` | 只读访问组件 |
| `RefRW<T>` | 读写访问组件 |
| `EnabledRefRO<T>` | 只读访问可启用组件状态 |
| `EnabledRefRW<T>` | 读写访问可启用组件状态 |
| `Entity` | 在查询中取到实体 ID |

## 4. 获取时间

在 System 中使用 `SystemAPI.Time` 获取时间：

```csharp
using Unity.Burst;
using Unity.Entities;

public partial struct TimeExampleSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (var timer in SystemAPI.Query<RefRW<LifeTimer>>())
        {
            timer.ValueRW.RemainSeconds -= deltaTime;
            timer.ValueRW.LastUpdateTime = elapsedTime;
        }
    }
}

public struct LifeTimer : IComponentData
{
    public float RemainSeconds;
    public double LastUpdateTime;
}
```

常见选择：

| 需求 | 推荐 |
| --- | --- |
| 每秒移动多少距离 | `DeltaTime` |
| 正弦波、循环动画、计时器显示 | `ElapsedTime` |
| 类似 `FixedUpdate` 的固定步长逻辑 | 放入固定步长系统组或参考 [FixedTimestep 示例](./Dots101/Entities101/Assets/HelloCube/11.%20FixedTimestep/) |

## 5. 控制位移、旋转、缩放

Entities 1.4 中常用 `LocalTransform` 表示位置、旋转和等比缩放。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct TransformControlSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
        {
            transform.ValueRW.Position += new float3(0, 0, 1) * speed.ValueRO.Value * deltaTime;
            transform.ValueRW = transform.ValueRO.RotateY(math.radians(90f) * deltaTime);
            transform.ValueRW.Scale = 1f + math.sin((float)SystemAPI.Time.ElapsedTime) * 0.25f;
        }
    }
}
```

也可以直接用工厂方法设置初始变换：

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ResetTransformSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<ResetTransformTag>())
        {
            transform.ValueRW = LocalTransform.FromPositionRotationScale(
                new float3(0, 1, 0),
                quaternion.identity,
                1f);
        }
    }
}

public struct ResetTransformTag : IComponentData
{
}
```

非等比缩放使用 `PostTransformMatrix`：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct NonUniformScaleSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach (var postTransform in SystemAPI.Query<RefRW<PostTransformMatrix>>())
        {
            postTransform.ValueRW.Value = float4x4.Scale(1f, 1f + math.sin(elapsedTime) * 0.5f, 1f);
        }
    }
}
```

不要把 `LocalToWorld` 当作主要写入目标。它通常由 Transform 系统根据 `LocalTransform`、`Parent`、`PostTransformMatrix` 等数据计算。

## 6. 动态创建和销毁 Entity

少量主线程结构性变化可以直接使用 `EntityManager`。大量或并行 Job 中的结构性变化优先使用 `EntityCommandBuffer`。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct Spawner : IComponentData
{
    public Entity Prefab;
    public int Count;
}

public partial struct SpawnOnceSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Spawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var spawnerEntity = SystemAPI.GetSingletonEntity<Spawner>();
        var spawner = SystemAPI.GetSingleton<Spawner>();

        for (int i = 0; i < spawner.Count; i++)
        {
            Entity instance = state.EntityManager.Instantiate(spawner.Prefab);
            float3 position = new float3(i * 2f, 0f, 0f);
            state.EntityManager.SetComponentData(instance, LocalTransform.FromPosition(position));
        }

        state.EntityManager.DestroyEntity(spawnerEntity);
    }
}
```

销毁满足条件的实体：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public partial struct DestroyBelowGroundSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (transform.ValueRO.Position.y < 0f)
            {
                ecb.DestroyEntity(entity);
            }
        }
    }
}
```

## 7. 动态添加和删除组件

组件组合变化属于结构性变化，会让实体移动到新的 Archetype。主线程可以直接用 `EntityManager`：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public struct Selected : IComponentData
{
}

public partial struct MainThreadSelectSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>().WithNone<Selected>().WithEntityAccess())
        {
            if (transform.ValueRO.Position.x > 0f)
            {
                state.EntityManager.AddComponent<Selected>(entity);
            }
        }

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Selected>().WithEntityAccess())
        {
            if (transform.ValueRO.Position.x <= 0f)
            {
                state.EntityManager.RemoveComponent<Selected>(entity);
            }
        }
    }
}
```

Job 中要用 `EntityCommandBuffer.ParallelWriter` 记录命令：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public partial struct JobSelectSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

        new AddSelectedJob
        {
            ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel();

        new RemoveSelectedJob
        {
            ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel();
    }
}

[WithNone(typeof(Selected))]
[BurstCompile]
public partial struct AddSelectedJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in LocalTransform transform)
    {
        if (transform.Position.x > 0f)
        {
            ECB.AddComponent<Selected>(chunkIndex, entity);
        }
    }
}

[WithAll(typeof(Selected))]
[BurstCompile]
public partial struct RemoveSelectedJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in LocalTransform transform)
    {
        if (transform.Position.x <= 0f)
        {
            ECB.RemoveComponent<Selected>(chunkIndex, entity);
        }
    }
}
```

## 8. 查询和访问组件

`SystemAPI.Query` 适合遍历一组实体，`SystemAPI.GetComponent` 和 `SystemAPI.SetComponent` 适合按实体 ID 访问单个实体。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public struct FollowTarget : IComponentData
{
    public Entity Target;
    public float FollowStrength;
}

public partial struct FollowTargetSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, follow) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<FollowTarget>>())
        {
            if (!SystemAPI.Exists(follow.ValueRO.Target))
            {
                continue;
            }

            if (!SystemAPI.HasComponent<LocalTransform>(follow.ValueRO.Target))
            {
                continue;
            }

            var targetTransform = SystemAPI.GetComponent<LocalTransform>(follow.ValueRO.Target);
            transform.ValueRW.Position = targetTransform.Position;
        }
    }
}
```

常用访问方法：

| API | 用途 |
| --- | --- |
| `SystemAPI.Exists(entity)` | 判断实体是否仍然有效 |
| `SystemAPI.HasComponent<T>(entity)` | 判断实体是否有某组件 |
| `SystemAPI.GetComponent<T>(entity)` | 读取单个实体组件 |
| `SystemAPI.SetComponent(entity, value)` | 写入单个实体组件 |
| `SystemAPI.GetComponentRW<T>(entity)` | 获取读写引用 |
| `SystemAPI.GetComponentLookup<T>()` | Job 或跨实体访问时批量准备查找表 |

## 9. 单例配置组件

只有一个实体拥有某组件时，可以把它当作单例配置读取。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public struct GameConfig : IComponentData
{
    public float GlobalMoveSpeed;
}

public partial struct ConfigDrivenMoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<GameConfig>();
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<PlayerTag>())
        {
            transform.ValueRW.Position.z += config.GlobalMoveSpeed * deltaTime;
        }
    }
}
```

需要修改单例时使用 `GetSingletonRW`：

```csharp
using Unity.Entities;

public partial struct ScoreSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var score = SystemAPI.GetSingletonRW<Score>();
        score.ValueRW.Value += 1;
    }
}

public struct Score : IComponentData
{
    public int Value;
}
```

## 10. DynamicBuffer 入门

`DynamicBuffer` 是可变长度数组组件，适合路径点、背包、技能列表、事件队列等数据。

定义 Buffer 元素：

```csharp
using Unity.Entities;
using Unity.Mathematics;

public struct Waypoint : IBufferElementData
{
    public float3 Value;
}
```

在 Baker 中添加初始数据：

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PathAuthoring : MonoBehaviour
{
    public Transform[] Points;

    class Baker : Baker<PathAuthoring>
    {
        public override void Bake(PathAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var buffer = AddBuffer<Waypoint>(entity);

            foreach (var point in authoring.Points)
            {
                buffer.Add(new Waypoint
                {
                    Value = (float3)point.position
                });
            }
        }
    }
}
```

运行时读取和追加：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

public partial struct WaypointSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var buffer in SystemAPI.Query<DynamicBuffer<Waypoint>>())
        {
            if (buffer.Length == 0)
            {
                buffer.Add(new Waypoint
                {
                    Value = new float3(0, 0, 0)
                });
            }

            float3 firstPoint = buffer[0].Value;
            buffer[0] = new Waypoint
            {
                Value = firstPoint + new float3(0, 1, 0)
            };
        }
    }
}
```

## 11. Enableable Component

如果只是切换状态，不一定要添加或删除组件。实现 `IEnableableComponent` 后，可以启用或禁用组件，通常比结构性变化更适合高频状态切换。

```csharp
using Unity.Entities;

public struct Frozen : IComponentData, IEnableableComponent
{
}
```

切换启用状态：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public partial struct FreezeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, frozen) in
                 SystemAPI.Query<RefRO<LocalTransform>, EnabledRefRW<Frozen>>())
        {
            frozen.ValueRW = transform.ValueRO.Position.y < 0f;
        }
    }
}
```

只处理未被冻结的实体：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

public partial struct MoveWhenNotFrozenSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>().WithDisabled<Frozen>())
        {
            transform.ValueRW.Position.z += deltaTime;
        }
    }
}
```

也可以通过 ECB 在 Job 中设置启用状态：

```csharp
ECB.SetComponentEnabled<Frozen>(chunkIndex, entity, true);
```

## 12. Prefab 生成

Prefab 生成的常见模式是：Baker 保存 prefab 对应的 Entity，运行时 `Instantiate`。

```csharp
using Unity.Entities;
using UnityEngine;

public class PrefabSpawnerAuthoring : MonoBehaviour
{
    public GameObject Prefab;
    public int Count = 10;

    class Baker : Baker<PrefabSpawnerAuthoring>
    {
        public override void Bake(PrefabSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PrefabSpawner
            {
                Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                Count = authoring.Count
            });
        }
    }
}

public struct PrefabSpawner : IComponentData
{
    public Entity Prefab;
    public int Count;
}
```

运行时批量生成：

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct PrefabSpawnSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PrefabSpawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var spawnerEntity = SystemAPI.GetSingletonEntity<PrefabSpawner>();
        var spawner = SystemAPI.GetSingleton<PrefabSpawner>();

        for (int i = 0; i < spawner.Count; i++)
        {
            Entity instance = state.EntityManager.Instantiate(spawner.Prefab);
            float3 position = new float3(i * 1.5f, 0f, 0f);
            SystemAPI.SetComponent(instance, LocalTransform.FromPosition(position));
        }

        state.EntityManager.DestroyEntity(spawnerEntity);
    }
}
```

## 13. 常见坑

| 问题 | 建议 |
| --- | --- |
| 在 Job 中直接 `AddComponent` 或 `DestroyEntity` | 使用 `EntityCommandBuffer` 记录，稍后播放 |
| 每帧大量添加删除组件 | 优先考虑 `IEnableableComponent` 或组件值表达状态 |
| 直接写 `LocalToWorld` | 通常写 `LocalTransform`，让 Transform 系统计算 |
| 忘记 `RequireForUpdate` | 系统可能在数据未准备好时运行 |
| 查询中滥用读写引用 | 能用 `RefRO` 就不要用 `RefRW` |
| 持有结构性变化前取到的 `DynamicBuffer` | 结构性变化后重新获取 Buffer |
| 主线程中大量结构性变化分散执行 | 合并到 ECB，减少同步点 |
| 单例组件实际有多个实体 | 保证配置实体唯一，或改用普通 Query |

## 14. 推荐阅读顺序

1. [Unity Entities 101](./UnityEntities101.md)
2. [HelloCube 示例](./Dots101/Entities101/Assets/HelloCube/)
3. [Transforms 文档](./EntitiesSamples/Docs/transforms.md)
4. [Systems 文档](./EntitiesSamples/Docs/systems.md)
5. [Entity Command Buffers 文档](./EntitiesSamples/Docs/entity-command-buffers.md)
6. [Baking 文档](./EntitiesSamples/Docs/baking.md)

