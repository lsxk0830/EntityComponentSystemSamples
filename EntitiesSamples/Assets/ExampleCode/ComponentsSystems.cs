using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#if false
namespace ExampleCode.Components
{
    public struct EnergyShield : IComponentData
    {
        public int HitPoints;
        public int MaxHitPoints;
        public float RechargeDelay;
        public float RechargeRate;
    }

    public struct OnFire : IComponentData
    {
        // 空的 component 称为“标签 component”。
        // 标签 components 不占用存储空间，但可以
        // 像任何其他 component 一样查询、添加和删除。
    }
}

namespace ExampleCode.SystemsAndSystemGroups
{
    // 创建和销毁 entities 的示例 system。
    // 此 system 将添加到名为 MySystemGroup 的 system 组。
    // ISystem 方法通过标记为 Burst “入口点”
    // 它们具有 BurstCompile 属性。
    [UpdateInGroup(typeof(MySystemGroup))]
    public partial struct MySystem : ISystem
    {
        // 创建 system 时调用一次。
        // 空时可以省略。
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        // 当 system 被销毁时调用一次。
        // 空时可以省略。
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        // 通常称为每一帧。当 system 被更新时
        // 由其所属的 system 组确定。
        // 空时可以省略。
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }
    }

    // system 组示例。
    public partial class MySystemGroup : ComponentSystemGroup
    {
        // system 组留空，除非您想要
        // 覆盖 OnUpdate、OnCreate 或 OnDestroy。
    }
}

namespace ExampleCode.Queries
{
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityQuery myQuery = SystemAPI.QueryBuilder().WithAll<Foo, Bar, Apple>().WithNone<Banana>().Build();
            ComponentTypeHandle<Foo> fooHandle = SystemAPI.GetComponentTypeHandle<Foo>();
            ComponentTypeHandle<Bar> barHandle = SystemAPI.GetComponentTypeHandle<Bar>();
            EntityTypeHandle entityHandle = SystemAPI.GetEntityTypeHandle();

            // 获取 component 值和 entity ID 的数组副本：
            {
                // 请记住，临时分配不需要手动处置。

                // 获取所有 Apple component 值的数组
                // entities 与 query 匹配。
                // 该数组是 chunks 中存储的数据的“副本”。
                NativeArray<Apple> apples = myQuery.ToComponentDataArray<Apple>(Allocator.Temp);

                // 获取与 query 匹配的所有 entities 的 ID 的数组。
                // 该数组是 chunks 中存储的数据的“副本”。
                NativeArray<Entity> entities = myQuery.ToEntityArray(Allocator.Temp);
            }

            // 获取与 query 匹配的 chunks 并访问 chunk 数据：
            {
                // 获取与 query 匹配的所有 chunks 的数组。
                NativeArray<ArchetypeChunk> chunks = myQuery.ToArchetypeChunkArray(Allocator.Temp);

                // 循环遍历所有与 query 匹配的 chunks。
                for (int i = 0, chunkCount = chunks.Length; i < chunkCount; i++)
                {
                    ArchetypeChunk chunk = chunks[i];

                    // `GetNativeArray` 返回的数组是完全相同的数组
                    // 存储在 chunk 中，因此您不应尝试丢弃它们。
                    NativeArray<Foo> foos = chunk.GetNativeArray(ref fooHandle);
                    NativeArray<Bar> bars = chunk.GetNativeArray(ref barHandle);

                    // 与 component 值不同，entity ID 不应该是
                    // 已修改，因此 entity ID 的数组始终是只读的。
                    NativeArray<Entity> entities = chunk.GetNativeArray(entityHandle);

                    // 循环 chunk 中的所有 entities。
                    for (int j = 0, entityCount = chunk.Count; j < entityCount; j++)
                    {
                        // 获取个体 entity 的 entity ID 和 Foo component。
                        Entity entity = entities[j];
                        Foo foo = foos[j];
                        Bar bar = bars[j];

                        // 设置 Foo 值。
                        foos[j] = new Foo { };
                    }
                }
            }

            // SystemAPI.Query：
            {
                // SystemAPI.Query 提供了更方便的循环方式
                // 通过 entities 匹配 query。源码生成
                // 将这个 foreach 翻译成等价的功能
                // 上一节的内容。了解 SystemAPI.Query
                // 是否应该将 ONLY 称为 foreach 的“in”子句。

                // 每次迭代处理一个与 query 匹配的 entity
                // 包括 Foo、Bar、Apple，不包括 Banana：
                // - 'foo' 被分配了对 Foo component 的读写引用
                // - 'bar' 被分配了对 Bar component 的只读引用
                // -“entity”被分配为 entity ID
                foreach (var (foo, bar, entity) in
                         SystemAPI.Query<RefRW<Foo>, RefRO<Bar>>()
                             .WithAll<Apple>()
                             .WithNone<Banana>()
                             .WithEntityAccess())
                {
                    foo.ValueRW = new Foo { };
                }
            }
        }
    }
}

