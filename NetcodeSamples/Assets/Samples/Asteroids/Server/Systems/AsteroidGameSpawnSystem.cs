using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.HostMigration;
using Unity.Transforms;

namespace Asteroids.Server
{
    /// <summary>Handles 生成船舶和 Asteroids.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    // system 已移至 InitializationSystemGroup，以避免与 Netcode systems 出现竞争条件
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct AsteroidGameSpawnSystem : ISystem
    {
        EntityQuery m_LevelQuery;
        EntityQuery m_ConnectionQuery;
        EntityQuery m_ShipQuery;
        EntityQuery m_DynamicAsteroidsQuery;
        EntityQuery m_StaticAsteroidsQuery;
        EntityQuery m_HostMigrationQuery;

        Entity m_AsteroidPrefab;
        Entity m_ShipPrefab;

        float m_AsteroidRadius;
        float m_ShipRadius;

        private NativeReference<Random> randomReference;
        ComponentLookup<PlayerStateComponentData> playerStateFromEntity;
        ComponentLookup<CommandTarget> commandTargetFromEntity;
        ComponentLookup<NetworkId> networkIdFromEntity;
        ComponentLookup<LocalTransform> localTransformLookup;

        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, ShipStateComponentData>();

            m_ShipQuery = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAll<LocalTransform, AsteroidTagComponentData>()
                .WithNone<StaticAsteroid>();

            m_DynamicAsteroidsQuery = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAll<StaticAsteroid>();

            m_StaticAsteroidsQuery = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAllRW<LevelComponent>();

            m_LevelQuery = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAllRW<NetworkId>(); // 无法使用 NetworkStreamConnection，不适用于单个 world 主机。

            m_ConnectionQuery = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAll<HostMigrationInProgress>();

            m_HostMigrationQuery = state.GetEntityQuery(builder);

            state.RequireForUpdate(m_LevelQuery);
            state.RequireForUpdate<AsteroidsSpawner>();

            // 确保每个随机数都是唯一的种子（不兼容 Burst）...
            // AND 随机反馈到自身，确保小行星生成具有更好的质量随机性。
            var fileTimeUtc = System.DateTime.UtcNow.ToFileTimeUtc();
            randomReference = new NativeReference<Random>(Random.CreateFromIndex((uint) fileTimeUtc), Allocator.Persistent);

            playerStateFromEntity = state.GetComponentLookup<PlayerStateComponentData>();
            commandTargetFromEntity = state.GetComponentLookup<CommandTarget>();
            networkIdFromEntity = state.GetComponentLookup<NetworkId>();
            localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            randomReference.Dispose();
            // 其他的会自动处理。
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_ConnectionQuery.IsEmptyIgnoreFilter)
            {
                // 没有连接的玩家，只需摧毁所有小行星即可拯救 CPU
                state.EntityManager.DestroyEntity(m_StaticAsteroidsQuery);
                state.EntityManager.DestroyEntity(m_DynamicAsteroidsQuery);
                return;
            }

            // 如果正在进行主机迁移，请跳过此处的任何生成，直到完成为止
            if (!m_HostMigrationQuery.IsEmptyIgnoreFilter)
                return;

            var settings = SystemAPI.GetSingleton<ServerSettings>();
            if (m_AsteroidPrefab == Entity.Null || m_ShipPrefab == Entity.Null)
            {
                var asteroidsSpawner = SystemAPI.GetSingleton<AsteroidsSpawner>();
                m_AsteroidPrefab = settings.levelData.staticAsteroidOptimization ? asteroidsSpawner.StaticAsteroid : asteroidsSpawner.Asteroid;
                m_ShipPrefab = asteroidsSpawner.Ship;

                if (m_AsteroidPrefab == Entity.Null || m_ShipPrefab == Entity.Null)
                    return;

                m_AsteroidRadius = settings.levelData.asteroidCollisionRadius;
                m_ShipRadius = settings.levelData.shipCollisionRadius;
            }

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            playerStateFromEntity.Update(ref state);
            commandTargetFromEntity.Update(ref state);
            networkIdFromEntity.Update(ref state);
            localTransformLookup.Update(ref state);

