using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;

namespace ContentManagement.Sample
{
#if UNITY_EDITOR
    public class WeakSceneAuthoring : MonoBehaviour
    {
        public WeakSceneListScriptableObject Settings;
        public UnityEditor.SceneAsset HighFidelityScene;
        // 我们在这里使用 WeakObjectSceneReference 而不是 UntypedWeakReferenceId 因为
        // UntypedWeakReferenceId 不将 scene 作为检查器中的输入。
        // 或者，我们可以使用 SceneAsset，然后使用以下代码在 baker 中创建 WeakObjectSceneReference：
        // new WeakObjectSceneReference { Id = UntypedWeakReferenceId.CreateFromObjectInstance(authoring.LowFidelityScene) };
        public WeakObjectSceneReference LowFidelityScene;

        class Baker : Baker<WeakSceneAuthoring>
        {
            public override void Bake(WeakSceneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new HighLowWeakScene
                {
                    HighSceneRef = UntypedWeakReferenceId.CreateFromObjectInstance(authoring.HighFidelityScene),
                    LowSceneRef = authoring.LowFidelityScene,
                });

                if ((authoring.Settings.ContentSource & ContentSourcePath.Local) != 0)
                    AddComponent<LocalContent>(entity);

                if ((authoring.Settings.ContentSource & ContentSourcePath.Remote) != 0)
                    AddComponent(entity, new RemoteContent { URL = authoring.Settings.RemoteURL });
            }
        }

    }
#endif

    public struct HighLowWeakScene : IComponentData
    {
        public UntypedWeakReferenceId HighSceneRef;
        public WeakObjectSceneReference LowSceneRef;

        public bool IsHighLoaded;   // 指示当前加载的是高保真还是低保真
        public Entity LoadedScene;  // 存储对当前加载的 scene 的引用（如果当前没有加载 scene，则为 null）
    }

    public struct ContentIsReady : IComponentData
    {
    }

    public struct RemoteContent : IComponentData
    {
        public FixedString512Bytes URL;
    }

    public struct LocalContent : IComponentData
    {
    }
}