namespace ExampleCode.EntityCommandBufferSystems
{
    // 定义一个新的 EntityCommandBufferSystem 将更新
    // 在 InitializationSystemGroup 之前的 FooSystem 中。
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(FooSystem))]
    public partial class MyEntityCommandBufferSystem : EntityCommandBufferSystem
    {
        // 通常没有任何理由重写 `EntityCommandBufferSystem` 的方法，
        // 所以空类是常态。其实需要定义自己的 `EntityCommandBufferSystem`
        // 首先是不常见的，因为默认的 world 已经包含
        // 几个，例如 `BeginSimulationEntityCommandBufferSystem`。
    }

    public partial struct FooSystem : ISystem
    {
    }
}

namespace ExampleCode.DynamicBuffers
{
    // 定义一个 DynamicBuffer<Waypoint> component 类型，
    // 这是一个可增长的 Waypoint 元素数组。
    // InternalBufferCapacity 是每个元素的数量
    // entity 直接存储在 chunk 中（默认为 8）。
    [InternalBufferCapacity(20)]
    public struct Waypoint : IBufferElementData
    {
        public float3 Value;
    }

    // 在 system 中创建和访问 DynamicBuffer 的示例。
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity entity = state.EntityManager.CreateEntity();

            // 将 Waypoint component 类型添加到 entity
            // 并返回新的缓冲区。
            DynamicBuffer<Waypoint> waypoints = state.EntityManager.AddBuffer<Waypoint>(entity);

            // 将长度设置为大于其当前容量的值可调整缓冲区的大小。
            waypoints.Length = 100;

            // 循环遍历缓冲区以设置其值。
            for (int i = 0; i < waypoints.Length; i++)
            {
                waypoints[i] = new Waypoint { Value = new float3() };
            }

            // DynamicBuffers 因结构变更操作而失效
            {
                // 尽管这种结构变化没有触及“航点”或其 entity，
                // 此操作会使“航点”和所有其他 DynamicBuffers 无效
                state.EntityManager.CreateEntity();

#if true
                // 由于“waypoints”已失效，任何读取或写入
                // 其内容引发安全检查异常。
                var w = waypoints[0]; // 例外！
#else
                // 重新获取 DynamicBuffer 实例。
                waypoints = state.EntityManager.GetBuffer<Waypoint>(entity);
                var w = waypoints[0]; // OK
#endif
            }

            // DynamicBuffers 的 EntityCommandBuffer 方法
            {
                EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

                // 记录用于从 entity 中删除 MyElement 动态缓冲区的命令。
                ecb.RemoveComponent<Waypoint>(entity);

                // 记录将 MyElement 动态缓冲区添加到现有 entity 的命令。
                // 返回的 DynamicBuffer 的数据存储在 EntityCommandBuffer 中，
                // 因此对返回缓冲区的更改也被记录为更改。
                DynamicBuffer<Waypoint> myBuff = ecb.AddBuffer<Waypoint>(entity);

                // 播放后，entity 将有一个 MyElement 缓冲区，其中
                // 长度 20 和这些记录值。
                myBuff.Length = 20;
                myBuff[0] = new Waypoint { Value = new float3() };
                myBuff[3] = new Waypoint { Value = new float3() };

                // SetBuffer 类似于 AddBuffer，但安全检查将在播放时抛出异常，如果
                // entity 尚未具有 MyElement 缓冲区。
                DynamicBuffer<Waypoint> otherBuf = ecb.SetBuffer<Waypoint>(entity);

                // 记录将附加到缓冲区的航点值。安全检查投掷
                // 如果 entity 尚无 MyElement 缓冲区，则播放时出现异常。
                ecb.AppendToBuffer<Waypoint>(entity, new Waypoint { Value = new float3() });

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }

            // 重新解释 DynamicBuffer
            {
                DynamicBuffer<Waypoint> myBuff = state.EntityManager.GetBuffer<Waypoint>(entity);

                // 有效，因为每个 float3 和每个 Waypoint 结构的大小都是 12 字节。
                DynamicBuffer<float3> floatsBuffer = myBuff.Reinterpret<float3>();

                // 'floatsBuffer' 和 'myBuff' 代表相同的内容，因此这两个赋值具有相同的效果
#if true
                floatsBuffer[2] = new float3(1, 2, 3);
#else
                myBuff[2] = new Waypoint { Value = new float3(1, 2, 3) };
#endif
            }
        }
    }
}

