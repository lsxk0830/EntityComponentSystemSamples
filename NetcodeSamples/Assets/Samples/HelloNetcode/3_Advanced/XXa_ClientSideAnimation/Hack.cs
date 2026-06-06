#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.Entities;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public class HackComponent : IComponentData
    {
        public AnimationClip[] Clip;
    }

    /// <summary>
    /// 这是使 ClientSideAnimation.unity scene 在这种情况下独立工作的必要技巧
    /// 其中 scene 是唯一添加到构建设置中的。
    ///
    /// 它通过强制引用剪辑来修复 baking 期间的问题
    /// 对于动画状态机的工作是必要的。
    /// </summary>
    public class Hack : MonoBehaviour
    {
        public GameObject Reference;

        class Baker : Baker<Hack>
        {
            public override void Bake(Hack authoring)
            {
                AddComponentObject(GetEntity(TransformUsageFlags.Dynamic), new HackComponent
                {
                    Clip = authoring.Reference.GetComponent<Animator>().runtimeAnimatorController.animationClips,
                });
            }
        }
    }
}
#endif
