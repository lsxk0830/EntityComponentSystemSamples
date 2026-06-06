using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class SetAlwaysRelevantSystem : SystemBase
{
    protected override void OnCreate()
    {
        var relevancy = SystemAPI.GetSingletonRW<GhostRelevancy>();
        // 这是设置 OnCreate 但也可以在运行时更新
        // 您还可以添加 AlwaysRelevant component 来在 authoring 时间标记 entities
        relevancy.ValueRW.DefaultRelevancyQuery = GetEntityQuery(typeof(AsteroidScore));
    }

    protected override void OnUpdate() { }
}
