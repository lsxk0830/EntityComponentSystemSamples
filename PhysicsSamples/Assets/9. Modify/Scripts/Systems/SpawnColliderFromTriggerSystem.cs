using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Material = Unity.Physics.Material;

// 由于 UnityEngine 材质和网格以及 RenderMeshArray，此类必须使用 SystemBase
[BurstCompile]
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class SpawnColliderFromTriggerSystem : SystemBase
{
    private EntityQuery m_TriggerTilesCreateQuery;
    private EntityQuery m_MeshCreationResourcesQuery;

    private UnityEngine.Mesh engineMeshA;
    private UnityEngine.Mesh engineMeshB;  //也被 prototypeC 使用

    private Entity prototypeA;
    private Entity prototypeB;
    private Entity prototypeC;
    private PhysicsCollider colliderA;
    private PhysicsCollider colliderB;
    private PhysicsCollider colliderC;
    private NativeList<BlobAssetReference<Unity.Physics.Collider>> CreatedColliderBlobs; //必须跟踪手动创建的 blob

    [BurstCompile]
    protected override void OnCreate()
    {
        CreatedColliderBlobs = new NativeList<BlobAssetReference<Unity.Physics.Collider>>(Allocator.Persistent);
        m_TriggerTilesCreateQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(TileTriggerCounter),
                typeof(PhysicsCollider),
                typeof(LocalTransform)
            },
        });

        // 获取从 CreateMeshFromResourcesAuthoring 烘焙的 RenderMeshArray 数据
        m_MeshCreationResourcesQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(RenderMeshArray),
                typeof(ResourcesLoadedTag)
            },
        });

        RequireForUpdate<TileTriggerCounter>();
        RequireForUpdate(m_MeshCreationResourcesQuery); // 如果网格资源不存在，请不要更新 system
    }

    [BurstCompile]
    protected override void OnStartRunning()
    {
        var meshResourcesEntities = m_MeshCreationResourcesQuery.ToEntityArray(Allocator.TempJob);
        if (meshResourcesEntities.Length == 0) return;

        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

        // 两个原型之间共享的数据：
        var worldIndex = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex;
        var renderMeshDescription = new RenderMeshDescription(UnityEngine.Rendering.ShadowCastingMode.Off);
        var meshResourcesEntity = meshResourcesEntities[0]; // 只关心第一个
        var renderMeshResources = entityManager.GetSharedComponentManaged<RenderMeshArray>(meshResourcesEntity);

        // 创建原型 A 并测试第一个实用函数：
        prototypeA = entityManager.CreateEntity();
        ecb.AddSharedComponent<PhysicsWorldIndex>(prototypeA, worldIndex);

        // 使用 Entities Graphics 所需的 components 填充基础 entity
        RenderMeshUtility.AddComponents(
            prototypeA,
            entityManager,
            renderMeshDescription,
            renderMeshResources,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(prototypeA, new LocalToWorld());

        engineMeshA = renderMeshResources.MeshReferences[0];
        var colliderBlob =
            Unity.Physics.MeshCollider.Create(engineMeshA, CollisionFilter.Default, Material.Default); // 测试功能
        CreatedColliderBlobs.Add(colliderBlob);
        colliderA = new PhysicsCollider() { Value = colliderBlob, };

        // 创建原型 B 并测试第二个实用函数
        prototypeB = entityManager.CreateEntity();
        ecb.AddSharedComponent<PhysicsWorldIndex>(prototypeB, worldIndex);
        RenderMeshUtility.AddComponents(
            prototypeB,
            entityManager,
            renderMeshDescription,
            renderMeshResources,
            MaterialMeshInfo.FromRenderMeshArrayIndices(1, 1));
        entityManager.AddComponentData(prototypeB, new LocalToWorld());

        engineMeshB = renderMeshResources.MeshReferences[1];
        var engineMeshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(engineMeshB); //测试功能
        colliderBlob =
            Unity.Physics.MeshCollider.Create(engineMeshDataArray, CollisionFilter.Default, Material.Default);
        CreatedColliderBlobs.Add(colliderBlob);
        colliderB = new PhysicsCollider() { Value = colliderBlob, };

        // 创建 Prototype C 并测试第三个实用函数
        prototypeC = entityManager.CreateEntity();
        ecb.AddSharedComponent<PhysicsWorldIndex>(prototypeC, worldIndex);
        RenderMeshUtility.AddComponents(
            prototypeC,
            entityManager,
            renderMeshDescription,
            renderMeshResources,
            MaterialMeshInfo.FromRenderMeshArrayIndices(2, 2));
        entityManager.AddComponentData(prototypeC, new LocalToWorld());

        var engineMeshData = engineMeshDataArray[0];
        colliderBlob =
            Unity.Physics.MeshCollider.Create(engineMeshData, CollisionFilter.Default, Material.Default);
        CreatedColliderBlobs.Add(colliderBlob);
        colliderC = new PhysicsCollider() { Value = colliderBlob, };

        ecb.Playback(entityManager);
        ecb.Dispose();
        engineMeshDataArray.Dispose();
        meshResourcesEntities.Dispose();
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        if (m_MeshCreationResourcesQuery.IsEmpty) return;

        if (prototypeA == Entity.Null || prototypeB == Entity.Null || prototypeC == Entity.Null)
        {
            Debug.Log("SpawnColliderFromTriggerSystem: Prototypes not initialized");
            return;
        }

        using (var entities = m_TriggerTilesCreateQuery.ToEntityArray(Allocator.TempJob))
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var tileInfo = m_TriggerTilesCreateQuery.ToComponentDataArray<TileTriggerCounter>(Allocator.TempJob);
            var tilePosition = m_TriggerTilesCreateQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var spawnJob = new SpawnCollidersFromTriggerJob()
            {
                PrototypeEntityA = prototypeA,
                PrototypeEntityB = prototypeB,
                PrototypeEntityC = prototypeC,
                ColliderA = colliderA,
                ColliderB = colliderB,
                ColliderC = colliderC,
                TileTriggerInfo = tileInfo,
                TilePosition = tilePosition,
                Ecb = ecb.AsParallelWriter(),
            }.Schedule(entities.Length, 128);
            spawnJob.Complete(); // 它在帧的开头运行，因此我们没有 job 依赖项来等待

            ecb.Playback(EntityManager);
            ecb.Dispose();
            tileInfo.Dispose(spawnJob);
            tilePosition.Dispose(spawnJob);
        }
    }

    [BurstCompile]
    struct SpawnCollidersFromTriggerJob : IJobParallelFor
    {
        public Entity PrototypeEntityA;                         // Entities 用作生成的 entities 的原型
        public Entity PrototypeEntityB;
        public Entity PrototypeEntityC;
        public PhysicsCollider ColliderA;
        public PhysicsCollider ColliderB;
        public PhysicsCollider ColliderC;
        public NativeArray<TileTriggerCounter> TileTriggerInfo; // 需要图块 trigger 数量和最大数量
        public NativeArray<LocalTransform> TilePosition;        // 瓷砖的 LocalTransform
        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute(int index)
        {
            var tiles = TileTriggerInfo[index];

            if (tiles.TriggerCount > 0 && tiles.TriggerCount < tiles.MaxTriggerCount)
            {
                Entity body;
                float3 verticalOffset;
                if (tiles.TriggerCount == 1)
                {
                    body = Ecb.Instantiate(index, PrototypeEntityA);
                    verticalOffset = new float3(0, 3, 0);
                    Ecb.AddComponent<PhysicsCollider>(index, body, ColliderA);  //添加之前创建的物理 collider
                }
                else if (tiles.TriggerCount == 2)
                {
                    body = Ecb.Instantiate(index, PrototypeEntityB);
                    verticalOffset = new float3(0, 4, 0);
                    Ecb.AddComponent<PhysicsCollider>(index, body, ColliderB);  //添加之前创建的物理 collider
                }
                else if (tiles.TriggerCount == 3)
                {
                    body = Ecb.Instantiate(index, PrototypeEntityC);
                    verticalOffset = new float3(0, 5, 0);
                    Ecb.AddComponent<PhysicsCollider>(index, body, ColliderC);  //添加之前创建的物理 collider
                }
                else
                {
                    return;
                }

                //添加变换、缩放、localToWorld
                var position = TilePosition[index].Position + verticalOffset;   //生成在 trigger 事件的图块上方
                var tl = LocalTransform.FromPositionRotationScale(position, quaternion.identity, 0.25f);
                Ecb.AddComponent<LocalTransform>(index, body, tl);
                Ecb.AddComponent<LocalToWorld>(index, body, new LocalToWorld { Value = tl.ToMatrix() });

                //Note: 网格-网格 collider 碰撞的成本很高。如果可能的话保持静态（参考：SceneCreationSystem.CreateBody）
                bool isDynamic = false;
                if (isDynamic)
                {
                    Ecb.AddComponent(index, body, PhysicsMass.CreateDynamic(ColliderA.MassProperties, 5.0f));
                    Ecb.AddComponent<PhysicsVelocity>(index, body, new PhysicsVelocity
                    {
                        Linear = float3.zero,
                        Angular = float3.zero
                    });
                    Ecb.AddComponent(index, body, new PhysicsDamping
                    {
                        Linear = 0.01f,
                        Angular = 0.05f
                    });
                }
            }
        }
    }

    [BurstCompile]
    protected override void OnDestroy()
    {
        // 需要手动处置所有手动创建的 BlobAssetReferences
        foreach (var collider in CreatedColliderBlobs)
        {
            if (collider.IsCreated)
                collider.Dispose();
        }
        CreatedColliderBlobs.Dispose();

        EntityManager.DestroyEntity(prototypeA);
        EntityManager.DestroyEntity(prototypeB);
        EntityManager.DestroyEntity(prototypeC);
    }
}
