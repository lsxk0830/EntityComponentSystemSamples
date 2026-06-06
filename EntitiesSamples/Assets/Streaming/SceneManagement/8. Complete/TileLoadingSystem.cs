using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;

namespace Streaming.SceneManagement.CompleteSample
{
    // 此 system 将根据与相关 entities 的图块距离加载/卸载图块 scenes
    [UpdateAfter(typeof(TileDistanceSystem))]
    partial struct TileLoadingSystem : ISystem
    {
        ComponentTypeSet loadComponentTypeSet;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            loadComponentTypeSet = new ComponentTypeSet(ComponentType.ReadWrite<RequiresPostLoadCommandBuffer>(),
                ComponentType.ReadWrite<TileEntity>(),
                ComponentType.ReadWrite<LoadSection0>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sectionQuery = SystemAPI.QueryBuilder().WithAll<TileInfo, DistanceToRelevant>().Build();

            var tileEntities = sectionQuery.ToEntityArray(Allocator.Temp);
            var tileInfos = sectionQuery.ToComponentDataArray<TileInfo>(Allocator.Temp);
            var tileDistances = sectionQuery.ToComponentDataArray<DistanceToRelevant>(Allocator.Temp).Reinterpret<float>();

            NativeList<LoadableTile> priorityLoadList = new NativeList<LoadableTile>(tileEntities.Length, Allocator.Temp);

            // 根据到相关 entities 的距离找到所有应该装载/卸载的 scenes
            for (int index = 0; index < tileInfos.Length; ++index)
            {
                if (tileDistances[index] < tileInfos[index].LoadingDistanceSq)
                {
                    priorityLoadList.Add(new LoadableTile
                    {
                        TileIndex = index,
                        DistanceSq = tileDistances[index]
                    });
                }
                else if (tileDistances[index] > tileInfos[index].UnloadingDistanceSq)
                {
                    // 检查之前是否已经加载过 tile
                    if (state.EntityManager.HasComponent<SubsceneEntity>(tileEntities[index]))
                    {
                        // 我们卸载 scene
                        var complexSceneSubsceneEntity =
                            state.EntityManager.GetComponentData<SubsceneEntity>(tileEntities[index]);
                        SceneSystem.UnloadScene(state.WorldUnmanaged, complexSceneSubsceneEntity.Value, SceneSystem.UnloadParameters.DestroyMetaEntities);
                        state.EntityManager.RemoveComponent<SubsceneEntity>(tileEntities[index]);
                    }
                }
            }

            // 优先加载最接近相关 entities 的部分
            priorityLoadList.Sort(new SectionDistanceComparer());

            // 加载
            int loading = 0;
            int maxToLoad = 4; // 限制我们一次加载的数量
            foreach (var loadEntry in priorityLoadList)
            {
                int tileIndex = loadEntry.TileIndex;
                if (!state.EntityManager.HasComponent<SubsceneEntity>(tileEntities[tileIndex]))
                {
                    // 将 Scene 加载为新实例并禁用各部分的自动加载
                    var sceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged,
                        tileInfos[tileIndex].Scene, new SceneSystem.LoadParameters()
                        {
                            Flags = SceneLoadFlags.NewInstance | SceneLoadFlags.DisableAutoLoad
                        });

                    float3 center = new float3(tileInfos[tileIndex].Position.x, 0f, tileInfos[tileIndex].Position.y);

                    state.EntityManager.AddComponent(sceneEntity, loadComponentTypeSet);

                    // 推迟添加 PostLoadCommandBuffer，以便大部分加载代码可以被突发
                    state.EntityManager.SetComponentData(sceneEntity, new RequiresPostLoadCommandBuffer
                    {
                        Position = center,
                        Rotation = tileInfos[tileIndex].Rotation
                    });

                    // 我们还存储图块的中心，以便在部分加载期间可以对其进行距离检查
                    state.EntityManager.SetComponentData(sceneEntity, new TileEntity
                    {
                        Value = tileEntities[tileIndex]
                    });

                    // 我们存储 scene entity 以便稍后处理卸载
                    state.EntityManager.AddComponentData(tileEntities[tileIndex], new SubsceneEntity
                    {
                        Value = sceneEntity
                    });

                    // 增加加载数量，因此我们只能同时加载有限数量的 scenes
                    ++loading;
                }
                else
                {
                    // 我们需要检查当前图块是否正在加载
                    var subSceneEntityComponent = state.EntityManager.GetComponentData<SubsceneEntity>(tileEntities[tileIndex]);
                    var streamingState = SceneSystem.GetSceneStreamingState(state.WorldUnmanaged, subSceneEntityComponent.Value);
                    if (streamingState != SceneSystem.SceneStreamingState.LoadedSuccessfully && streamingState != SceneSystem.SceneStreamingState.LoadedSectionEntities)
                    {
                        // 增加加载数量，因此我们只能同时加载有限数量的 scenes
                        ++loading;
                    }
                }

                if (loading >= maxToLoad)
                {
                    break;
                }
            }
        }
    }
}
