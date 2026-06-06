using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

// Mike 的 GDC 演讲“使用 Component Systems 的面向数据的方法”
// 是剖析 Boids 示例代码的一个很好的参考：
// https://youtu.be/p65Yt20pw0g?t=1446
// 它解释了此示例的稍微旧的实现，但几乎所有的
// 信息仍然相关。

// The targets (2 red fish) and obstacle (1 shark) move based on the ActorAnimation tab
// 在 Unity UI 中，以便它们根据关键帧动画移动。

namespace Boids
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BoidSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var boidQuery = SystemAPI.QueryBuilder().WithAll<Boid>().WithAllRW<LocalToWorld>().Build();
            var targetQuery = SystemAPI.QueryBuilder().WithAll<BoidTarget, LocalToWorld>().Build();
            var obstacleQuery = SystemAPI.QueryBuilder().WithAll<BoidObstacle, LocalToWorld>().Build();

            var obstacleCount = obstacleQuery.CalculateEntityCount();
            var targetCount = targetQuery.CalculateEntityCount();

            var world = state.WorldUnmanaged;
            state.EntityManager.GetAllUniqueSharedComponents(out NativeList<Boid> uniqueBoidTypes, world.UpdateAllocator.ToAllocator);
            float dt = math.min(0.05f, SystemAPI.Time.DeltaTime);

            // Boid 的每个变体代表 SharedComponentData 的不同值，并且是独立的，
            // 这意味着同一变体的 Boids 只能相互交互。因此，这个循环处理每个
            // 单独的变体类型。
            foreach (var boidSettings in uniqueBoidTypes)
            {
                boidQuery.AddSharedComponentFilter(boidSettings);

                var boidCount = boidQuery.CalculateEntityCount();
                if (boidCount == 0)
                {
                    // 早点出去。如果给定的变体不包含 Boids，则继续下一个循环。
                    // 例如，变体 0 总是会提前退出，它代表默认的、未初始化的
                    // Boid 结构体，该结构体未出现在本示例中。
                    boidQuery.ResetFilter();
                    continue;
                }

                // 下面计算相邻 Boid 的空间单元
                // note: 使用稀疏网格而不是密集有界网格，所以有
                // 没有预定义的空间边界。

                var hashMap                   = new NativeParallelMultiHashMap<int, int>(boidCount, world.UpdateAllocator.ToAllocator);
                var cellIndices               = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellObstaclePositionIndex = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellTargetPositionIndex   = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellCount                 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellObstacleDistance      = CollectionHelper.CreateNativeArray<float, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellAlignment             = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellSeparation            = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref world.UpdateAllocator);

                var copyTargetPositions       = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(targetCount, ref world.UpdateAllocator);
                var copyObstaclePositions     = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(obstacleCount, ref world.UpdateAllocator);

                // 这些 jobs 提取相关位置，标题为 component
                // 到 NativeArrays，以便 `MergeCells` 和 `Steer` jobs 可以随机访问它们。
                // 这些 jobs 是使用 IJobEntity 语法定义的。
                var initialBoidJob = new InitialPerBoidJob
                {
                    CellAlignment = cellAlignment,
                    CellSeparation = cellSeparation,
                    ParallelHashMap = hashMap.AsParallelWriter(),
                    InverseBoidCellRadius = 1.0f / boidSettings.CellRadius,
                };
                var initialBoidJobHandle = initialBoidJob.ScheduleParallel(boidQuery, state.Dependency);

                var initialTargetJob = new InitialPerTargetJob
                {
                    TargetPositions = copyTargetPositions,
                };
                var initialTargetJobHandle = initialTargetJob.ScheduleParallel(targetQuery, state.Dependency);

                var initialObstacleJob = new InitialPerObstacleJob
                {
                    ObstaclePositions = copyObstaclePositions,
                };
                var initialObstacleJobHandle = initialObstacleJob.ScheduleParallel(obstacleQuery, state.Dependency);

                var initialCellCountJob = new MemsetNativeArray<int>
                {
                    Source = cellCount,
                    Value  = 1
                };
                var initialCellCountJobHandle = initialCellCountJob.Schedule(boidCount, 64, state.Dependency);

                var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialBoidJobHandle, initialCellCountJobHandle);
                var copyTargetObstacleBarrierJobHandle = JobHandle.CombineDependencies(initialTargetJobHandle, initialObstacleJobHandle);
                var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(initialCellBarrierJobHandle, copyTargetObstacleBarrierJobHandle);

                var mergeCellsJob = new MergeCells
                {
                    cellIndices               = cellIndices,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPositionIndex   = cellTargetPositionIndex,
                    cellCount                 = cellCount,
                    targetPositions           = copyTargetPositions,
                    obstaclePositions         = copyObstaclePositions
                };
                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap, 64, mergeCellsBarrierJobHandle);

                // 这里读取之前计算出的每个 cell 的所有 boid 的 boid 信息进行更新
                // 每个 boid 的 `localToWorld` 基于其新计算的航向，使用
                // 标准 boid 集群算法。
                var steerBoidJob = new SteerBoidJob
                {
                    CellIndices = cellIndices,
                    CellCount = cellCount,
                    CellAlignment = cellAlignment,
                    CellSeparation = cellSeparation,
                    CellObstacleDistance = cellObstacleDistance,
                    CellObstaclePositionIndex = cellObstaclePositionIndex,
                    CellTargetPositionIndex = cellTargetPositionIndex,
                    ObstaclePositions = copyObstaclePositions,
                    TargetPositions = copyTargetPositions,
                    CurrentBoidVariant = boidSettings,
                    DeltaTime = dt,
                    MoveDistance = boidSettings.MoveSpeed * dt,
                };
                var steerBoidJobHandle = steerBoidJob.ScheduleParallel(boidQuery, mergeCellsJobHandle);

                // 使用 dispose jobs 来处置分配的容器。
                state.Dependency = steerBoidJobHandle;

                // 我们传递 job 句柄并添加依赖项，以便我们在 jobs 之间保持正确的顺序
                // 随着循环迭代。出于我们执行的目的，此顺序不是必需的；然而，没有
                // 此处添加依赖项调用，安全性 system 会抛出错误，因为我们正在访问多个
                // boid 数据片段，它会认为可能存在竞争条件。

                boidQuery.AddDependency(state.Dependency);
                boidQuery.ResetFilter();
            }
            uniqueBoidTypes.Dispose();
        }

        // 在此示例中，共有 3 个独特的 boid 变体，每个变体对应一个唯一的值
        // Boid SharedComponent (note: this includes the default uninitialized value at
        // 索引 0，示例中实际未使用）。

        // 这会累积每个单元中所有 boids 的 `positions`（分离）和 `headings`（对齐）：
        // 1）统计每个单元格中 boid 的数量
        // 2）找到距离每个 boid cell 最近的障碍物和目标
        // 3) 跟踪哪个数组条目包含每个 boid 单元格的累积值
        // 在这种情况下，单元代表 cellRadius 内彼此靠近的 boids 的散列桶
        // 取整至最接近的 int3。
        // Note: `IJobNativeParallelMultiHashMapMergedSharedKeyIndices` 是一个自定义的 job，用于安全/高效地迭代
        // 本示例中使用了 NativeContainer (`NativeParallelMultiHashMap`)。目前，这些类型的更改或添加
        // 自定义 jobs 通常需要访问通过 `public` API 无法获得的数据/字段
        // 容器。这就是为什么自定义 job 类型 `IJobNativeParallelMultiHashMapMergedSharedKeyIndicies` 声明于
        // DOTS package（可以看到 `internal` 容器字段）而不是在 Boids 示例中。
        [BurstCompile]
        struct MergeCells : IJobNativeParallelMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<int>                 cellIndices;
            public NativeArray<float3>              cellAlignment;
            public NativeArray<float3>              cellSeparation;
            public NativeArray<int>                 cellObstaclePositionIndex;
            public NativeArray<float>               cellObstacleDistance;
            public NativeArray<int>                 cellTargetPositionIndex;
            public NativeArray<int>                 cellCount;
            [ReadOnly] public NativeArray<float3>   targetPositions;
            [ReadOnly] public NativeArray<float3>   obstaclePositions;

            void NearestPosition(NativeArray<float3> targets, float3 position, out int nearestPositionIndex, out float nearestDistance)
            {
                nearestPositionIndex = 0;
                nearestDistance      = math.lengthsq(position - targets[0]);
                for (int i = 1; i < targets.Length; i++)
                {
                    var targetPosition = targets[i];
                    var distance       = math.lengthsq(position - targetPosition);
                    var nearest        = distance < nearestDistance;

                    nearestDistance      = math.select(nearestDistance, distance, nearest);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);
            }

            // 解析最近障碍物和目标的距离并存储单元索引。
            public void ExecuteFirst(int index)
            {
                var position = cellSeparation[index] / cellCount[index];

                int obstaclePositionIndex;
                float obstacleDistance;
                NearestPosition(obstaclePositions, position, out obstaclePositionIndex, out obstacleDistance);
                cellObstaclePositionIndex[index] = obstaclePositionIndex;
                cellObstacleDistance[index]      = obstacleDistance;

                int targetPositionIndex;
                float targetDistance;
                NearestPosition(targetPositions, position, out targetPositionIndex, out targetDistance);
                cellTargetPositionIndex[index] = targetPositionIndex;

                cellIndices[index] = index;
            }

            // 对正在考虑的实际索引的对齐和分离求和并存储
            // 我们存储单元格的第一个值的索引。
            // note: 将这些项目相加，以便在 `Steer` 中可以解析它们的单元平均值。
            public void ExecuteNext(int cellIndex, int index)
            {
                cellCount[cellIndex]      += 1;
                cellAlignment[cellIndex]  += cellAlignment[cellIndex];
                cellSeparation[cellIndex] += cellSeparation[cellIndex];
                cellIndices[index]        = cellIndex;
            }
        }

        [BurstCompile]
        partial struct InitialPerBoidJob : IJobEntity
        {
            public NativeArray<float3> CellAlignment;
            public NativeArray<float3> CellSeparation;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter ParallelHashMap;
            public float InverseBoidCellRadius;
            void Execute([EntityIndexInQuery] int entityIndexInQuery, in LocalToWorld localToWorld)
            {
                CellAlignment[entityIndexInQuery] = localToWorld.Forward;
                CellSeparation[entityIndexInQuery] = localToWorld.Position;
                // 填充哈希图，其中每个桶包含位置量化的所有 Boid 的索引
                // 对于给定的小区半径为相同的值，以便可以通过以下方式随机访问信息
                // `MergeCells` 和 `Steer` jobs。
                // 这对于算法而言很有用，因为它限制了比较的次数，
                // 实际上发生在不同的主体之间。不是针对每个 boid，而是搜索所有 boid
                // 对于特定半径内的 boids，这通过哈希到桶的简化来限制那些。
                var hash = (int)math.hash(new int3(math.floor(localToWorld.Position * InverseBoidCellRadius)));
                ParallelHashMap.Add(hash, entityIndexInQuery);
            }
        }

        [BurstCompile]
        partial struct InitialPerTargetJob : IJobEntity
        {
            public NativeArray<float3> TargetPositions;
            void Execute([EntityIndexInQuery] int entityIndexInQuery, in LocalToWorld localToWorld)
            {
                TargetPositions[entityIndexInQuery] = localToWorld.Position;
            }
        }

        [BurstCompile]
        partial struct InitialPerObstacleJob : IJobEntity
        {
            public NativeArray<float3> ObstaclePositions;
            void Execute([EntityIndexInQuery] int entityIndexInQuery, in LocalToWorld localToWorld)
            {
                ObstaclePositions[entityIndexInQuery] = localToWorld.Position;
            }
        }

        [BurstCompile]
        partial struct SteerBoidJob : IJobEntity
        {
            [ReadOnly] public NativeArray<int> CellIndices;
            [ReadOnly] public NativeArray<int> CellCount;
            [ReadOnly] public NativeArray<float3> CellAlignment;
            [ReadOnly] public NativeArray<float3> CellSeparation;
            [ReadOnly] public NativeArray<float> CellObstacleDistance;
            [ReadOnly] public NativeArray<int> CellObstaclePositionIndex;
            [ReadOnly] public NativeArray<int> CellTargetPositionIndex;
            [ReadOnly] public NativeArray<float3> ObstaclePositions;
            [ReadOnly] public NativeArray<float3> TargetPositions;
            public Boid CurrentBoidVariant;
            public float DeltaTime;
            public float MoveDistance;
            void Execute([EntityIndexInQuery] int entityIndexInQuery, ref LocalToWorld localToWorld)
            {
                // 临时存储代码可读性的值
                var forward                           = localToWorld.Forward;
                var currentPosition                   = localToWorld.Position;
                var cellIndex                         = CellIndices[entityIndexInQuery];
                var neighborCount                     = CellCount[cellIndex];
                var alignment                         = CellAlignment[cellIndex];
                var separation                        = CellSeparation[cellIndex];
                var nearestObstacleDistance           = CellObstacleDistance[cellIndex];
                var nearestObstaclePositionIndex      = CellObstaclePositionIndex[cellIndex];
                var nearestTargetPositionIndex        = CellTargetPositionIndex[cellIndex];
                var nearestObstaclePosition           = ObstaclePositions[nearestObstaclePositionIndex];
                var nearestTargetPosition             = TargetPositions[nearestTargetPositionIndex];

                // 设置影响方向的三个主要生物群的方向，根据调整
                // 在预定义的权重上：
                // 1）对齐——它应该沿着与周围相似的方向移动多少？
                // note: 我们使用 `alignment/neighborCount`，因为在这种情况下我们需要平均对齐；然而
                // 对齐目前是正在考虑的 cellIndex 内所有 boids 的总和。
                var alignmentResult     = CurrentBoidVariant.AlignmentWeight
                                          * math.normalizesafe((alignment / neighborCount) - forward);
                // 2）分离——它与其他物体的距离有多近，是否太多或太少而不舒服？
                // note: 这里的间隔代表单元的可能中心的总和。我们执行乘法
                // 这样 `currentPosition` 和 `separation` 都被加权以代表整个单元格，而不是
                // 当前的个人 boid。
                var separationResult    = CurrentBoidVariant.SeparationWeight
                                          * math.normalizesafe((currentPosition * neighborCount) - separation);
                // 3) 目标 - 是否仍在朝着目的地前进？
                var targetHeading       = CurrentBoidVariant.TargetWeight
                                          * math.normalizesafe(nearestTargetPosition - currentPosition);

                // 创建避障向量 s.t。它指向最近的障碍物
                // 但在指定的“ObstacleAversionDistance”。如果这个距离大于
                // 到障碍物的当前距离，方向会反转。这模拟了
                // 如果 `currentPosition` 太靠近障碍物，则其重量会推动
                // 当前机体向最快的方向逃跑；但是，如果障碍不是
                // 太近，权重表示主体不需要逃脱但会移动
                // 如果仍然朝那个方向移动，则速度会更慢（注意：我们最终不会使用这个 move-slower
                // 在这种情况下，因为 `targetForward` 决定在遇到障碍物时不使用避障功能
                // 还不够接近）。
                var obstacleSteering                  = currentPosition - nearestObstaclePosition;
                var avoidObstacleHeading              = (nearestObstaclePosition + math.normalizesafe(obstacleSteering)
                    * CurrentBoidVariant.ObstacleAversionDistance) - currentPosition;

                // 更新后的航向。如果不需要回避（即障碍物不在范围内）
                // 预定义的半径），然后使用通常定义的航向，该航向使用以下组合
                // 加权对齐、分离和目标方向向量。
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - CurrentBoidVariant.ObstacleAversionDistance;
                var normalHeading                     = math.normalizesafe(alignmentResult + separationResult + targetHeading);
                var targetForward                     = math.select(normalHeading, avoidObstacleHeading, nearestObstacleDistanceFromRadius < 0);

                // 使用新计算的航向进行更新
                var nextHeading                       = math.normalizesafe(forward + DeltaTime * (targetForward - forward));
                localToWorld = new LocalToWorld
                {
                    Value = float4x4.TRS(
                        // TODO: 预计算速度*dt
                        new float3(localToWorld.Position + (nextHeading * MoveDistance)),
                        quaternion.LookRotationSafe(nextHeading, math.up()),
                        new float3(1.0f, 1.0f, 1.0f))
                };
            }
        }
    }
}
