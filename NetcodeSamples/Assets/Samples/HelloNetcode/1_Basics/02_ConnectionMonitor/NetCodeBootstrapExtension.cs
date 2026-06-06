// #定义 ENABLE_NETCODE_SAMPLE_TIMEOUT

namespace Samples.HelloNetcode
{
#if ENABLE_NETCODE_SAMPLE_TIMEOUT
    [UnityEngine.Scripting.Preserve]
    public class NetCodeBootstrapExtension : NetCodeBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            // 要设置自定义驱动程序，需要连接它的构造函数
            // 在 boostrap system 中创建 world 之前。Netcode引导程序是
            // 已经在前面的主连接示例中定义，并且可以
            // 项目中只有一个设置，我们只需在此处添加这一行
            // 现有的 NetCodeBootstrap 类（通常一个项目只会有
            // boostrap 定义在一处）。
            NetworkStreamReceiveSystem.DriverConstructor = new DriverConstructor();
            return base.Initialize(defaultWorldName);
        }
    }
#endif
}
