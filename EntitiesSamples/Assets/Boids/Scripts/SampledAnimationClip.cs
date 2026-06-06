using Unity.Entities;
using Unity.Mathematics;

namespace Boids
{
    public struct SampledAnimationClip : IComponentData
    {
        public float SampleRate;
        public int FrameCount;

        // 播放状态
        public float CurrentTime;
        public int FrameIndex;
        public float TimeOffset;

        public BlobAssetReference<TransformSamples> TransformSamplesBlob;
    }

    public struct TransformSamples
    {
        public BlobArray<float3> TranslationSamples;
        public BlobArray<quaternion> RotationSamples;
    }
}
