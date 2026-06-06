using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Physics.Authoring
{
    public static class ConvexHullGenerationParametersExtensions
    {
        // 建议简化公差至少 1 厘米
        internal const float k_MinRecommendedSimplificationTolerance = 0.01f;

        internal static void InitializeToRecommendedAuthoringValues(
            ref this ConvexHullGenerationParameters generationParameters, NativeArray<float3> points
        )
        {
            generationParameters = ConvexHullGenerationParameters.Default.ToAuthoring();

            if (points.Length <= 1)
                return;

            var bounds = new Aabb { Min = points[0], Max = points[0] };
            for (var i = 1; i < points.Length; ++i)
                bounds.Include(points[i]);
            generationParameters.SimplificationTolerance = math.max(
                k_MinRecommendedSimplificationTolerance,
                ConvexHullGenerationParameters.Default.SimplificationTolerance * math.cmax(bounds.Extents)
            );
            // TODO: 根据输入点初始化其他属性？
        }

        internal static void OnValidate(ref this ConvexHullGenerationParameters generationParameters, float maxAngle = 180f)
        {
            generationParameters.SimplificationTolerance = math.max(0f, generationParameters.SimplificationTolerance);
            generationParameters.BevelRadius = math.max(0f, generationParameters.BevelRadius);
            generationParameters.MinimumAngle = math.clamp(generationParameters.MinimumAngle, 0f, maxAngle);
        }

        public static ConvexHullGenerationParameters ToAuthoring(this ConvexHullGenerationParameters generationParameters)
        {
            generationParameters.MinimumAngle = math.degrees(generationParameters.MinimumAngle);
            return generationParameters;
        }

        public static ConvexHullGenerationParameters ToRunTime(this ConvexHullGenerationParameters generationParameters)
        {
            generationParameters.MinimumAngle = math.radians(generationParameters.MinimumAngle);
            return generationParameters;
        }
    }
}
