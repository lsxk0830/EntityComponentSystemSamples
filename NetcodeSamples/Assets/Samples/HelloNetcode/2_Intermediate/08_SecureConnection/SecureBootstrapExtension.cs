//#定义 ENABLE_NETCODE_SAMPLE_SECURE

using Unity.NetCode;

namespace Samples.HelloNetcode
{
#if ENABLE_NETCODE_SAMPLE_SECURE
    [UnityEngine.Scripting.Preserve]
    public class SecureBootStrapExtension : FrontendBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
#if !UNITY_EDITOR && NETCODE_DEBUG
            UnityEngine.Debug.LogWarning(">>>>>>>>>> SAMPLE CODE: don't ship the certificates as a part of your build <<<<<<<<<<");
#elif !UNITY_EDITOR && !NETCODE_DEBUG
            UnityEngine.Debug.LogError(">>>>>>>>>> SAMPLE CODE: Don't ship the certificates as a part of your build <<<<<<<<<<");
#endif
            // 要设置自定义驱动程序，需要连接它的构造函数
            // 在引导程序 system 中创建 world 之前。Netcode引导程序是
            // 已经在主连接示例中定义，并且可以
            // 项目中只有一个设置，我们只需在此处添加这一行
            // 现有的 NetCodeBootstrap 类（通常一个项目只会有
            // boostrap 定义在一处）。
            NetworkStreamReceiveSystem.DriverConstructor = new SecureDriverConstructor();
            return base.Initialize(defaultWorldName);
        }
    }
#endif
}
