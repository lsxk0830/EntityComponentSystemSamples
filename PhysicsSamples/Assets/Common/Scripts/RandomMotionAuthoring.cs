using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public struct RandomMotion : IComponentData
{
    public float CurrentTime;
    public float3 InitialPosition;
    public float3 DesiredPosition;
    public float Speed;
    public float Tolerance;
    public float3 Range;
}

// 此行为将设置动态物体的线速度以随机选择
// 空间中的点。当物体获得指定公差的随机位置时，
// 选择一个新的随机位置，正文从那里开始标题。
public class RandomMotionAuthoring : MonoBehaviour
{
    public float3 Range = new float3(1);
}

class RandomMotionAuthoringBaker : Baker<RandomMotionAuthoring>
{
    public override void Bake(RandomMotionAuthoring authoring)
    {
        var length = math.length(authoring.Range);
        var transform = GetComponent<Transform>();
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new RandomMotion
        {
            InitialPosition = transform.position,
            DesiredPosition = transform.position,
            Speed = length * 0.001f,
            Tolerance = length * 0.1f,
            Range = authoring.Range,
        });
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct RandomMotionSystem : ISystem
{
    [BurstCompile]
    public partial struct EntityRandomMotionJob : IJobEntity
    {
        public Random Random;
        public PhysicsStep StepComponent;
        public float DeltaTime;

        public void Execute(ref RandomMotion motion, ref PhysicsVelocity velocity, in LocalTransform transform, in PhysicsMass mass)
        {
            motion.CurrentTime += DeltaTime;

            Random.InitState((uint)(motion.CurrentTime * 1000));

            var currentOffset = transform.Position - motion.InitialPosition;
            var desiredOffset = motion.DesiredPosition - motion.InitialPosition;
            // 如果我们距离目的地足够近，请选择一个新目的地
            if (math.lengthsq(transform.Position - motion.DesiredPosition) < motion.Tolerance)
            {
                var min = new float3(-math.abs(motion.Range));
                var max = new float3(math.abs(motion.Range));
                desiredOffset = Random.NextFloat3(min, max);
                motion.DesiredPosition = desiredOffset + motion.InitialPosition;
            }
            var offset = desiredOffset - currentOffset;
            // 平滑地改变线速度
            velocity.Linear = math.lerp(velocity.Linear, offset, motion.Speed);
            if (mass.InverseMass != 0)
            {
                velocity.Linear -= StepComponent.Gravity * DeltaTime;
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RandomMotion>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<PhysicsStep>(out var stepComponent))
            stepComponent = PhysicsStep.Default;

        state.Dependency = new EntityRandomMotionJob
        {
            Random = new Random(),
            DeltaTime = SystemAPI.Time.DeltaTime,
            StepComponent = stepComponent
        }.Schedule(state.Dependency);
    }
}
