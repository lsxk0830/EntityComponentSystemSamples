using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace Unity.Physics.Extensions
{
    /// <summary>
    /// 作用于物理 components 的效用函数。
    /// </summary>
    public static class PhysicsSamplesExtensions
    {
        #region CompoundCollider Utilities
        /// <summary>
        /// 给定层次结构的根 Collider 和引用该层次结构中的子级的 ColliderKey，
        /// 该函数返回引用父级 Collider 的 ColliderKey。
        /// </summary>
        /// <param name="rootColliderPtr">A <see cref="Collider"/> 位于 Collider hierarchy.</param> 的根部
        /// <param name="childColliderKey">A <see cref="ColliderKey"/> 引用 Collider 层次结构中低于 rootColliderPtr.</param> 的子 Collider
        /// <param name="parentColliderKey">A <see cref="ColliderKey"/> 引用子 Collider 的父级。如果参数为 invalid.</param>，则为 ColliderKey.Empty
        /// <returns>Whether 已成功在 hierarchy.</returns> 中找到父级
        public static unsafe bool TryGetParentColliderKey(Collider* rootColliderPtr, ColliderKey childColliderKey, out ColliderKey parentColliderKey)
        {
            var childColliderPtr = rootColliderPtr;
            var childColliderKeyNumBits = childColliderPtr->NumColliderKeyBits;

            // 从空的 collider 键开始，并在我们向下遍历复合层次结构时将子键推到它上面。
            var parentColliderKeyPath = ColliderKeyPath.Empty;

            // 在下降过程中，childColliderKey 弹出子键并将它们推送到 parentColliderKeyPath
            do
            {
                childColliderKey.PopSubKey(childColliderKeyNumBits, out var childIndex);
                switch (childColliderPtr->Type)
                {
                    case ColliderType.Compound:
                        // 让下一个孩子下来并再次循环
                        parentColliderKeyPath.PushChildKey(new ColliderKeyPath(new ColliderKey(childColliderKeyNumBits, childIndex), childColliderKeyNumBits));
                        childColliderPtr = ((CompoundCollider*)childColliderPtr)->Children[(int)childIndex].Collider;
                        childColliderKeyNumBits = childColliderPtr->NumColliderKeyBits;
                        break;
                    case ColliderType.Mesh:
                    case ColliderType.Terrain:
                        // 我们已经击中了地形或网格 collider，因此下面应该只有 PolygonColliders。
                        // 此时，childColliderKey 应为空，childIndex 应为多边形的索引。
                        if (!childColliderKey.Equals(ColliderKey.Empty))
                        {
                            // 我们已经到达底部，但没有弹出所有子键。
                            // 给定的 childColliderKey 不适合此层次结构！
                            parentColliderKey = ColliderKey.Empty;
                            return false;
                        }
                        break;
                    default:
                        // 我们已经击中了凸 collider，所以 rootColliderPtr 一定不是
                        // 首先是层次结构的根，因此没有父级！
                        parentColliderKey = ColliderKey.Empty;
                        return false;
                }
            }
            while (!childColliderKey.Equals(ColliderKey.Empty)
                   && !childColliderPtr->CollisionType.Equals(CollisionType.Convex));

            parentColliderKey = parentColliderKeyPath.Key;
            // 此时 childColliderKey 应为空。
            // 然而，如果不是，那么我们到达一片叶子时却找不到孩子 collider！
            return childColliderKey.Equals(ColliderKey.Empty);
        }

        /// <summary>
        /// 给定层次结构的根 Collider 和引用该层次结构中的子级的 ColliderKey，
        /// 该函数返回请求的 ChildCollider。
        /// </summary>
        /// <param name="rootColliderPtr">A <see cref="Collider"/> 位于 Collider hierarchy.</param> 的根部
        /// <param name="childColliderKey">A <see cref="ColliderKey"/> 引用 Collider 层次结构中低于 rootColliderPtr.</param> 的子 Collider
        /// <param name="childCollider">A 从层次结构返回有效的 <see cref="ChildCollider"/>，如果 found.</param>
        /// <returns>Whether 在 hierarchy.</returns>中成功找到指定的 ColliderKey
        public static unsafe bool TryGetChildInHierarchy(Collider* rootColliderPtr, ColliderKey childColliderKey, out ChildCollider childCollider)
        {
            //public static unsafe bool GetLeafCollider(Collider* root, RigidTransform rootTransform, ColliderKey key, out ChildCollider leaf)
            childCollider = new ChildCollider(rootColliderPtr, RigidTransform.identity);
            while (!childColliderKey.Equals(ColliderKey.Empty))
            {
                if (!childCollider.Collider->GetChild(ref childColliderKey, out ChildCollider child))
                {
                    break;
                }
                childCollider = new ChildCollider(childCollider, child);
            }
            return (childCollider.Collider != null);
        }

        /// <summary>
        /// 根据提供的 collider 密钥 entity 对设置 CompoundCollider 中的 Entity 引用。
        /// </summary>
        /// <param name="compoundColliderPtr">A <see cref="CompoundCollider"/>.</param>
        /// <param name="keyEntityPairs">An <see cref="ColliderKey"/> 和 <see cref="Entity"/> pairs.</param> 的数组
        public static unsafe void RemapColliderEntityReferences(
            CompoundCollider* compoundColliderPtr, in NativeArray<PhysicsColliderKeyEntityPair> keyEntityPairs) =>
            RemapCompoundColliderEntityReferences(compoundColliderPtr, keyEntityPairs, ColliderKey.Empty);

        internal static unsafe void RemapCompoundColliderEntityReferences(
            CompoundCollider* compoundColliderPtr,
            in NativeArray<PhysicsColliderKeyEntityPair> keyEntityPairs,
            in ColliderKey key)
        {
            for (int childIndex = 0; childIndex < compoundColliderPtr->Children.Length; childIndex++)
            {
                ref CompoundCollider.Child child = ref compoundColliderPtr->Children[childIndex];
                var childKey = key; childKey.PushSubKey(compoundColliderPtr->NumColliderKeyBits, (uint)childIndex);
                for (int i = 0; i < keyEntityPairs.Length; i++)
                {
                    if (childKey.Equals(keyEntityPairs[i].Key))
                    {
                        child.Entity = keyEntityPairs[i].Entity;
                        break;
                    }
                }
                if (child.Collider->Type == ColliderType.Compound)
                {
                    RemapCompoundColliderEntityReferences((CompoundCollider*)child.Collider, keyEntityPairs, childKey);
                }
            }
        }

        #endregion
    }
}
