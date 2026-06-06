using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using System;

namespace Unity.NetCode
{
    /// <summary>
    /// Component 用于配置物理如何重建和步进 predicted 物理 world。
    /// </summary>
    [Serializable]
    public struct PhysicsLoopConfig : IComponentData
    {
        /// <summary>
        /// 使用立即模式（无 jobs，仅主线程）来单步执行物理 world。这可能比
        /// using jobs in case the number of entities is relatively small.
        /// </summary>
        public byte StepImmediateMode;
        /// <summary>
        /// 使用立即模式（无 jobs，仅主线程）来构建或更新物理 world。这可能比
        /// using jobs in case the number of entities is relatively small.
        /// </summary>
        public byte UseImmediateMode;
        /// <summary>
        /// 启用后，物理 world （特别是宽相树）仅针对第一个 predicted 从头开始​​构建
        /// （当 prediction 启动时）。对于所有后续的 predicted 蜱，宽相 AABB 树仅
        /// 使用上一个物理步骤计算出的物理速度和重力进行更新。这会带来更好的性能
        /// 比从头开始重建，代价是稍差的宽相剔除。
        /// 必须遵守以下条件以避免完整的物理 world 构建：
        /// - prediction 循环内没有创建或销毁动态物理对象
        /// </summary>
        public byte BuildPhysicsWorldOnceThenUpdate;
    }

    /// <summary>
    /// 可用于配置 predicted 物理循环更新的授权行为。会烤的
    /// 烘烤 <see cref="PhysicsLoopConfig"/> component。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PredictedPhysicsStepConfig : MonoBehaviour
    {
        public PhysicsLoopConfig Config;

        class Baker : Baker<PredictedPhysicsStepConfig>
        {
            public override void Bake(PredictedPhysicsStepConfig authoring)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), authoring.Config);
            }
        }
    }
}
