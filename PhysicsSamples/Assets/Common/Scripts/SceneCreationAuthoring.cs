using UnityEngine;

// authoring components 的基类，使用 SceneCreationSystem 从代码创建 scene
public abstract class SceneCreationAuthoring<T> : MonoBehaviour
    where T : SceneCreationSettings, new()
{
    public Material DynamicMaterial;
    public Material StaticMaterial;
}
