using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Baking.AutoAuthoring.BakingTypeAutoAuthoring
{
    // 烘焙无法在 baker 中处理的附加 authoring 属性。
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PositionBakeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Complex>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (complex, transform) in
                     SystemAPI.Query<RefRO<Complex>, RefRW<LocalTransform>>()
                         .WithAll<BakedEntity>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                transform.ValueRW = LocalTransform.FromPositionRotation(complex.ValueRO.Properties.Position,
                    quaternion.Euler(math.radians(complex.ValueRO.Properties.Rotation)));
            }
        }
    }
}
