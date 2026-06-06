using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.VFX;

[RequireMatchingQueriesForUpdate]
public partial struct JumpingSpherePSSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var time = (float)SystemAPI.Time.ElapsedTime;
        var y = math.abs(math.cos(time * 3f));

        //让球体跳跃
        foreach (var translation in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<JumpingSphereTag>())
        {
            translation.ValueRW.Position = new float3(0, y, 0);
        }

        //根据变量 y 播放粒子 system
        foreach (var particleSystem in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<VisualEffect>>().WithAll<JumpingSpherePSTag>())
        {
            if (y < 0.05f) particleSystem.Value.Play();
        }
    }
}
