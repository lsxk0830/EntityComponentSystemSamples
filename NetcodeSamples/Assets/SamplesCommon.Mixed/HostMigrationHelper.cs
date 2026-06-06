using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.HostMigration;
using Unity.Networking.Transport;
using UnityEngine;

namespace Unity.NetCode.Samples.Common
{
    public static class HostMigrationHelper
    {
        /// <summary>
        /// 可选的帮助程序方法，用于协助主机迁移过程中所需的操作。<see cref="SetHostMigrationData"/>
        /// 也可以直接使用已手动设置恢复托管的 server world 进行调用
        /// 具有给定的主机迁移数据。
        ///
        /// 获取给定的主机迁移数据并启动主机迁移过程。这开始加载
        /// 主机之前加载的 entity scenes（如果有）。<see cref="NetworkDriverStore"/> 和 <see cref="NetworkDriver"/> 中
        /// 新的 server world 将被适当创建，驱动程序构造函数需要能够
        /// 使用给定的构造函数设置中继连接。本地 client world 将从继电器切换到
        /// 本地 IPC 连接到 server world。
        /// </summary>
        /// <param name="driverConstructor">The 网络驱动程序构造函数在新的 server world 和 client world.</param> 中注册
        /// <param name="migrationData">The 包含主机迁移数据的数据 blob，部署到新的 server world.</param>
        /// 如果启动新 server</returns> 时出现任何立即故障，则 <returns>Returns false
        public static bool MigrateDataToNewServerWorld(INetworkStreamDriverConstructor driverConstructor, ref NativeArray<byte> migrationData)
        {
            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = driverConstructor;
            var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            if (migrationData.Length == 0)
                Debug.LogWarning($"No host migration data given during host migration, no data will be deployed.");
            else
                HostMigrationData.Set(migrationData, serverWorld);

            using var serverDriverQuery = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            var serverDriver = serverDriverQuery.GetSingletonRW<NetworkStreamDriver>();
            if (!serverDriver.ValueRW.Listen(NetworkEndpoint.AnyIpv4))
            {
                Debug.LogError($"NetworkStreamDriver.Listen() failed");
                return false;
            }

            var ipcPort = serverDriver.ValueRW.GetLocalEndPoint(serverDriver.ValueRW.DriverStore.FirstDriver).Port;

            // 需要重新创建 client 驱动，然后通过 IPC 直接连接到新的 server world
            return ConfigureClientAndConnect(ClientServerBootstrap.ClientWorld, driverConstructor, NetworkEndpoint.LoopbackIpv4.WithPort(ipcPort));
        }

        /// <summary>
        /// 可选的帮助程序方法，用于使用给定的驱动程序构造函数创建 client 驱动程序并连接到端点。
        /// NetworkDriverStore 将被重新创建，因为 client 可以从本地 IPC 连接切换到中继
        /// 连接或反转，继电器数据可以在驱动程序创建时设置。
        /// </summary>
        /// <param name="clientWorld">The client world 需要为 configured.</param>
        /// <param name="driverConstructor">The 网络驱动程序构造函数，用于在 client world.</param> 中创建新的网络驱动程序
        /// <param name="serverEndpoint">The 配置网络 driver.</param> 后 client 将连接到的网络端点
        /// <returns>Returns true 如果连接调用 succeeds</returns>
        public static bool ConfigureClientAndConnect(World clientWorld, INetworkStreamDriverConstructor driverConstructor, NetworkEndpoint serverEndpoint)
        {
            if (clientWorld == null || !clientWorld.IsCreated)
            {
                Debug.LogError("HostMigration.ConfigureClientAndConnect: Invalid client world provided");
                return false;
            }

            using var clientNetDebugQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetDebug>());
            var clientNetDebug = clientNetDebugQuery.GetSingleton<NetDebug>();
            var clientDriverStore = new NetworkDriverStore();
            driverConstructor.CreateClientDriver(clientWorld, ref clientDriverStore, clientNetDebug);
            using var clientDriverQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            var clientDriver = clientDriverQuery.GetSingleton<NetworkStreamDriver>();
            clientDriver.ResetDriverStore(clientWorld.Unmanaged, ref clientDriverStore);

            var connectionEntity = clientDriver.Connect(clientWorld.EntityManager, serverEndpoint);
            if (connectionEntity == Entity.Null)
                return false;
            return true;
        }
    }
}
