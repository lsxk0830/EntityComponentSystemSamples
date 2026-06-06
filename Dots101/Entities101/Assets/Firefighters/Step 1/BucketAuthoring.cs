using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Tutorials.Firefighters
{
    public class BucketAuthoring : MonoBehaviour
    {
        private class Baker : Baker<BucketAuthoring>
        {
            public override void Bake(BucketAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent<Bucket>(entity);
                AddComponent<URPMaterialPropertyBaseColor>(entity);
            }
        }
    }

    public struct Bucket : IComponentData
    {
        public float Water;  // 0 = 空，1 = 满

        public Entity CarryingBot;
        public bool IsCarried;
    }
}

