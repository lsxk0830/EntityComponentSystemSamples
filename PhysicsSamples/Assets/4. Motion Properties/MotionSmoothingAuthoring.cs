using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// 该脚本强制 GO 和 DOTS 物理采用较低的固定时间步长，以演示运动平滑
class MotionSmoothingAuthoring : MonoBehaviour
{
    // 出于演示目的，默认为低刻度率
    [Min(0)]
    public int StepsPerSecond = 15;

    float m_FixedTimetep;

    void OnEnable()
    {
        m_FixedTimetep = Time.fixedDeltaTime;
        Time.fixedDeltaTime = 1f / StepsPerSecond;
    }

    void OnDisable() => Time.fixedDeltaTime = m_FixedTimetep;

    void OnValidate() => StepsPerSecond = math.max(0, StepsPerSecond);

    class Baker : Baker<MotionSmoothingAuthoring>
    {
        public override void Bake(MotionSmoothingAuthoring authoring)
        {
            var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SetFixedTimestep
            {
                Value = 1f / authoring.StepsPerSecond
            });
        }
    }
}

struct SetFixedTimestep : IComponentData
{
    public float Value;
}
