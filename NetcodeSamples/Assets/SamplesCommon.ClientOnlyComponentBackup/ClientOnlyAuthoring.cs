using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Unity.NetCode.Samples
{
    /// <summary>
    /// 测试或游戏中使用的 Authoring 类 scene。将所有仅 client 的 components 添加到 ghost prefab。
    /// </summary>
    internal class ClientOnlyAuthoring : MonoBehaviour
    {
    }

    /// <summary>
    /// Singleton component 用于启用仅 client 的备份 systems。
    /// </summary>
    public struct EnableClientOnlyBackup : IComponentData
    {
    }
}
