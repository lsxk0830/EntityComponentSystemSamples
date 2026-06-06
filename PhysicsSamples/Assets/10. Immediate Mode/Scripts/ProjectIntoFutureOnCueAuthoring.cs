using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;
using Slider = UnityEngine.UI.Slider;

public struct ProjectIntoFutureTrail : IComponentData {}

public class ProjectIntoFutureOnCueData : IComponentData
{
    public bool NeedsUpdate;
    public int NumSteps;
    public float3 WhiteBallVelocity;
    public Entity WhiteBallEntity;
    public RenderMeshArray GhostMaterial;
    public float TrailScale;
    public NativeArray<float3> Positions;
    public PhysicsWorld LocalWorld;
    public ImmediatePhysicsWorldStepper ImmediatePhysicsStepper;

    public bool IsInitialized => !WhiteBallEntity.Equals(Entity.Null);

    public void Initialize(EntityManager manager, Entity whiteBallEntity, int numSteps, Mesh referenceMesh, Material referenceMaterial, in PhysicsWorld physicsWorld)
    {
        WhiteBallEntity = whiteBallEntity;
        NumSteps = numSteps;
        GhostMaterial = new RenderMeshArray(new[] { referenceMaterial }, new[] { referenceMesh });

        if (!ImmediatePhysicsStepper.Created)
        {
            ImmediatePhysicsStepper = ImmediatePhysicsWorldStepper.Create();
        }

        CheckEntityPool(manager, physicsWorld.NumDynamicBodies);

        NeedsUpdate = true;
    }