            // 优化：防止聚集数十万个小行星，从而杀死主线程。
            // Note: 这将导致飞船可能在小行星内生成，但地图尺寸更大
            // 应该使这成为一个低重现事件。
            // TODO - 我们可以摧毁你的生成方块中的所有小行星 chunks 吗？
            int currentAsteroidsCount = m_DynamicAsteroidsQuery.CalculateEntityCountWithoutFiltering() + m_StaticAsteroidsQuery.CalculateEntityCountWithoutFiltering();
            NativeList<Entity> dynamicAsteroidEntities = default;
            NativeList<LocalTransform> dynamicAsteroidTransforms = default;
            NativeList<StaticAsteroid> staticAsteroids = default;
            NativeList<Entity> staticAsteroidEntities = default;
            var tooManyEntitiesToGather = currentAsteroidsCount > 10_000;
            if (tooManyEntitiesToGather)
            {
                dynamicAsteroidEntities = new(0, state.WorldUpdateAllocator);
                dynamicAsteroidTransforms = new(0, state.WorldUpdateAllocator);
                staticAsteroids = new(0, state.WorldUpdateAllocator);
                staticAsteroidEntities = dynamicAsteroidEntities;
            }
            else
            {
                dynamicAsteroidEntities = m_DynamicAsteroidsQuery.ToEntityListAsync(state.WorldUpdateAllocator, out var asteroidEntitiesHandle);
                dynamicAsteroidTransforms = m_DynamicAsteroidsQuery.ToComponentDataListAsync<LocalTransform>(state.WorldUpdateAllocator, out var asteroidTranslationsHandle);
                staticAsteroids = m_StaticAsteroidsQuery.ToComponentDataListAsync<StaticAsteroid>(state.WorldUpdateAllocator, out var staticAsteroidsHandle);
                staticAsteroidEntities = m_StaticAsteroidsQuery.ToEntityListAsync(state.WorldUpdateAllocator, out var staticAsteroidEntitiesHandle);
                state.Dependency = JobHandle.CombineDependencies(asteroidEntitiesHandle, asteroidTranslationsHandle,
                    JobHandle.CombineDependencies(staticAsteroidsHandle, staticAsteroidEntitiesHandle, state.Dependency));
            }

            var shipTransforms = m_ShipQuery.ToComponentDataListAsync<LocalTransform>(state.WorldUpdateAllocator, out var shipTranslationsHandle);
            var level = m_LevelQuery.ToComponentDataListAsync<LevelComponent>(state.WorldUpdateAllocator, out var levelHandle);

            state.Dependency = JobHandle.CombineDependencies(shipTranslationsHandle, levelHandle, state.Dependency);

            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();
            var fixedDeltaTime = tickRate.SimulationFixedTimeStep;

            var shipLevelPadding = m_ShipRadius + 50;
            var asteroidLevelPadding = m_AsteroidRadius + 3;
            var minShipAsteroidSpawnDistance = m_ShipRadius + m_AsteroidRadius + 100;
            var minShipToShipSpawnDistance = (m_ShipRadius + m_ShipRadius) + 300;

            var shipTransformsList = new NativeList<LocalTransform>(64, state.WorldUpdateAllocator);

            var shipListJob = new CreateShipListJob
            {
                shipTransformsIn = shipTransforms,
                shipTransformsOut = shipTransformsList,
            };
            state.Dependency = shipListJob.Schedule(state.Dependency);

            var spawnPlayerShips = new SpawnPlayerShips
            {
                ecb = ecb,
                playerStateFromEntity = playerStateFromEntity,
                commandTargetFromEntity = commandTargetFromEntity,
                networkIdFromEntity = networkIdFromEntity,
                shipTransforms = shipTransformsList,
                dynamicAsteroidTransforms = dynamicAsteroidTransforms,
                localTransformLookup = localTransformLookup,
                staticAsteroids = staticAsteroids,
                dynamicAsteroidEntities = dynamicAsteroidEntities,
                staticAsteroidEntities = staticAsteroidEntities,
                level = level,
                random = randomReference,
                tick = tick,
                shipPrefab = m_ShipPrefab,
                fixedDeltaTime = fixedDeltaTime,
                shipLevelPadding = shipLevelPadding,
                minShipAsteroidSpawnDistance = minShipAsteroidSpawnDistance,
                minShipToShipSpawnDistance = minShipToShipSpawnDistance
            };
            state.Dependency = spawnPlayerShips.Schedule(state.Dependency);

