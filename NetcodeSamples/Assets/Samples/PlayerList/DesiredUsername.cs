using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.NetCode.Samples.PlayerList
{
    /// <summary>
    ///     PlayerList 示例将处理通知此 `DesiredUsername` 的其他用户。
    ///     <inheritdoc cref="Value"/>
    /// </summary>
    public struct DesiredUsername : IComponentData
    {
        /// <remarks>Changing 该值将 trigger 和 RPC 广播，因此您应该仅在用户完成输入新的 name.</remarks> 后设置此值
        public FixedString64Bytes Value;
    }
}