    public void CheckEntityPool(EntityManager manager, int numDynamicBodies)
    {
        int totalNumberOfEntities = NumSteps * numDynamicBodies;
        int diff = totalNumberOfEntities - Positions.Length;

        if (diff <= 0)
        {
            return;
        }

        if (Positions.IsCreated) Positions.Dispose();
        Positions = new NativeArray<float3>(totalNumberOfEntities, Allocator.Persistent);

        for (int i = 0; i < diff; i++)
        {
            var ghost = manager.Instantiate(WhiteBallEntity);

            manager.RemoveComponent<PhysicsCollider>(ghost);
            manager.RemoveComponent<PhysicsVelocity>(ghost);

            manager.AddComponentData(ghost, new ProjectIntoFutureTrail());
            manager.AddSharedComponentManaged(ghost, GhostMaterial);
            manager.SetComponentData(ghost, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            var scale = new PostTransformMatrix { Value = float4x4.Scale(TrailScale) };
            manager.AddComponentData(ghost, scale);
        }

        NeedsUpdate = true;
    }
}

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class ProjectIntoFutureOnCueSystem : SystemBase
{
    public ProjectIntoFutureOnCueData Data;

    ProjectIntoFutureOnCueAuthoring m_Authoring;

    public ProjectIntoFutureOnCueSystem(ProjectIntoFutureOnCueAuthoring authoring)
    {
        m_Authoring = authoring;
    }

    [BurstCompile]
    [WithAll(typeof(ProjectIntoFutureTrail))]
    private partial struct ProjectIntoFutureTrailJob : IJobEntity
    {
        public NativeArray<float3> Positions;
        public float TrailScale;
        public int NumSteps;

        public void Execute([EntityIndexInQuery] int entityInQueryIndex, ref LocalTransform localTransform, ref PostTransformMatrix postTransformMatrix)
        {
            var posT0 = Positions[entityInQueryIndex];

            // 如果我们到了最后一步就返回
            if ((entityInQueryIndex % NumSteps) == (NumSteps - 1))
            {
                localTransform.Position = posT0;
                postTransformMatrix.Value = float4x4.Scale(TrailScale);
                return;
            }

            // 获取下一个位置
            var posT1 = Positions[entityInQueryIndex + 1];

            // 如果我们还没有搬家就回来
            var haveMovement = !posT0.Equals(posT1);
            if (!haveMovement)
            {
                localTransform.Position = posT0; // 将此注释掉以在拍摄后留下痕迹。
                postTransformMatrix.Value = float4x4.Scale(TrailScale);

                return;
            }

            // 将 ghost 球放置在 T0 和 T1 之间的中间位置

            localTransform.Position = math.lerp(posT0, posT1, 0.5f);


            // 沿着 T0 和 T1 之间的方向定位球
            // 并在这两个位置之间拉伸球。
            var forward = posT1 - posT0;
            var scaleValue = math.length(forward);
            var rotationValue = quaternion.LookRotationSafe(forward, new float3(0, 1, 0));


            localTransform.Rotation = rotationValue;
            postTransformMatrix.Value.c2.z = scaleValue / localTransform.Scale;
        }
    }

    // “隐藏”entities 到玩家不可见的某个位置。
    // 比从 entities 中删除渲染 component 更有效。
    [BurstCompile]
    struct ResetPositionsJob : IJob
    {
        public NativeArray<float3> Positions;

        public void Execute()
        {
            for (int i = 0; i < Positions.Length; i++)
            {
                Positions[i] = new float3(0, -1, 0);
            }
        }
    }

    [BurstCompile]
    struct StepLocalWorldJob : IJob
    {
        public SimulationStepInput StepInput;
        public SimulationContext SimulationContext;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> TrailPositions;
        public int NumSteps;
        public int StepIndex;

        public void Execute()
        {
            // 更新路线
            for (int b = 0; b < StepInput.World.DynamicBodies.Length; b++)
            {
                TrailPositions[b * NumSteps + StepIndex] = StepInput.World.DynamicBodies[b].WorldFromBody.pos;
            }

            // 步骤本地 world
            ImmediatePhysicsWorldStepper.StepUnityPhysicsSimulationImmediate(StepInput, ref SimulationContext);
        }
    }

#if HAVOK_PHYSICS_EXISTS
    [BurstCompile]
    struct StepLocalWorldHavokJob : IJob
    {
        public SimulationStepInput StepInput;
        public Havok.Physics.SimulationContext SimulationContext;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> TrailPositions;

        public int NumSteps;
        public int StepIndex;

        public void Execute()
        {
            // 更新路线
            for (int b = 0; b < StepInput.World.DynamicBodies.Length; b++)
            {
                TrailPositions[b * NumSteps + StepIndex] = StepInput.World.DynamicBodies[b].WorldFromBody.pos;
            }

            // 步骤本地 world
            ImmediatePhysicsWorldStepper.StepHavokPhysicsSimulationImmediate(StepInput, ref SimulationContext);
        }
    }
#endif

    protected override void OnCreate()
    {
        Data = new ProjectIntoFutureOnCueData
        {
            NeedsUpdate = true,
            NumSteps = 0,
            WhiteBallVelocity = 0f,
            WhiteBallEntity = Entity.Null,
            TrailScale = 0.1f,
            LocalWorld = new PhysicsWorld(),
            Positions = new NativeArray<float3>(),
            GhostMaterial = default,
        };

        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<BuildPhysicsWorldData>();
    }

    protected override void OnDestroy()
    {
        CompleteDependency();

        if (Data.Positions.IsCreated) Data.Positions.Dispose();
        if (Data.LocalWorld.NumBodies != 0) Data.LocalWorld.Dispose();
        if (Data.ImmediatePhysicsStepper.Created) Data.ImmediatePhysicsStepper.Dispose();

        m_Authoring.OnSystemDestroyed();
    }

    protected override void OnUpdate()
    {
        // 使 PhysicsWorld 安全阅读
        // 完成上一步的本地模拟轨迹。
        Dependency.Complete();

        bool bUpdate = true;
        bUpdate &= (Data.IsInitialized && Data.NeedsUpdate);
        bUpdate &= !Data.WhiteBallVelocity.Equals(float3.zero);
        if (!bUpdate)
            return;

        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        Data.CheckEntityPool(EntityManager, world.NumDynamicBodies);

        // 清除踪迹，为新的模拟做好准备 prediction
        new ResetPositionsJob { Positions = Data.Positions}.Run();

        // 如果之前克隆了本地 world，请将其删除并创建一个新的。
        if (Data.LocalWorld.NumBodies > 0)
        {
            Data.LocalWorld.Dispose();
        }
        Data.LocalWorld = world.Clone();

        float timeStep = SystemAPI.Time.DeltaTime;

        if (!SystemAPI.TryGetSingleton(out PhysicsStep stepComponent))
        {
            stepComponent = PhysicsStep.Default;
        }

        var bpwData = SystemAPI.GetSingleton<BuildPhysicsWorldData>();
        var stepInput = new SimulationStepInput
        {
            World = Data.LocalWorld,
            TimeStep = timeStep,
            NumSubsteps = stepComponent.SubstepCount,
            NumSolverIterations = stepComponent.SolverIterationCount,
            SolverStabilizationHeuristicSettings = stepComponent.SolverStabilizationHeuristicSettings,
            Gravity = stepComponent.Gravity,
            SynchronizeCollisionWorld = true,
            HaveStaticBodiesChanged = bpwData.HaveStaticBodiesChanged
        };

        // 将请求的母球速度分配给本地模拟
        Data.LocalWorld.SetLinearVelocity(Data.LocalWorld.GetRigidBodyIndex(Data.WhiteBallEntity), Data.WhiteBallVelocity);

        // 在初始步骤之前同步 CollisionWorld。
        // 由于 stepInput.SynchronizeCollisionWorld 为真，模拟将
        // 在后续步骤中自动同步 CollisionWorld。
        // 仅当我们修改了母球速度时才需要这样做。
        Data.LocalWorld.CollisionWorld.ScheduleUpdateDynamicTree(
            ref Data.LocalWorld, stepInput.TimeStep, stepInput.Gravity, default, false)
            .Complete();

        // 步骤本地 world
        for (int i = 0; i < Data.NumSteps; i++)
        {
            if (stepComponent.SimulationType == SimulationType.UnityPhysics)
            {
                // TODO: 查看 SimulationContext.ScheduleReset 的公共版本
                // 这样我们就可以链接多个 StepLocalWorldJob 实例。

                // 如果动态人体计数增加，则处置并重新分配输入速度缓冲区。
                // 处理先前的冲突和 trigger 事件流并分配新流。
                Data.ImmediatePhysicsStepper.SimulationContext.Reset(stepInput);

                new StepLocalWorldJob()
                {
                    StepInput = stepInput,
                    SimulationContext = Data.ImmediatePhysicsStepper.SimulationContext,
                    StepIndex = i,
                    NumSteps = Data.NumSteps,
                    TrailPositions = Data.Positions
                }.Run();
            }
#if HAVOK_PHYSICS_EXISTS
            else
            {
                Data.ImmediatePhysicsStepper.HavokSimulationContext.Reset(ref Data.LocalWorld);
                new StepLocalWorldHavokJob()
                {
                    StepInput = stepInput,
                    SimulationContext = Data.ImmediatePhysicsStepper.HavokSimulationContext,
                    StepIndex = i,
                    NumSteps = Data.NumSteps,
                    TrailPositions = Data.Positions
                }.Run();
            }
#endif
        }

        Dependency = new ProjectIntoFutureTrailJob
        {
            Positions = Data.Positions,
            TrailScale = Data.TrailScale,
            NumSteps = Data.NumSteps
        }.Schedule(Dependency);

        Data.NeedsUpdate = false;
    }
}

public class ProjectIntoFutureOnCueAuthoring : MonoBehaviour
{
    public Mesh ReferenceMesh;
    public Material ReferenceMaterial;
    public Slider RotateSlider;
    public Slider StrengthSlider;
    public int NumSteps = 25;
    public float TrailScale = 0.1f;

