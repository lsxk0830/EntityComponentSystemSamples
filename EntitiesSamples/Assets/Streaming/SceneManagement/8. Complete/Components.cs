using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Streaming.SceneManagement.CompleteSample
{
    struct RequiresPostLoadCommandBuffer : IComponentData
    {
        public float3 Position;
        public float Rotation;
    }

    public struct TileOffset : IComponentData
    {
        public float3 Offset;
        public float Rotation;
    }

    struct SectionDistanceComparer : IComparer<LoadableTile>
    {
        public int Compare(LoadableTile x, LoadableTile y)
        {
            return x.DistanceSq.CompareTo(y.DistanceSq);
        }
    }

    // 运行时信息以及加​​载和卸载该部分的距离范围
    public struct TileLODRange : IComponentData
    {
        public float LowerRadiusSq;
        public float HigherRadiusSq;
    }

    struct LoadableTile
    {
        public int TileIndex;
        public float DistanceSq;
    }

    public struct SubsceneEntity : IComponentData
    {
        public Entity Value;
    }

    public struct TileEntity : IComponentData
    {
        public Entity Value;
    }

    // 用来表示需要加载 Section 0。
    public struct LoadSection0 : IComponentData
    {
    }
}