            var spawnAsteroids = new SpawnAllAsteroids
            {
                ecb = ecb,
                shipTransforms = shipTransformsList.AsDeferredJobArray(),
                localTransformLookup = localTransformLookup,
                level = level,
                random = randomReference,
                tick = tick,
                asteroidPrefab = m_AsteroidPrefab,
                asteroidLevelPadding = asteroidLevelPadding,
                minShipAsteroidSpawnDistance = minShipAsteroidSpawnDistance,
                currentAsteroidsCount = currentAsteroidsCount,
                numAsteroids = settings.levelData.numAsteroids,
                asteroidVelocity = settings.levelData.asteroidVelocity,
                staticAsteroidOptimization = settings.levelData.staticAsteroidOptimization ? 1: 0
            };
            state.Dependency = spawnAsteroids.Schedule(state.Dependency);
        }

        [BurstCompile]
        struct CreateShipListJob : IJob
        {
            [ReadOnly] public NativeList<LocalTransform> shipTransformsIn;
            public NativeList<LocalTransform> shipTransformsOut;
            public void Execute()
            {
                shipTransformsOut.AddRange(shipTransformsIn.AsArray());
            }
        }

        [BurstCompile]
        [WithAll(typeof(PlayerSpawnRequest))]
        internal partial struct SpawnPlayerShips : IJobEntity
        {
            public EntityCommandBuffer ecb;
            public ComponentLookup<PlayerStateComponentData> playerStateFromEntity;
            public ComponentLookup<CommandTarget> commandTargetFromEntity;
            public ComponentLookup<NetworkId> networkIdFromEntity;
            public NativeList<LocalTransform> shipTransforms;
            [ReadOnly] public NativeList<LocalTransform> dynamicAsteroidTransforms;
            [ReadOnly] public ComponentLookup<LocalTransform> localTransformLookup;
            [ReadOnly] public NativeList<StaticAsteroid> staticAsteroids;
            [ReadOnly] public NativeList<Entity> dynamicAsteroidEntities;
            [ReadOnly] public NativeList<Entity> staticAsteroidEntities;
            [ReadOnly] public NativeList<LevelComponent> level;

            public NativeReference<Random> random;
            public NetworkTick tick;
            public Entity shipPrefab;
            public float fixedDeltaTime;
            public float shipLevelPadding;
            public float minShipAsteroidSpawnDistance;
            public float minShipToShipSpawnDistance;

            void Execute(Entity entity, in ReceiveRpcCommandRequest requestSource)
            {
                // 销毁生成请求：
                ecb.DestroyEntity(entity);

                // 请求有效吗？
                if (!playerStateFromEntity.HasComponent(requestSource.SourceConnection) ||
                    !commandTargetFromEntity.HasComponent(requestSource.SourceConnection) ||
                    commandTargetFromEntity[requestSource.SourceConnection].targetEntity != Entity.Null ||
                    playerStateFromEntity[requestSource.SourceConnection].IsSpawning != 0)
                    return;

                // 尝试为飞船找到一个不靠近其他玩家的随机生成位置。
                // 但不要允许失败，而是采取“糟糕”的立场。
                var rand = random.Value;
                TryFindSpawnPos(ref rand, shipTransforms.AsArray(), level[0], shipLevelPadding, minShipToShipSpawnDistance, out var validShipPos);
                random.Value = rand;

                // 实例化船舶：
                var shipEntity = ecb.Instantiate(shipPrefab);
                //@罗纳德。这是必要的，因为网格不支持正确的缩放因子
                var originalScale = localTransformLookup[shipPrefab].Scale;
                var trans = LocalTransform.FromPositionRotationScale(
                        validShipPos,
                        quaternion.RotateZ(math.radians(90f)),
                        originalScale
                    );

                ecb.SetComponent(shipEntity, trans);
                ecb.SetComponent(shipEntity, new GhostOwner {NetworkId = networkIdFromEntity[requestSource.SourceConnection].Value});
                ecb.SetComponent(shipEntity, new PlayerIdComponentData {PlayerEntity = requestSource.SourceConnection});
                ecb.SetComponent(requestSource.SourceConnection, new CommandTarget {targetEntity = shipEntity});
                ecb.SetComponent(requestSource.SourceConnection, new PlayerStateComponentData {IsSpawning = 0});
                ecb.AppendToBuffer(requestSource.SourceConnection, new LinkedEntityGroup {Value = shipEntity});

                // 添加到列表中以防止下面的小行星在它们附近生成。
                shipTransforms.Add(trans);

                // 将玩家标记为当前正在生成
                playerStateFromEntity[requestSource.SourceConnection] = new PlayerStateComponentData {IsSpawning = 1};

                // 摧毁距离此生成点太近的小行星：
                var minShipAsteroidSpawnDistanceSqr = minShipAsteroidSpawnDistance * minShipAsteroidSpawnDistance;
                for (int i = 0; i < dynamicAsteroidTransforms.Length; i++)
                {
                    if (math.distancesq(dynamicAsteroidTransforms[i].Position, validShipPos) < minShipAsteroidSpawnDistanceSqr)
                        ecb.DestroyEntity(dynamicAsteroidEntities[i]);
                }
                for (int i = 0; i < staticAsteroids.Length; i++)
                {
                    if (math.distancesq(staticAsteroids[i].GetPosition(tick, 1f, fixedDeltaTime), validShipPos) < minShipAsteroidSpawnDistanceSqr)
                        ecb.DestroyEntity(staticAsteroidEntities[i]);
                }
            }
        }

        [BurstCompile]
        struct SpawnAllAsteroids : IJob
        {
            public EntityCommandBuffer ecb;
            [ReadOnly] public NativeArray<LocalTransform> shipTransforms;
            [ReadOnly] public ComponentLookup<LocalTransform> localTransformLookup;
            [ReadOnly] public NativeList<LevelComponent> level;

            public NativeReference<Random> random;
            public NetworkTick tick;
            public Entity asteroidPrefab;
            public float asteroidLevelPadding;
            public float minShipAsteroidSpawnDistance;
            public int currentAsteroidsCount;
            public int numAsteroids;
            public float asteroidVelocity;
            public int staticAsteroidOptimization;

            public void Execute()
            {
                var rand = random.Value;
                for (int i = currentAsteroidsCount; i < numAsteroids; ++i)
                {
                    // 在随机位置生成小行星，假设我们能找到一颗不在船下的有效小行星。
                    // 不要将此视为错误（因为它可能偶尔会偶然发生，或者如果地图上挤满了船只）。
                    // 相反，停止尝试再生成此帧即可。
                    if (!TryFindSpawnPos(ref rand, shipTransforms, level[0], asteroidLevelPadding, minShipAsteroidSpawnDistance, out var validAsteroidPos))
                        break;

                    var angle = rand.NextFloat(-0.0f, 359.0f);
                    //@罗纳德。这是必要的，因为网格不支持正确的缩放因子
                    var originalScale = localTransformLookup[asteroidPrefab].Scale;
                    var trans = LocalTransform.FromPositionRotationScale(
                        validAsteroidPos,
                        quaternion.RotateZ(math.radians(angle)),
                        originalScale);
                    var vel = new Velocity {Value = math.mul(trans.Rotation, new float3(0, asteroidVelocity, 0)).xy};

                    var e = ecb.Instantiate(asteroidPrefab);

                    ecb.SetComponent(e, trans);
                    if (staticAsteroidOptimization == 1)
                    {
                        UnityEngine.Debug.Assert(tick.IsValid);
                        ecb.SetComponent(e,
                            new StaticAsteroid
                            {
                                InitialPosition = trans.Position.xy, InitialVelocity = vel.Value, InitialAngle = angle,
                                SpawnTick = tick,
                            });
                    }
                    else
                    {
                        ecb.SetComponent(e, vel);
                    }
                }

                random.Value = rand;
            }
        }

        static bool TryFindSpawnPos(ref Random rand, NativeArray<LocalTransform> avoidTransforms, LevelComponent levelComponent, float levelPadding, float minSpawnDistance, out float3 validRandomAsteroidPosition)
        {
            validRandomAsteroidPosition = 0;
            var minSpawnDistanceSqr = minSpawnDistance * minSpawnDistance;

            for (var attempt = 0; attempt < 5; attempt++)
            {
                validRandomAsteroidPosition = new float3(rand.NextFloat(levelPadding, levelComponent.levelWidth - levelPadding), rand.NextFloat(levelPadding, levelComponent.levelHeight - levelPadding), 0);

                var isValidLocation = true;
                for (var i = 0; i < avoidTransforms.Length; i++)
                {
                    if (math.distancesq(avoidTransforms[i].Position, validRandomAsteroidPosition) < minSpawnDistanceSqr)
                    {
                        isValidLocation = false;
                        break;
                    }
                }
                if(isValidLocation)
                    return true;
                minSpawnDistanceSqr *= 0.8f; // 减小大小并重试。
            }
            return false;
        }
    }
}
