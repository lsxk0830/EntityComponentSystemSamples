using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using UnityEngine;

// 运行时碰撞过滤器修改演示使用此 system 来更改静态碰撞过滤器
// scene 中的立方体。球体将穿过立方体并发生碰撞，具体取决于 CollisionFilter
// CollidersWith 值已设置。立方体将更改颜色（材质）以匹配 CollisionFilter CollidesWith 值。
// 该演示仅依赖于使用 3 个 CollisionFilters 类别，并且它们必须是连续的。
// 必须设置的 Physics 类别名称为：
// - 类别 20 = 红色（值 = 1 << 20, 1048576）
// - 类别 21 = 绿色（值 = 1 << 21, 2097152）
// - 类别 22 = 蓝色（值 = 1 << 22, 4194304）
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial class RotateThroughCollisionFiltersSystem : SystemBase
{
    private const uint RedCollisionFilter = 1 << 20;
    private const uint GreenCollisionFilter = 1 << 21;
    private const uint BlueCollisionFilter = 1 << 22;

    protected override void OnCreate()
    {
        RequireForUpdate<ChangeCollisionFilterCountdown>();
    }

    protected override void OnStartRunning()
    {
        var collisionFilterQuery = SystemAPI.QueryBuilder().WithAll<ChangeCollisionFilterCountdown>().Build();
        var entityArray = collisionFilterQuery.ToEntityArray(Allocator.Temp);
        if (entityArray.Length == 0) return;

        // 从 query 中的第一个 entity 获取 RenderMeshArray 并使用它来查找
        // 匹配 scene 中静态立方体的材质
        var mesh = EntityManager.GetSharedComponentManaged<RenderMeshArray>(entityArray[0]);

        // 对于每个红/绿/蓝 GameObject，在 RenderMeshArray 中找到匹配的材质索引
        var indexRed = FindMatchingMaterialIndex("Red", ref mesh);
        var indexBlue = FindMatchingMaterialIndex("Blue", ref mesh);
        var indexGreen = FindMatchingMaterialIndex("Green", ref mesh);

        // 将材质索引保存在单例 component 中以供以后使用
        var colours = new ColoursForFilter
        {
            RedIndex = indexRed,
            BlueIndex = indexBlue,
            GreenIndex = indexGreen
        };
        Entity coloursEntity = EntityManager.CreateEntity(typeof(ColoursForFilter));
        EntityManager.SetComponentData(coloursEntity, colours);
        SystemAPI.SetSingleton(colours);

        entityArray.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton(out ColoursForFilter colourIndices))
            return;

        // 更改静态立方体的 CollisionFilter
        var jobHandle = new RotateFilterCountDownJob()
            .Schedule(Dependency);

        Dependency = jobHandle;
        jobHandle.Complete();

        // 根据 CollisionFilter 更改 colliders 的材质（颜色）
        foreach (var(collider, _, countdown, entity)
                 in SystemAPI.Query<RefRO<PhysicsCollider>, RenderMeshArray, RefRO<ChangeCollisionFilterCountdown>>()
                     .WithEntityAccess()
                     .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
        {
            if (countdown.ValueRO.Countdown == countdown.ValueRO.ResetCountdown) // 材料需要更新
            {
                // 获取修改后的碰撞过滤器
                var filter = collider.ValueRO.Value.Value.GetCollisionFilter();

                // 根据匹配的碰撞过滤器更新材质
                int index = -1;
                if (filter.CollidesWith == RedCollisionFilter)
                {
                    index = colourIndices.RedIndex;
                }
                else if (filter.CollidesWith == GreenCollisionFilter)
                {
                    index = colourIndices.GreenIndex;
                }
                else if (filter.CollidesWith == BlueCollisionFilter)
                {
                    index = colourIndices.BlueIndex;
                }

                if (index > -1)
                {
                    var newMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(entity);
                    newMeshInfo.Material = MaterialMeshInfo.ArrayIndexToStaticIndex(index);
                    EntityManager.SetComponentData(entity, newMeshInfo);
                }
            }
        }
    }

    // 该 job 对 ChangeCollisionFilterCountdown component 进行倒计时，并在发生冲突时更改冲突过滤器
    // 倒计时归零。
    [BurstCompile]
    private partial struct RotateFilterCountDownJob : IJobEntity
    {
        private void Execute(ref PhysicsCollider collider, ref ChangeCollisionFilterCountdown tag)
        {
            if (--tag.Countdown > 0) return;

            tag.Countdown = tag.ResetCountdown; //重置倒计时
            ref var colliderBlob = ref collider.Value.Value;

            if (!colliderBlob.IsUnique)
            {
                Debug.LogWarning($"Warning: The collider {colliderBlob.Type} is not unique. This will change the filter on all shared collider blobs.");
            }

            var currentFilter = colliderBlob.GetCollisionFilter();
            uint filter = currentFilter.CollidesWith;
            uint newValue = filter << 1; // 位移位以获得下一个过滤器
            if (newValue > BlueCollisionFilter) newValue = RedCollisionFilter; // 滚动到第一个过滤器
            CollisionFilter newFilter = new CollisionFilter
            {
                BelongsTo = newValue,
                CollidesWith = newValue,
                GroupIndex = currentFilter.GroupIndex
            };
            colliderBlob.SetCollisionFilter(newFilter);
        }
    }

    // 此方法通过输入 RenderMeshArray Materials[] 数组搜索具有匹配名称的材质
    // 并返回匹配材料的索引
    private int FindMatchingMaterialIndex(string name, ref RenderMeshArray inputRenderMeshArray)
    {
        bool match = false;
        int index = -1;
        while (!match)
        {
            ++index;
            var compareMaterial = inputRenderMeshArray.MaterialReferences[index].Value.name;
            if (compareMaterial.Equals(name))
            {
                match = true;
            }
        }

        return index;
    }
}
