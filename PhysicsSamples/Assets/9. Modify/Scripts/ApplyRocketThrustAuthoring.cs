using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using Unity.Physics.Extensions;
using UnityEngine;
using Unity.Transforms;

using Math = Unity.Physics.Math;

[RequireComponent(typeof(PhysicsBodyAuthoring))]
public class ApplyRocketThrustAuthoring : MonoBehaviour
{
    [Min(0)] public float Magnitude = 1.0f;
    public Vector3 LocalDirection = -Vector3.forward;
    public Vector3 LocalOffset = Vector3.zero;

    public void OnDrawGizmos()
    {
        if (LocalDirection.Equals(Vector3.zero)) return;

        var originalColor = Gizmos.color;
        var originalMatrix = Gizmos.matrix;

        Gizmos.color = Color.red;

        // 计算最终的 Physics 主体运行时坐标 system，该坐标消除了父级中非均匀缩放带来的倾斜
        var worldFromLocalRigidTransform = Math.DecomposeRigidBodyTransform(transform.localToWorldMatrix);
        var worldFromLocal = Matrix4x4.TRS(worldFromLocalRigidTransform.pos, worldFromLocalRigidTransform.rot, Vector3.one);

        Vector3 directionWorld = worldFromLocal.MultiplyVector(LocalDirection.normalized);
        Vector3 offsetWorld = worldFromLocal.MultiplyPoint(LocalOffset);

        // 根据 world 主体变换和局部偏移和方向计算最终的 world 推力坐标 system
        Math.CalculatePerpendicularNormalized(directionWorld, out _, out var directionPerpendicular);
        var worldFromThrust = Matrix4x4.TRS(offsetWorld, Quaternion.LookRotation(directionWorld, directionPerpendicular), Vector3.one);

        Gizmos.matrix = worldFromThrust;

        float Shift = Magnitude * 0.1f;
        Gizmos.DrawFrustum(new Vector3(0, 0, -Shift), UnityEngine.Random.Range(1.0f, 2.5f), Magnitude, Shift, 1.0f);

        Gizmos.matrix = originalMatrix;
        Gizmos.color = originalColor;
    }
}

class ApplyRocketThrustAuthoringBaker : Baker<ApplyRocketThrustAuthoring>
{
    public override void Bake(ApplyRocketThrustAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new ApplyRocketThrust
        {
            Magnitude = authoring.Magnitude,
            Direction = authoring.LocalDirection.normalized,
            Offset = authoring.LocalOffset,
        });
    }
}

public struct ApplyRocketThrust : IComponentData
{
    public float Magnitude;
    public float3 Direction;
    public float3 Offset;
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct ApplyRocketThrustSystem : ISystem
{
    [BurstCompile]
    public partial struct ApplyRocketThurstJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref ApplyRocketThrust rocket, ref LocalTransform transform, ref PhysicsVelocity pv, ref PhysicsMass pm)
        {
            // 牛顿第三定律指出，每一个作用力都会产生一个大小相等、方向相反的反应。
            // 由于这是火箭推力，因此施加的冲量使用负方向。
            float3 impulse = -rocket.Direction * rocket.Magnitude;
            impulse = math.rotate(transform.Rotation.value, impulse);
            impulse *= DeltaTime;

            float3 offset = math.rotate(transform.Rotation, rocket.Offset) + transform.Position;

            pv.ApplyImpulse(pm, transform.Position, transform.Rotation, impulse, offset);
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ApplyRocketThurstJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        }.Schedule(state.Dependency);
    }
}
