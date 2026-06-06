using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Extensions;
using Collider = Unity.Physics.Collider;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Query
{
    // 该收集器过滤掉带有透明自定义标签的尸体
    public struct IgnoreTransparentClosestHitCollector : ICollector<RaycastHit>
    {
        public bool EarlyOutOnFirstHit => false;

        public float MaxFraction { get; private set; }

        public int NumHits { get; private set; }

        public RaycastHit ClosestHit;

        private CollisionWorld m_World;
        private const int k_TransparentCustomTag = (1 << 1);

        public IgnoreTransparentClosestHitCollector(CollisionWorld world)
        {
            m_World = world;

            MaxFraction = 1.0f;
            ClosestHit = default;
            NumHits = 0;
        }

        private static bool IsTransparent(BlobAssetReference<Collider> collider, ColliderKey key)
        {
            bool bIsTransparent = false;
            unsafe
            {
                // 仅凸面 Colliders 具有与其关联的材质。所以基于 CollisionType
                // 我们需要从基本 Collider 类型进行转换，因此，我们需要指针。
                var c = collider.AsPtr();
                {
                    var cc = ((ConvexCollider*)c);

                    // 我们还需要检查我们的 Collider 是否是复合的（i.e. 有子项）。
                    // 如果是，那么我们获取射线击中的实际叶节点。
                    // 检查我们的 collider 是否是复合的
                    if (c->CollisionType != CollisionType.Convex)
                    {
                        // 如果是，则将叶子设为凸 Collider
                        c->GetLeaf(key, out ChildCollider child);
                        cc = (ConvexCollider*)child.Collider;
                    }

                    // 现在我们肯定已经有了 ConvexCollider，因此可以检查材质。
                    bIsTransparent = (cc->Material.CustomTags & k_TransparentCustomTag) != 0;
                }
            }

            return bIsTransparent;
        }

        public bool AddHit(RaycastHit hit)
        {
            if (IsTransparent(m_World.Bodies[hit.RigidBodyIndex].Collider, hit.ColliderKey))
            {
                return false;
            }

            MaxFraction = hit.Fraction;
            ClosestHit = hit;
            NumHits = 1;

            return true;
        }
    }
}
