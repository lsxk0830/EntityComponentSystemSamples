using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContentManagement.Sample
{
    public partial struct WeakSceneLoadingSystem : ISystem
    {
        private bool init;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContentIsReady>();
            state.RequireForUpdate<HighLowWeakScene>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var weakScene = SystemAPI.GetSingleton<HighLowWeakScene>();

            var loadParams = new SceneSystem.LoadParameters
            {
                AutoLoad = true,
                Flags = SceneLoadFlags.LoadAdditive
            };

            if (!init)
            {
                Debug.Log("Hit Enter to toggle between low and high fidelity");

                // 低保真 scene 的初始负载
                weakScene.LoadedScene = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, weakScene.LowSceneRef.Id.GlobalId.AssetGUID, loadParams);

                SystemAPI.SetSingleton(weakScene);
                init = true;
                return;
            }

            // 仅当用户按 Enter 键时切换 scenes
            if (!Keyboard.current.enterKey.wasPressedThisFrame)
            {
                return;
            }

            // 在两个 scenes 之间切换
            SceneSystem.UnloadScene(state.WorldUnmanaged, weakScene.LoadedScene);  // 卸载电流 scene

            if (weakScene.IsHighLoaded)
            {
                // 加载低保真度
                weakScene.LoadedScene = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, weakScene.LowSceneRef.Id.GlobalId.AssetGUID, loadParams);
                weakScene.IsHighLoaded = false;
            }
            else
            {
                // 加载高保真度
                weakScene.LoadedScene = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, weakScene.HighSceneRef.GlobalId.AssetGUID, loadParams);
                weakScene.IsHighLoaded = true;
            }

            SystemAPI.SetSingleton(weakScene);
        }
    }
}