namespace ExampleCode.EnableableComponents
{
    // 可启用的 component 类型示例。
    // 实现 IComponentData 或 IBufferElementData 的结构
    // 也可以实现 IEnableableComponent。
    // `EntityManager`、`ComponentLookup<T>` 和 `ArchetypeChunk` 都有方法
    //用于检查和设置 components 的使能状态。
    public struct Health : IComponentData, IEnableableComponent
    {
        public float Value;
    }

    // system 演示了可启用的 component 类型的使用。
    [BurstCompile]
    public partial struct MySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 检查并设置启用状态。
            {
                EntityManager em = state.EntityManager;

                // 创建单个 entity 并添加运行状况 component。
                Entity myEntity = em.CreateEntity();
                em.AddComponent<Health>(myEntity);

                // Components 开始启用生命周期，因此返回 true。
                bool b = em.IsComponentEnabled<Health>(myEntity);

                // 禁用 myEntity 的健康状况 component
                em.SetComponentEnabled<Health>(myEntity, false);

                ComponentLookup<Health> healthLookup = SystemAPI.GetComponentLookup<Health>();

                // 尽管禁用，component 仍然可以读取和修改。
                Health h = healthLookup[myEntity];

                // 我们还可以通过 ComponentLookup 来检查和设置启用状态。
                b = healthLookup.IsComponentEnabled(myEntity);
                healthLookup.SetComponentEnabled(myEntity, false);
            }

            EntityQuery myQuery = SystemAPI.QueryBuilder().WithAll<Health>().Build();

            // Query 方法。
            {
                // 禁用的 entities 将会在结果中包含 NOT。
                var entities = myQuery.ToEntityArray(Allocator.Temp);
                var healths = myQuery.ToComponentDataArray<Health>(Allocator.Temp);

                EntityQuery myQueryIgnoreEnabled = SystemAPI.QueryBuilder().WithAll<Health>()
                    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();

                // 禁用的 entities WILL 将包含在结果中。
                entities = myQueryIgnoreEnabled.ToEntityArray(Allocator.Temp);
                healths = myQueryIgnoreEnabled.ToComponentDataArray<Health>(Allocator.Temp);
            }

            // 检查并设置 chunk 中每个 entity 的启用状态。
            {
                ComponentTypeHandle<Health> healthHandle = SystemAPI.GetComponentTypeHandle<Health>();

                var chunks = myQuery.ToArchetypeChunkArray(Allocator.Temp);
                for (int i = 0; i < chunks.Length; i++)
                {
                    var chunk = chunks[i];

                    // 循环遍历 chunk 的 entities。
                    for (int entityIdx = 0, entityCount = chunk.Count; entityIdx < entityCount; entityIdx++)
                    {
                        // 读取启用状态
                        // entity 的健康状况 component。
                        bool enabled = chunk.IsComponentEnabled(ref healthHandle, entityIdx);

                        // 禁用 entity 的运行状况 component。
                        chunk.SetComponentEnabled(ref healthHandle, entityIdx, false);
                    }
                }
            }
        }
    }
}

namespace ExampleCode.Aspects
{
    // 包装 Foo component 的示例方面
    // 以及 Bar component 的启用状态。
    public readonly partial struct MyAspect : IAspect
    {
        // 该方面包括 entity ID。
        // 因为它是只读值类型，
        // 将该领域公开并没有危险。
        public readonly Entity Entity;

        // 该方面包括 Foo component，
        // 具有读写权限。
        readonly RefRW<Foo> foo;

        // 获取和设置 Foo component 的属性。
        public float3 Foo
        {
            get => foo.ValueRO.Value;
            set => foo.ValueRW.Value = value;
        }

        // 该方面包括启用状态
        // 酒吧 component。
        public readonly EnabledRefRW<Bar> BarEnabled;
    }

    public struct Foo : IComponentData
    {
        public float3 Value;
    }

    public struct Bar : IComponentData, IEnableableComponent
    {
        public float Value;
    }

    /*
     * 这些方法返回一个方面的实例：

        - `SystemAPI.GetAspectRW<T>(Entity)`
        - `SystemAPI.GetAspectRO<T>(Entity)`
        - `EntityManager.GetAspect<T>(Entity)`
        - `EntityManager.GetAspectRO<T>(Entity)`

        如果传递的 `Entity` 没有包含在方面 `T` 中的所有 components，则这些方法将抛出。

        如果您使用任何尝试修改底层 components 的方法或属性，则由 `GetAspectRO()` 返回的 Aspects 将抛出异常。

        您还可以通过将方面实例包含为 `IJobEntity` 的 `Execute` 方法的参数或 `SystemAPI.Query` 的类型参数来获取方面实例。
     */
}
#endif
