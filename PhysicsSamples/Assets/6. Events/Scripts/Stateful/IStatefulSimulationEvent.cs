using Unity.Entities;

namespace Unity.Physics.Stateful
{
    // 描述事件状态。
    // 事件状态设置为：
    //    0) 未定义，当状态未知或不需要时
    //    1）进入，当 2 个物体在当前帧中有交互，但在上一帧没有交互时
    //    2）停留，当 2 个物体在当前帧中相互作用，并且它们在前一帧中也相互作用时
    //    3) 退出，当两个物体在当前帧中没有交互，但它们在前一帧中交互时
    public enum StatefulEventState : byte
    {
        Undefined,
        Enter,
        Stay,
        Exit
    }

    // 使用额外的 StatefulEventState 扩展 ISimulationEvent。
    public interface IStatefulSimulationEvent<T> : IBufferElementData, ISimulationEvent<T>
    {
        public StatefulEventState State { get; set; }
    }
}
