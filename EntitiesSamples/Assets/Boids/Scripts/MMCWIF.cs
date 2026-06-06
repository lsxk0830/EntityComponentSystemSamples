using UnityEngine;
using Unity.Entities;

#if !UNITY_DISABLE_MANAGED_COMPONENTS
namespace Boids
{
    /*
     * 该文件确保我们保留当前的 ​​typemanager 哈希行为，
     * 也就是说，如果您 #if UNITY_EDITOR 类 IComponentData 本身中的一个字段，
     * 播放器中会有不同的哈希值，然后就会出现错误
     * 当播放器运行时，如果将其放入 subscene 中。
     *
     * 但是，如果您对类 IComponentData 类执行相同的操作
     * 包含作为字段，那会很好，因为我们不查看类类型内部
     * 字段。
     */
    public class MMCWIFAuthoring : MonoBehaviour
    {
        class Baker : Baker<MMCWIFAuthoring>
        {
            public override void Bake(MMCWIFAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new MyManagedComponentWithIfdeffedField());
            }
        }
    }
    public class MemberThingWithIfdeffedField
    {
#if UNITY_EDITOR
        public int x;
#endif
    }


    public class MyManagedComponentWithIfdeffedField : IComponentData
    {
        MemberThingWithIfdeffedField x;
    }
}
#endif
