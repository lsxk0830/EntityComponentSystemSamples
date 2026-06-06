using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace Baking.BakingDependencies
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    // 这是一个 baking system，一个仅在 baking world 中运行的 system，在 bakers 之后还有 run。它提供
    // more flexibility (e.g. accessing any entity) but doesn't allow expressing dependencies. This is the reason why
    // 该示例依赖于 baker（注册依赖项）和 system（执行以下操作）之间的通信
    // 可渲染的 entities 所需的设置，其中不存在可直接在 baker 中执行此操作的 API）。
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ImageGeneratorBakingSystem : ISystem
    {
        EntityQuery m_ImageGeneratorEntitiesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 收集 ImageGenerator baker 处理过的所有 entities。
            m_ImageGeneratorEntitiesQuery =
                SystemAPI.QueryBuilder().WithAll<ImageGeneratorEntity, MeshArrayBakingType>().Build();

            // 过滤 entities，其 ImageGeneratorEntity 缓冲区已更新。换句话说，
            // entities，在此 baking 过程中，baker 具有 run。或者，我们可以过滤
            // 相反，MeshArrayBakingType component，因为它是由相同的 baker 添加的。
            m_ImageGeneratorEntitiesQuery.SetChangedVersionFilter(ComponentType.ReadOnly<ImageGeneratorEntity>());
            state.RequireForUpdate(m_ImageGeneratorEntitiesQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var renderMeshDescription = new RenderMeshDescription(UnityEngine.Rendering.ShadowCastingMode.Off);
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

            // 由于以下循环会进行结构更改，因此无法使用 SystemAPI.Query() 迭代 query 的内容。
            // 因此，每个值都是通过随机访问 component 值来处理的。这个比较贵，但是
            // entities 的金额预计较低。因为每个 ImageGenerator 中只有一个这样的 entity，并且也
            // 因为 system 是反应性的，所以这里只需要处理已更新的。
            var entities = m_ImageGeneratorEntitiesQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var bakingType = SystemAPI.ManagedAPI.GetComponent<MeshArrayBakingType>(entity);
                var bakingEntities = SystemAPI.GetBuffer<ImageGeneratorEntity>(entity).Reinterpret<Entity>()
                    .ToNativeArray(Allocator.Temp);

                foreach (var bakingEntity in bakingEntities)
                {
                    RenderMeshUtility.AddComponents(bakingEntity, state.EntityManager, renderMeshDescription,
                        bakingType.meshArray, materialMeshInfo);
                }
            }
        }
    }
#endif
}
