using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// 用于存储胶囊形状的 authoring 数据的结构。相比之下
    /// CapsuleGeometry 结构中的 run 时间，该结构允许存储稳定的方向
    /// 值，以及当源数据定义时可以保留的高度值
    /// 相对于非均匀缩放的对象。
    /// </summary>
    [Serializable]
    public struct CapsuleGeometryAuthoring : IEquatable<CapsuleGeometryAuthoring>
    {
        /// <summary>
        /// 胶囊的局部方向。当它是时，它与前轴（z）对齐
        /// 身份。
        /// </summary>
        public quaternion Orientation { get => m_OrientationEuler; set => m_OrientationEuler.SetValue(value); }
        internal EulerAngles OrientationEuler { get => m_OrientationEuler; set => m_OrientationEuler = value; }
        [SerializeField]
        EulerAngles m_OrientationEuler;

        /// <summary> 胶囊的局部位置偏移。</summary>
        public float3 Center { get => m_Center; set => m_Center = value; }
        [SerializeField]
        float3 m_Center;

        /// <summary>
        /// 胶囊的高度。它可以存储任何值，但最终总会被转换
        /// 为至少两倍半径的值。
        /// </summary>
        public float Height { get => m_Height; set => m_Height = value; }
        [SerializeField]
        float m_Height;

        /// <summary> 胶囊的半径。</summary>
        ///
        /// <value> 半径。</value>
        public float Radius { get => m_Radius; set => m_Radius = value; }
        [SerializeField]
        float m_Radius;

        public bool Equals(CapsuleGeometryAuthoring other)
        {
            return m_Height.Equals(other.m_Height)
                && m_Center.Equals(other.m_Center)
                && m_Radius.Equals(other.m_Radius)
                && m_OrientationEuler.Equals(other.m_OrientationEuler);
        }

        public override int GetHashCode()
        {
            return unchecked((int)math.hash(
                new float3x3(
                    Center,
                    m_OrientationEuler.Value,
                    new float3((float)m_OrientationEuler.RotationOrder, m_Height, m_Radius)
                )
            ));
        }
    }

    public static class CapsuleGeometryAuthoringExtensions
    {
        /// <summary>
        /// 从 run 时间 CapsuleGeometry 实例构造 CapsuleGeometryAuthoring 实例。
        /// </summary>
        public static CapsuleGeometryAuthoring ToAuthoring(this CapsuleGeometry input)
        {
            var orientationEuler = EulerAngles.Default;
            orientationEuler.SetValue(quaternion.LookRotationSafe(input.Vertex0 - input.Vertex1, new float3 { z = 1f }));
            return new CapsuleGeometryAuthoring
            {
                Height = input.GetHeight(),
                OrientationEuler = orientationEuler,
                Center = input.GetCenter(),
                Radius = input.Radius
            };
        }

        /// <summary>
        /// 从 CapsuleGeometryAuthoring 实例构造 run 时间 CapsuleGeometry 实例。
        /// </summary>
        public static CapsuleGeometry ToRuntime(this CapsuleGeometryAuthoring input)
        {
            var halfHeight   = 0.5f * input.Height;
            var halfDistance = halfHeight - input.Radius;
            var axis         = math.normalize(math.mul(input.Orientation, new float3 { z = 1f }));
            var halfAxis     = axis * halfDistance;
            var vertex0      = input.Center + halfAxis;
            var vertex1      = input.Center - halfAxis;
            return new CapsuleGeometry
            {
                Vertex0 = vertex0,
                Vertex1 = vertex1,
                Radius  = input.Radius
            };
        }
    }
}
