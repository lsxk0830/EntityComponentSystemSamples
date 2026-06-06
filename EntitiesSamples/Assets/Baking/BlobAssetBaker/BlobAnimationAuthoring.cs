using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Baking.BlobAssetBaker
{
    public class BlobAnimationAuthoring : MonoBehaviour
    {
        // 我们将把这条动画曲线烘焙成一个斑点。
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        class Baker : Baker<BlobAnimationAuthoring>
        {
            public override void Bake(BlobAnimationAuthoring authoring)
            {
                // TransformUsageFlags.Dynamic 给出 entity LocalTransform 和 LocalToWorld components。
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var blobReference = CreateBlob(authoring.Curve, Allocator.Persistent);

                // BlobAsset 的所有权传递给 BlobAssetStore，
                // 它将自动管理 BlobAsset 的生命周期和重复数据删除。
                AddBlobAsset<AnimationBlobData>(ref blobReference, out _);

                AddComponent(entity, new Animation { AnimBlobReference = blobReference });
            }

            BlobAssetReference<AnimationBlobData> CreateBlob(AnimationCurve curve, Allocator allocator,
                Allocator builderAllocator = Allocator.TempJob)
            {
                // 确保在创建 blob 资产后立即处置生成器。
                using (var blobBuilder = new BlobBuilder(builderAllocator))
                {
                    // Blob 资源是从根结构（本例中为 AnimationBlobData）开始构建的。
                    ref var root = ref blobBuilder.ConstructRoot<AnimationBlobData>();
                    int keyCount = 12;

                    float endTime = curve[curve.length - 1].time;
                    root.InvLength = 1.0F / endTime;
                    root.KeyCount = keyCount;

                    // 构建 Keys 数组。
                    var array = blobBuilder.Allocate(ref root.Keys, keyCount + 1);
                    for (int i = 0; i < keyCount; i++)
                    {
                        float time = (float)i / (float)(keyCount - 1) * endTime;
                        array[i] = curve.Evaluate(time);
                    }

                    array[keyCount] = array[keyCount - 1];

                    // 将构建器数据复制到最终的 Blob 资产表单中。
                    return blobBuilder.CreateBlobAssetReference<AnimationBlobData>(allocator);
                }
            }
        }
    }

    public struct Animation : IComponentData
    {
        public BlobAssetReference<AnimationBlobData> AnimBlobReference;
        public float Time;
    }

    // 我们 blob 的根结构。
    // 一个非常简单的动画曲线，使用固定间隔的线性 interpolation。
    public struct AnimationBlobData
    {
        public BlobArray<float> Keys;
        public float InvLength;
        public float KeyCount;
    }
}
