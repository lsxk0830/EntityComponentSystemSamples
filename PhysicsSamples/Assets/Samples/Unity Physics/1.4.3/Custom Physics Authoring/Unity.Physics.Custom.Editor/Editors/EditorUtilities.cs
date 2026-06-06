using UnityEngine;
using Unity.Mathematics;
using static Unity.Physics.Math;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Unity.Physics.Editor
{
    /// <summary>
    /// 提供使用句柄设置位置和轴的实用程序，
    /// </summary>
    public class EditorUtilities
    {
        // Editor 用于 joint 枢轴或枢轴对
        public static void EditPivot(RigidTransform worldFromA, RigidTransform worldFromB, bool lockBtoA,
            ref float3 pivotA, ref float3 pivotB, Object target)
        {
            EditorGUI.BeginChangeCheck();
            float3 pivotAinW = Handles.PositionHandle(math.transform(worldFromA, pivotA), quaternion.identity);
            float3 pivotBinW;

            if (lockBtoA)
            {
                pivotBinW = pivotAinW;
                pivotB = math.transform(math.inverse(worldFromB), pivotBinW);
            }
            else
            {
                pivotBinW = Handles.PositionHandle(math.transform(worldFromB, pivotB), quaternion.identity);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Edit joint pivot");
                pivotA = math.transform(math.inverse(worldFromA), pivotAinW);
                pivotB = math.transform(math.inverse(worldFromB), pivotBinW);
            }

            Handles.DrawLine(worldFromA.pos, pivotAinW);
            Handles.DrawLine(worldFromB.pos, pivotBinW);
        }

        // Editor 用于 joint 轴或轴对
        public class AxisEditor
        {
            // 即使我们只编辑轴而不是旋转，我们也需要跟踪完整的旋转以保持旋转手柄稳定
            private quaternion m_RefA = quaternion.identity;
            private quaternion m_RefB = quaternion.identity;

            // 检测正在编辑的对象的变化以重置参考方向
            private Object m_LastTarget;

            private static bool NormalizeSafe(ref float3 x)
            {
                float lengthSq = math.lengthsq(x);
                const float epsSq = 1e-8f;
                if (math.abs(lengthSq - 1) > epsSq)
                {
                    if (lengthSq > epsSq)
                    {
                        x *= math.rsqrt(lengthSq);
                    }
                    else
                    {
                        x = new float3(1, 0, 0);
                    }

                    return true;
                }

                return false;
            }

            private static bool NormalizePerpendicular(float3 axis, ref float3 perpendicular)
            {
                // 确保垂直实际上垂直于方向
                float dot = math.dot(axis, perpendicular);
                float absDot = math.abs(dot);
                if (absDot > 1.0f - 1e-5f)
                {
                    // 平行，选择任意垂线
                    float3 dummy;
                    CalculatePerpendicularNormalized(axis, out perpendicular, out dummy);
                    return true;
                }

                if (absDot > 1e-5f)
                {
                    // 拒绝方向
                    perpendicular -= dot * axis;
                    NormalizeSafe(ref perpendicular);
                    return true;
                }

                return NormalizeSafe(ref perpendicular);
            }

            public void Update(RigidTransform worldFromA, RigidTransform worldFromB, bool lockBtoA, float3 pivotA, float3 pivotB,
                ref float3 directionA, ref float3 directionB, ref float3 perpendicularA, ref float3 perpendicularB, Object target)
            {
                // 在 world 空间工作
                float3 directionAinW = math.rotate(worldFromA, directionA);
                float3 directionBinW = math.rotate(worldFromB, directionB);
                float3 perpendicularAinW = math.rotate(worldFromB, perpendicularA);
                float3 perpendicularBinW = math.rotate(worldFromB, perpendicularB);
                bool changed = false;

                // 如果目标发生变化，请修复输入并重置参考方向以与新目标的轴对齐
                if (target != m_LastTarget)
                {
                    m_LastTarget = target;

                    // 强制执行标准化方向
                    changed |= NormalizeSafe(ref directionAinW);
                    changed |= NormalizeSafe(ref directionBinW);

                    // 强制标准化垂直线，与其各自的方向正交
                    changed |= NormalizePerpendicular(directionAinW, ref perpendicularAinW);
                    changed |= NormalizePerpendicular(directionBinW, ref perpendicularBinW);

                    // 从方向和垂直方向计算 joint 在 A 中的旋转
                    float3x3 rotationA = new float3x3(directionAinW, perpendicularAinW, math.cross(directionAinW, perpendicularAinW));
                    m_RefA = new quaternion(rotationA);

                    if (lockBtoA)
                    {
                        m_RefB = m_RefA;
                    }
                    else
                    {
                        // 从方向和垂直方向计算 joint 在 B 中的旋转
                        float3x3 rotationB = new float3x3(directionBinW, perpendicularBinW, math.cross(directionBinW, perpendicularBinW));
                        m_RefB = new quaternion(rotationB);
                    }
                }

                EditorGUI.BeginChangeCheck();

                // 制作旋转器
                quaternion oldRefA = m_RefA;
                quaternion oldRefB = m_RefB;

                float3 pivotAinW = math.transform(worldFromA, pivotA);
                m_RefA = Handles.RotationHandle(m_RefA, pivotAinW);

                float3 pivotBinW;
                if (lockBtoA)
                {
                    directionB = math.rotate(math.inverse(worldFromB), directionAinW);
                    perpendicularB = math.rotate(math.inverse(worldFromB), perpendicularAinW);
                    pivotBinW = pivotAinW;
                    m_RefB = m_RefA;
                }
                else
                {
                    pivotBinW = math.transform(worldFromB, pivotB);
                    m_RefB = Handles.RotationHandle(m_RefB, pivotBinW);
                }

                // 应用旋转器的更改
                if (EditorGUI.EndChangeCheck())
                {
                    quaternion dqA = math.mul(m_RefA, math.inverse(oldRefA));
                    quaternion dqB = math.mul(m_RefB, math.inverse(oldRefB));
                    directionAinW = math.mul(dqA, directionAinW);
                    directionBinW = math.mul(dqB, directionBinW);
                    perpendicularAinW = math.mul(dqB, perpendicularAinW);
                    perpendicularBinW = math.mul(dqB, perpendicularBinW);
                    changed = true;
                }

                // 如果轴发生变化则写回
                if (changed)
                {
                    Undo.RecordObject(target, "Edit joint axis");
                    directionA = math.rotate(math.inverse(worldFromA), directionAinW);
                    directionB = math.rotate(math.inverse(worldFromB), directionBinW);
                    perpendicularA = math.rotate(math.inverse(worldFromB), perpendicularAinW);
                    perpendicularB = math.rotate(math.inverse(worldFromB), perpendicularBinW);
                }

                // 绘制更新的轴
                float3 z = new float3(0, 0, 1); // ArrowHandleCap() draws an arrow pointing in (0, 0, 1)
                Handles.ArrowHandleCap(0, pivotAinW, Quaternion.FromToRotation(z, directionAinW), HandleUtility.GetHandleSize(pivotAinW) * 0.75f, Event.current.type);
                Handles.ArrowHandleCap(0, pivotAinW, Quaternion.FromToRotation(z, perpendicularAinW), HandleUtility.GetHandleSize(pivotAinW) * 0.75f, Event.current.type);
                if (!lockBtoA)
                {
                    Handles.ArrowHandleCap(0, pivotBinW, Quaternion.FromToRotation(z, directionBinW), HandleUtility.GetHandleSize(pivotBinW) * 0.75f, Event.current.type);
                    Handles.ArrowHandleCap(0, pivotBinW, Quaternion.FromToRotation(z, perpendicularBinW), HandleUtility.GetHandleSize(pivotBinW) * 0.75f, Event.current.type);
                }
            }
        }

        public static void EditLimits(RigidTransform worldFromA, RigidTransform worldFromB, float3 pivotA, float3 axisA, float3 axisB, float3 perpendicularA, float3 perpendicularB,
            ref float minLimit, ref float maxLimit, JointAngularLimitHandle limitHandle, Object target)
        {
            // Transform 至 world 空间
            float3 pivotAinW = math.transform(worldFromA, pivotA);
            float3 axisAinW = math.rotate(worldFromA, axisA);
            float3 perpendicularAinW = math.rotate(worldFromA, perpendicularA);
            float3 axisBinW = math.rotate(worldFromA, axisB);
            float3 perpendicularBinW = math.rotate(worldFromB, perpendicularB);

            // 从 joint 空间获取旋转
            // JointAngularLimitHandle 在 (0, 0, 1) 处使用轴 = (1, 0, 0) 和角度 = 0，因此选择旋转以将其指向我们的轴方向和垂直方向
            float3x3 worldFromJointA = new float3x3(axisAinW, -math.cross(axisAinW, perpendicularAinW), perpendicularAinW);
            float3x3 worldFromJointB = new float3x3(axisBinW, -math.cross(axisBinW, perpendicularBinW), perpendicularBinW);
            float3x3 jointBFromA = math.mul(math.transpose(worldFromJointB), worldFromJointA);

            // 设置角度限制控制的方向
            float angle = CalculateTwistAngle(new quaternion(jointBFromA), 0); // index = 0 because axis is the first column in worldFromJoint
            quaternion limitOrientation = math.mul(quaternion.AxisAngle(axisAinW, angle), new quaternion(worldFromJointA));
            Matrix4x4 handleMatrix = Matrix4x4.TRS(pivotAinW, limitOrientation, Vector3.one);

            float size = HandleUtility.GetHandleSize(pivotAinW) * 0.75f;

            limitHandle.xMin = -maxLimit;
            limitHandle.xMax = -minLimit;
            limitHandle.xMotion = ConfigurableJointMotion.Limited;
            limitHandle.yMotion = ConfigurableJointMotion.Locked;
            limitHandle.zMotion = ConfigurableJointMotion.Locked;
            limitHandle.yHandleColor = new Color(0, 0, 0, 0);
            limitHandle.zHandleColor = new Color(0, 0, 0, 0);
            limitHandle.radius = size;

            using (new Handles.DrawingScope(handleMatrix))
            {
                // 绘制参考轴
                float3 z = new float3(0, 0, 1); // ArrowHandleCap() draws an arrow pointing in (0, 0, 1)
                Handles.ArrowHandleCap(0, float3.zero, Quaternion.FromToRotation(z, new float3(1, 0, 0)), size, Event.current.type);

                // 绘制极限编辑器手柄
                EditorGUI.BeginChangeCheck();
                limitHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    // 在设置新限制之前记录目标对象，以便可以撤消/重做更改
                    Undo.RecordObject(target, "Edit joint angular limits");
                    minLimit = -limitHandle.xMax;
                    maxLimit = -limitHandle.xMin;
                }
            }
        }
    }
}
#endif
