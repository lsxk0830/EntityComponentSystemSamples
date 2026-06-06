using Unity.NetCode;

namespace KickBall
{
    // 自定义引导程序，可实现自动连接并创建 Client 和 Server worlds。
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = 7979; // 启用自动连接
            base.Initialize(defaultWorldName); // 使用常规引导程序
            return true;
        }
    }
}


