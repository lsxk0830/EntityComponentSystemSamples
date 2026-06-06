using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    public struct ChatMessage : IRpcCommand
    {
        public FixedString128Bytes Message;
    }

    public struct ChatUser : IRpcCommand
    {
        public int UserData;
    }

    public struct ChatUserInitialized : IComponentData { }

    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class RpcClientSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<EnableRPC>();
            // 在建立连接之前无法发送任何 RPC/聊天消息
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            // 这并未设置为使用一个或多个聊天窗口处理多个 clients/worlds，但
            // 必须使用 rpc 消息，否则将发出警告
            if (World.IsThinClient())
            {
                EntityManager.DestroyEntity(GetEntityQuery(new EntityQueryDesc()
                {
                    All = new[] { ComponentType.ReadOnly<ReceiveRpcCommandRequest>() },
                    Any = new[] { ComponentType.ReadOnly<ChatMessage>(), ComponentType.ReadOnly<ChatUser>() }
                }));
            }

            // 当用户或聊天消息 RPCs 到达时，它们会被添加到消费队列中
            // 在 UI system 中。
            var buffer = new EntityCommandBuffer(Allocator.Temp);
            var connections = GetComponentLookup<NetworkId>(true);
            FixedString32Bytes worldName = World.Name;
            foreach (var (rpcCmd, chat, entity) in SystemAPI.Query<RefRW<ReceiveRpcCommandRequest>, RefRW<ChatMessage>>().WithEntityAccess())
            {
                buffer.DestroyEntity(entity);

                // 不是线程安全的，因此所有 UI 逻辑都保留在主线程上
                RpcUiData.Messages.Data.Enqueue(chat.ValueRO.Message);
            }

            foreach (var (rpcCmd, user, entity) in SystemAPI.Query<ReceiveRpcCommandRequest, ChatUser>().WithEntityAccess())
            {
                var conId = connections[rpcCmd.SourceConnection].Value;
                UnityEngine.Debug.Log(
                    $"[{worldName}] Received {user.UserData} from connection {conId}");
                buffer.DestroyEntity(entity);
                RpcUiData.Users.Data.Enqueue(user.UserData);
            }

            if (!buffer.IsEmpty)
            {
                buffer.Playback(EntityManager);
            }
        }
    }

    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class RpcServerSystem : SystemBase
    {
        // 用户信息仅作为单个整数进行跟踪（=连接 ID），以使这尽可能简单
        private NativeList<int> m_Users;

        protected override void OnCreate()
        {
            RequireForUpdate<EnableRPC>();
            m_Users = new NativeList<int>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_Users.Dispose();
        }

        protected override void OnUpdate()
        {
            var buffer = new EntityCommandBuffer(Allocator.Temp);
            var connections = GetComponentLookup<NetworkId>(true);
            FixedString32Bytes worldName = World.Name;

            // 新传入的 RPCs 与 ReceiveRpcCommandRequestComponent component 和 RPC 数据负载 component (ChatMessage) 一起放置在 entity 上
            // 处理完后应删除此 entity
            // server RPC 向所有连接广播聊天消息
            foreach (var (rpcCmd, chat, entity) in SystemAPI.Query<ReceiveRpcCommandRequest, ChatMessage>().WithEntityAccess())
            {
                var conId = connections[rpcCmd.SourceConnection].Value;
                UnityEngine.Debug.Log(
                    $"[{worldName}] Received {chat.Message} on connection {conId}.");
                buffer.DestroyEntity(entity);
                var broadcastEntity = buffer.CreateEntity();
                buffer.AddComponent(broadcastEntity, new ChatMessage() { Message = FixedString.Format("User {0}: {1}", conId, chat.Message) });
                buffer.AddComponent<SendRpcCommandRequest>(broadcastEntity);
            }

            var users = m_Users;
            foreach (var (id, entity) in SystemAPI.Query<NetworkId>().WithEntityAccess().WithNone<ChatUserInitialized>())
            {
                var connectionId = id.Value;

                // 通知所有有关新聊天用户（包括他自己）的连接
                var broadcastEntity = buffer.CreateEntity();
                buffer.AddComponent(broadcastEntity, new ChatUser() { UserData = connectionId });
                buffer.AddComponent<SendRpcCommandRequest>(broadcastEntity);
                UnityEngine.Debug.Log($"[{worldName}] New user 'User {connectionId}' connected. Broadcasting user entry to all connections;");

                // 仅通知新连接有关已连接的其他用户，这使用 TargetConnection 部分
                // RPC 请求 component
                for (int i = 0; i < users.Length; ++i)
                {
                    var newEntity = buffer.CreateEntity();
                    var user = users[i];
                    buffer.AddComponent(newEntity, new ChatUser() { UserData = user });
                    buffer.AddComponent<SendRpcCommandRequest>(newEntity);
                    buffer.SetComponent(newEntity, new SendRpcCommandRequest { TargetConnection = entity });
                    UnityEngine.Debug.Log($"[{worldName}] Sending user 'User {user}' to new connection {connectionId}");
                }

                // 将连接添加到用户列表
                users.Add(connectionId);

                // 标记此连接/用户，以便我们不再处理
                buffer.AddComponent<ChatUserInitialized>(entity);
            }

            if (!buffer.IsEmpty)
            {
                buffer.Playback(EntityManager);
            }
        }
    }

    // 对 DOTS 和 GameObject systems 之间传递数据的队列进行管理，这样
    // 两者解耦得更干净一些
    [UpdateInGroup(typeof(HelloNetcodeSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RpcUiDataSystem : SystemBase
    {
        private bool m_OwnsData;
        protected override void OnCreate()
        {
            m_OwnsData = !RpcUiData.Users.Data.IsCreated;
            if (m_OwnsData)
            {
                RpcUiData.Users.Data = new UnsafeQueue<int>(Allocator.Persistent);
                RpcUiData.Messages.Data = new UnsafeQueue<FixedString128Bytes>(Allocator.Persistent);
            }
            Enabled = false;
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            if (m_OwnsData)
            {
                RpcUiData.Messages.Data.Dispose();
                RpcUiData.Users.Data.Dispose();
            }
        }
    }

    public abstract class RpcUiData
    {
        public static readonly SharedStatic<UnsafeQueue<FixedString128Bytes>> Messages = SharedStatic<UnsafeQueue<FixedString128Bytes>>.GetOrCreate<MessagesKey>();
        public static readonly SharedStatic<UnsafeQueue<int>> Users = SharedStatic<UnsafeQueue<int>>.GetOrCreate<UsersKey>();

        // 共享静态字段的标识符
        private class MessagesKey {}
        private class UsersKey {}
    }
}
