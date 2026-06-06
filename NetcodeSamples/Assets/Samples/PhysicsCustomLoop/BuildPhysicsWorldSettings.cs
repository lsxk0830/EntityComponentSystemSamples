using Unity.Entities;
using Unity.Physics;

namespace Unity.NetCode
{
    /// <summary>
    /// Component 用于控制 predicted 物理循环。让您配置每个帧的方式
    /// 物理 world 已构建（“增量”或完整），并且物理是否应使用立即模式 run
    /// （这对于小规模物理模拟来说可能会更好）。
    /// </summary>
    internal struct BuildPhysicsWorldSettings : IComponentData
    {
        /// <summary>
        /// 使用立即模式构建物理 world。所有工作都是在主线程上同步完成的。
        /// </summary>
        public byte UseImmediateMode;
        /// <summary>
        /// 使用立即模式步进物理 world。所有工作都是在主线程上同步完成的。
        /// </summary>
        public byte StepImmediateMode;
        /// <summary>
        /// 不要重建物理 world 数据，而是更新预先存在的刚体、运动
        /// 电流变换、物理速度和属性。
        /// 宽相树不是从头开始重建的，而是使用当前的物理速度和重力进行更新（参见
        /// <see cref="CollisionWorld.UpdateDynamicTree"/>)。
        /// </summary>
        public byte UpdateBroadphaseAndMotion;
        /// <summary>
        /// 自第一个 prediction 更新以来的物理更新次数。
        /// </summary>
        public int CurrentPhysicsStep;
    }
}
