#if UNITY_EDITOR

using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Boids
{
    // 演示获取编辑器中有关 GameObjects 的一些可用数据并保存在
    // 适合 Component system 更新的运行时格式。
    // - 播放第一个附加的动画剪辑（仅期望一个）
    // - 以指定速率记录位置和旋转
    // - 将示例存储到 DynamicBuffer 中
    public class TransformRecorderAuthoring : MonoBehaviour
    {
        [Range(2, 120)] public int SamplesPerSecond = 60;

        class Baker : Baker<TransformRecorderAuthoring>
        {
            public override void Bake(TransformRecorderAuthoring authoring)
            {
                var animationClips = AnimationUtility.GetAnimationClips(authoring.gameObject);
                var animationClip = animationClips[0];
                var lengthSeconds = animationClip.length;
                var sampleRate = 1.0f / authoring.SamplesPerSecond;
                var frameCount = (int)(lengthSeconds / sampleRate);
                if (frameCount < 2) // 至少要捕获两帧动画。
                {
                    return;
                }

                var s = 0.0f;

                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var transformSamplesBlob = ref blobBuilder.ConstructRoot<TransformSamples>();
                var translationSamples = blobBuilder.Allocate(ref transformSamplesBlob.TranslationSamples, frameCount);
                var rotationSamples = blobBuilder.Allocate(ref transformSamplesBlob.RotationSamples, frameCount);

                for (int i = 0; i < frameCount; i++)
                {
                    animationClip.SampleAnimation(authoring.gameObject, s);

                    translationSamples[i] = authoring.gameObject.transform.position;
                    rotationSamples[i] = authoring.gameObject.transform.rotation;

                    s += sampleRate;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SampledAnimationClip
                {
                    FrameCount = frameCount,
                    SampleRate = sampleRate,
                    CurrentTime = 0.0f,
                    FrameIndex = 0,
                    TimeOffset = 0,
                    TransformSamplesBlob = blobBuilder.CreateBlobAssetReference<TransformSamples>(Allocator.Persistent)
                });

                blobBuilder.Dispose();
            }
        }
    }
}

#endif
