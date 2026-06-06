using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Boids
{
    [RequireMatchingQueriesForUpdate]
    public partial class SampledAnimationClipPlaybackSystem : SystemBase
    {
        partial struct UpdateTRSJob : IJobEntity
        {
            void Execute(ref LocalTransform transform, in SampledAnimationClip sampledAnimationClip)
            {
                var frameIndex = sampledAnimationClip.FrameIndex;
                var timeOffset = sampledAnimationClip.TimeOffset;

                // 请注意，不要将 Value（或 Value 中的任何字段，如 Samples）缓存在 Blob 资源内。
                var prevTranslation = sampledAnimationClip.TransformSamplesBlob.Value.TranslationSamples[frameIndex];
                var nextTranslation = sampledAnimationClip.TransformSamplesBlob.Value.TranslationSamples[frameIndex + 1];
                var prevRotation    = sampledAnimationClip.TransformSamplesBlob.Value.RotationSamples[frameIndex];
                var nextRotation    = sampledAnimationClip.TransformSamplesBlob.Value.RotationSamples[frameIndex + 1];

                transform.Position = math.lerp(prevTranslation, nextTranslation, timeOffset);
                transform.Rotation = math.slerp(prevRotation, nextRotation, timeOffset);
            }
        }

        partial struct UpdateTimeJob : IJobEntity
        {
            public float deltaTime;

            void Execute(ref SampledAnimationClip sampledAnimationClip)
            {
                var currentTime = sampledAnimationClip.CurrentTime + deltaTime;
                var sampleRate = sampledAnimationClip.SampleRate;
                var frameIndex = (int)(currentTime / sampledAnimationClip.SampleRate);
                var timeOffset = (currentTime - (frameIndex * sampleRate)) * (1.0f / sampleRate);

                // 结束时重新启动循环：
                //   - 不要在最后一帧和第一帧之间进行插值。
                //   - 不必担心将时间插入循环的开始。
                //   - 不要太担心最后一帧的确切含义。
                if (frameIndex >= (sampledAnimationClip.FrameCount - 2))
                {
                    currentTime = 0.0f;
                    frameIndex = 0;
                    timeOffset = 0.0f;
                }

                sampledAnimationClip.CurrentTime = currentTime;
                sampledAnimationClip.FrameIndex = frameIndex;
                sampledAnimationClip.TimeOffset = timeOffset;
            }
        }

        protected override void OnUpdate()
        {
            new UpdateTRSJob().ScheduleParallel();

            var deltaTime = math.min(0.05f, SystemAPI.Time.DeltaTime);

            new UpdateTimeJob() { deltaTime = math.min(0.05f, SystemAPI.Time.DeltaTime) }.ScheduleParallel();
        }
    }
}