    Entity m_WhiteBallEntity = Entity.Null;
    EntityQuery m_WhiteBallQuery;
    ProjectIntoFutureOnCueSystem m_ProjectIntoFutureOnCueSystem;
    EntityQuery m_PhysicsVelocityQuery;

    bool m_DidStart = false;

    private float3 GetVelocityFromSliders()
    {
        float angle = RotateSlider.value - 90;
        float strength = StrengthSlider.value;
        float3 velocity = strength * math.forward(quaternion.AxisAngle(math.up(), math.radians(angle)));

        return velocity;
    }

    void OnEnable()
    {
        m_DidStart = true;

        m_WhiteBallQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(WhiteBall));
        m_PhysicsVelocityQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(PhysicsVelocity));

        m_ProjectIntoFutureOnCueSystem = new ProjectIntoFutureOnCueSystem(this);
        World.DefaultGameObjectInjectionWorld.AddSystemManaged(m_ProjectIntoFutureOnCueSystem);
        var group = World.DefaultGameObjectInjectionWorld
            .GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
        group.AddSystemToUpdateList(m_ProjectIntoFutureOnCueSystem);
    }

    public void OnSystemDestroyed()
    {
        m_ProjectIntoFutureOnCueSystem = null;
        OnDisable();
    }

    void OnDisable()
    {
        if (World.DefaultGameObjectInjectionWorld?.IsCreated == true)
        {
            if (World.DefaultGameObjectInjectionWorld.EntityManager.IsQueryValid(m_WhiteBallQuery))
            {
                m_WhiteBallQuery.Dispose();
            }
            if (World.DefaultGameObjectInjectionWorld.EntityManager.IsQueryValid(m_PhysicsVelocityQuery))
            {
                m_PhysicsVelocityQuery.Dispose();
            }

            if (m_ProjectIntoFutureOnCueSystem != null)
            {
                World.DefaultGameObjectInjectionWorld.DestroySystemManaged(m_ProjectIntoFutureOnCueSystem);
            }
        }

        m_WhiteBallEntity = Entity.Null;
        m_WhiteBallQuery = default;
        m_PhysicsVelocityQuery = default;
        m_ProjectIntoFutureOnCueSystem = null;

        m_DidStart = false;
    }

    void Update()
    {
        if (m_WhiteBallEntity.Equals(Entity.Null) &&
            World.DefaultGameObjectInjectionWorld.EntityManager.IsQueryValid(m_WhiteBallQuery) &&
            !m_WhiteBallQuery.IsEmpty)
        {
            m_WhiteBallEntity = m_WhiteBallQuery.GetSingletonEntity();
        }

        ProjectIntoFutureOnCueData data = GetData();
        if (data != null)
        {
            data.TrailScale = TrailScale;

            if (!data.IsInitialized && !m_WhiteBallEntity.Equals(Entity.Null))
            {
                EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<PhysicsWorldSingleton>();
                EntityQuery singletonQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(builder);
                PhysicsWorld physicsWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                if (physicsWorld.NumDynamicBodies > 0)
                {
                    data.Initialize(World.DefaultGameObjectInjectionWorld.EntityManager, m_WhiteBallEntity, NumSteps, ReferenceMesh, ReferenceMaterial, in physicsWorld);
                    data.WhiteBallVelocity = GetVelocityFromSliders();
                    data.NeedsUpdate = true;
                }
                singletonQuery.Dispose();
            }
        }
    }

    ProjectIntoFutureOnCueData GetData()
    {
        return m_ProjectIntoFutureOnCueSystem?.Data;
    }

    void CullVelocity()
    {
        int entityCount = m_PhysicsVelocityQuery.CalculateEntityCount();
        NativeArray<PhysicsVelocity> velocities = new NativeArray<PhysicsVelocity>(entityCount, Allocator.Temp);
        for (int i = 0; i < entityCount; i++)
        {
            velocities[i] = new PhysicsVelocity
            {
                Angular = float3.zero,
                Linear = float3.zero
            };
        }
        m_PhysicsVelocityQuery.CopyFromComponentDataArray(velocities);
    }

    public void OnSliderValueChanged()
    {
        if (m_DidStart)
        {
            ProjectIntoFutureOnCueData data = GetData();
            if (data != null && data.IsInitialized)
            {
                data.WhiteBallVelocity = GetVelocityFromSliders();
                data.NeedsUpdate = true;
                CullVelocity();
            }
        }
    }

    public void OnButtonClick()
    {
        if (m_DidStart)
        {
            ProjectIntoFutureOnCueData data = GetData();

            if (data != null)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                // 在主模拟中将所需的速度分配给白球
                var velocity = entityManager.GetComponentData<PhysicsVelocity>(m_WhiteBallEntity);
                velocity.Linear = GetVelocityFromSliders();
                entityManager.SetComponentData(m_WhiteBallEntity, velocity);

                data.WhiteBallVelocity = float3.zero;
                data.NeedsUpdate = true;
            }
        }
    }
}
