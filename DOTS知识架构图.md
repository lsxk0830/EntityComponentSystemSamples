# DOTS 知识架构图

这张图把本仓库中的 DOTS 学习内容整理成从基础并行、ECS 数据建模，到 Physics、Netcode 和 Graphics 示例的知识路径。核心 DOTS / Unity 术语保留英文，方便对照官方 Package 手册、示例代码和 API。

```mermaid
flowchart TD
    DOTS["DOTS 示例知识体系"]

    DOTS --> Foundation["基础能力"]
    DOTS --> ECS["Entities / ECS"]
    DOTS --> Physics["Unity Physics"]
    DOTS --> Netcode["Netcode for Entities"]
    DOTS --> Graphics["Entities Graphics"]
    DOTS --> Samples["Samples / 附加资料"]
```

## 基础能力

```mermaid
flowchart TD
    Foundation["基础能力"]

    Foundation --> JobSystem["Job System"]
    Foundation --> Burst["Burst 编译与数据并行思维"]
    JobSystem --> JobDeps["Job 依赖与 Complete"]
    JobSystem --> ParallelJobs["Parallel Jobs"]
    JobSystem --> NativeCollections["Native Collections 与 Allocator"]
```

## Entities / ECS

```mermaid
flowchart TD
    ECS["Entities / ECS"]

    ECS --> EntityModel["Entity / Component / System"]
    ECS --> WorldModel["World / EntityManager"]
    ECS --> DataLayout["Archetype / Chunk 数据布局"]
    ECS --> Query["EntityQuery 与 SystemAPI"]
    ECS --> ECB["EntityCommandBuffer"]
    ECS --> Transform["Transform components 和 systems"]
    ECS --> Baking["Baker / SubScene / entity scenes"]
    ECS --> AdvancedECS["Aspects / Blob assets / Enableable components"]
```

## Unity Physics

```mermaid
flowchart TD
    Physics["Unity Physics"]

    Physics --> Bodies["静态 / 动态 / Kinematic bodies"]
    Physics --> Colliders["Colliders 与 Compound colliders"]
    Physics --> Queries["Collision queries"]
    Physics --> Events["Collision / Trigger events"]
    Physics --> Filtering["Collision filtering"]
    Physics --> Joints["Joints 和 Motors"]
    Physics --> PhysicsSystems["Physics systems 与 Simulation"]
```

## Netcode for Entities

```mermaid
flowchart TD
    Netcode["Netcode for Entities"]

    Netcode --> Worlds["Client / Server worlds"]
    Netcode --> Commands["玩家输入 Command stream"]
    Netcode --> Ghosts["Ghost entities 与 snapshots"]
    Netcode --> Prediction["Prediction"]
    Netcode --> Interpolation["Interpolation / Extrapolation"]
    Netcode --> RPC["RPC"]
    Netcode --> Bootstrap["Bootstrap 与连接流程"]
```

## Entities Graphics

```mermaid
flowchart TD
    Graphics["Entities Graphics"]

    Graphics --> Rendering["Entities Graphics 渲染路径"]
    Graphics --> HDRP["HDRP Samples"]
    Graphics --> URP["URP Samples"]
```

## Samples 与附加资料

```mermaid
flowchart TD
    Samples["Samples / 附加资料"]

    Samples --> Dots101["Dots101 入门示例"]
    Samples --> EntitiesSamples["EntitiesSamples"]
    Samples --> PhysicsSamples["PhysicsSamples"]
    Samples --> NetcodeSamples["NetcodeSamples"]
    Samples --> PackageManuals["Package 手册与附加材料"]
```

## 跨模块关系

- Job System 支撑 Entities / ECS 的并行执行。
- Entities / ECS 是 Physics、Netcode 和 Graphics 示例的数据基础。
- Unity Physics 的模拟状态可以参与 Netcode 同步。
- Entities Graphics 使用 ECS 中的批量渲染数据。

## 建议阅读路径

1. 先读 [UnityJobSystem101.md](UnityJobSystem101.md)，理解 `Job`、依赖、并行执行和 `Native Collections`。
2. 再读 [UnityEntities101.md](UnityEntities101.md)，建立 `Entity`、`Component`、`System`、`World`、`Archetype`、`Chunk` 和 `Baker` 的整体模型。
3. 需要物理模拟时读 [UnityPhysics101.md](UnityPhysics101.md)，重点关注 bodies、colliders、collision queries、events、filtering、joints 和 Physics systems。
4. 需要多人联网时读 [UnityNetcodeforEntities101.md](UnityNetcodeforEntities101.md)，重点关注 `Ghost`、`Prediction`、`Interpolation`、`RPC`、client/server worlds 和连接流程。
5. 回到 [README.md](README.md) 中的 samples 索引，按目标功能进入 `EntitiesSamples`、`PhysicsSamples`、`NetcodeSamples` 或 `GraphicsSamples` 查看完整示例。

## 图例说明

- 实线表示主要学习分支。
- 虚线表示模块之间的依赖或常见组合关系。
- 图中的英文术语是 DOTS / Unity 的关键概念，建议在阅读代码和 Package 手册时保持英文对照。
