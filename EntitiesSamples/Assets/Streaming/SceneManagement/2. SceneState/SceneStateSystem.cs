using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace Streaming.SceneManagement.SceneState
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct SceneStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SceneReference>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sceneQuery = SystemAPI.QueryBuilder().WithAll<SceneReference>().Build();
            var scenes = sceneQuery.ToComponentDataArray<SceneReference>(Allocator.Temp);

            // 我们不能在这里使用 foreach query，因为 SceneSystem 方法添加和删除 components，
            // 这在 foreach query 中是不允许的。
            for (int index = 0; index < scenes.Length; ++index)
            {
                var scene = scenes[index];
                scene.StreamingState = SceneSystem.GetSceneStreamingState(state.WorldUnmanaged, scene.EntityScene);

                // 当用户单击 UI 中的按钮时，将设置 LoadingAction。
                switch (scene.LoadingAction)
                {
                    case LoadingAction.LoadAll:
                    case LoadingAction.LoadMeta:
                    {
                        var loadParam = new SceneSystem.LoadParameters
                        {
                            AutoLoad = (scene.LoadingAction == LoadingAction.LoadAll)
                        };
                        if (scene.EntityScene == default)
                        {
                            scene.EntityScene =
                                SceneSystem.LoadSceneAsync(state.WorldUnmanaged, scene.SceneAsset, loadParam);
                        }
                        else
                        {
                            SceneSystem.LoadSceneAsync(state.WorldUnmanaged, scene.EntityScene, loadParam);
                        }

                        break;
                    }
                    case LoadingAction.UnloadAll:
                    {
                        SceneSystem.UnloadScene(state.WorldUnmanaged, scene.EntityScene,
                            SceneSystem.UnloadParameters.DestroyMetaEntities);
                        scene.EntityScene = default;
                        break;
                    }
                    case LoadingAction.UnloadEntities:
                    {
                        SceneSystem.UnloadScene(state.WorldUnmanaged, scene.EntityScene);
                        break;
                    }
                }

                scene.LoadingAction = LoadingAction.None;
                scenes[index] = scene;
            }

            // 将数组中的值复制回实际的 components。
            sceneQuery.CopyFromComponentDataArray(scenes);
        }
    }
}
