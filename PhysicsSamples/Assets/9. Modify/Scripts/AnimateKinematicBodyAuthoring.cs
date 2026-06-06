using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

struct TeleportKinematicBody : IComponentData {}

struct AnimateKinematicBodyCurve : ISharedComponentData, IEquatable<AnimateKinematicBodyCurve>
{
    public AnimationCurve TranslationCurve;
    public AnimationCurve OrientationCurve;

    public bool Equals(AnimateKinematicBodyCurve other) =>
        Equals(TranslationCurve, other.TranslationCurve) && Equals(OrientationCurve, other.OrientationCurve);

    public override bool Equals(object obj) => obj is AnimateKinematicBodyCurve other && Equals(other);

    public override int GetHashCode() =>
        unchecked((int)math.hash(new int2(TranslationCurve?.GetHashCode() ?? 0, OrientationCurve?.GetHashCode() ?? 0)));
}

// 沿 z 轴平移主体并按照动画曲线绕 y 轴旋转
[RequireComponent(typeof(PhysicsBodyAuthoring))]
class AnimateKinematicBodyAuthoring : MonoBehaviour
{
    public enum Mode
    {
        Simulate,
        Teleport
    }

    #pragma warning disable 649
    public Mode AnimateMode;
    #pragma warning restore 649

    // 默认在 1 秒内以恒定速度向后平移 6 个单位，并从头开始重复
    public AnimationCurve TranslationCurve = new AnimationCurve(
        new Keyframe(0f, 3f, -6f, -6f),
        new Keyframe(1f, -3f, -6f, -6f)
    )
    {
        preWrapMode = WrapMode.Loop,
        postWrapMode = WrapMode.Loop
    };

    // 默认值在 2 秒内围绕 y 轴在正负 15 度之间重复平滑旋转
    public AnimationCurve OrientationCurve = new AnimationCurve(
        new Keyframe(0f, -15f, 0f, 0f),
        new Keyframe(1f, 15f, 0f, 0f),
        new Keyframe(2f, -15f, 0f, 0f)
    )
    {
        preWrapMode = WrapMode.Loop,
        postWrapMode = WrapMode.Loop
    };
}

class AnimateKinematicBodyBaker : Baker<AnimateKinematicBodyAuthoring>
{
    public override void Bake(AnimateKinematicBodyAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        if (authoring.AnimateMode == AnimateKinematicBodyAuthoring.Mode.Teleport)
            AddComponent<TeleportKinematicBody>(entity);

        AddSharedComponentManaged(entity, new AnimateKinematicBodyCurve
        {
            TranslationCurve = authoring.TranslationCurve,
            OrientationCurve = authoring.OrientationCurve
        });
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
partial struct AnimateKinematicBodySystem : ISystem
{
    // cleanup component 用于识别第一帧上的新动画主体
    struct Initialized : ICleanupComponentData {}

    // 对动画曲线进行采样以生成新​​的位置和方向
    // 曲线沿 z 轴平移并设置绕 y 轴旋转的方向
    static void Sample(in AnimateKinematicBodyCurve curve, in float t, ref float3 position, ref quaternion orientation)
    {
        position.z = curve.TranslationCurve.Evaluate(t);
        orientation = quaternion.AxisAngle(math.up(), math.radians(curve.OrientationCurve.Evaluate(t)));
    }

    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

        // 将所有新物体传送到第一帧上的适当位置
        // 否则它们可能会与初始位置和动画第一帧之间的物体发生碰撞

        foreach (var(transform, curve, entity)
                 in SystemAPI.Query<RefRW<LocalTransform>, AnimateKinematicBodyCurve>().WithEntityAccess().WithNone<Initialized>())

        {
            // 采样曲线并将结果直接应用于平移和旋转

            Sample(curve, elapsedTime, ref transform.ValueRW.Position, ref transform.ValueRW.Rotation);

            commandBuffer.AddComponent<Initialized>(entity);
        }

        // 通过平移和旋转 components 移动运动体会将它们传送到目标位置

        foreach (var(transform, mass, curve) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PhysicsMass>, AnimateKinematicBodyCurve>().WithAll<TeleportKinematicBody, Initialized>())

        {
            // 采样曲线并将结果直接应用于平移和旋转

            Sample(curve, elapsedTime, ref transform.ValueRW.Position, ref transform.ValueRW.Rotation);

        }

        var tickSpeed = 1f / SystemAPI.Time.DeltaTime;

        // 通过 PhysicsVelocity component 移动运动体将与其经过的任何物体生成接触事件
        // use PhysicsVelocity.CalculateVelocityToTarget() to compute the velocity required to move to a desired target position
        // NOTE: 如果您想避免不正确的接触事件，则只能在运动不连续时将运动体传送到框架上
        // 如果这样做，请确保在该框架上设置 PhysicsGraphicalSmoothing.ApplySmoothing = 0（如果使用它），以防止错误的 interpolation

        foreach (var(velocity, transform, mass, curve) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<LocalTransform>, RefRO<PhysicsMass>, AnimateKinematicBodyCurve>().WithAll<Initialized>().WithNone<TeleportKinematicBody>())

        {
            // 采样曲线以确定目标位置和方向

            var targetTransform = new RigidTransform(transform.ValueRO.Rotation, transform.ValueRO.Position);

            Sample(curve, elapsedTime, ref targetTransform.pos, ref targetTransform.rot);

            // 修改 PhysicsVelocity 移动到目标位置

            velocity.ValueRW = PhysicsVelocity.CalculateVelocityToTarget(mass.ValueRO, transform.ValueRO.Position, transform.ValueRO.Rotation, targetTransform, tickSpeed);

        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose(); // 无法使用上面的方法，因为出现 DCICE002 错误
    }
}
