using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    public struct ClientRequestApproval : IApprovalRpcCommand
    {
        public FixedString4096Bytes Payload;
    }

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct ClientConnectionApprovalSystem : ISystem
    {
        FixedString4096Bytes m_Payload;
        // 当我们处于批准状态但有效负载尚未准备好发送时进行标记
        bool m_SendApprovalWhenReady;
        // 这仅用于检测我们是否已完全连接而没有触发批准（因此连接批准功能已关闭）
        bool m_ApprovalIsRequired;

        bool AuthenticationIsEnabled(ref SystemState state)
        {
            // Thin clients 应始终使用虚拟有效负载，因为它们无法使用玩家身份验证服务
            if (state.WorldUnmanaged.IsThinClient())
                return false;
            return ConnectionApprovalData.PlayerAuthenticationEnabled.Data;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 启用玩家身份验证服务，禁用时使用虚拟负载
            ConnectionApprovalData.PlayerAuthenticationEnabled.Data = false;

            ConnectionApprovalData.ApprovalPayload.Data = default;
            m_Payload = new FixedString4096Bytes((FixedString4096Bytes)"ABC");
            state.RequireForUpdate<EnableConnectionApproval>();
            state.RequireForUpdate<RpcCollection>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 如果我们未经批准就连接，请投诉，因为这就是此示例所演示的内容
            if (!m_ApprovalIsRequired && SystemAPI.HasSingleton<NetworkId>())
            {
                UnityEngine.Debug.LogError($"[{state.WorldUnmanaged.Name}] Connection Approval system ran without connection approval enabled. To test approvals properly you need to load the sample via the Frontend menu as it ensures the feature is enabled.");
                state.Enabled = false;
            }

            // 检查尚未完全连接的连接并发送连接批准消息
            foreach (var evt in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                // Note: 即使进入握手状态后发送您的批准实际上也是无害的（严格来说不
                // 等待批准状态）。即使连接批准已关闭，也可以发送此信息。
                if (evt.State == ConnectionState.State.Approval)
                    m_ApprovalIsRequired = m_SendApprovalWhenReady = true;
                else if (evt.State == ConnectionState.State.Disconnected)
                    m_SendApprovalWhenReady = false;
            }

            // 如果启用了玩家身份验证服务，请等待，直到设置了批准有效负载的身份验证数据
            if (AuthenticationIsEnabled(ref state))
            {
                if (ConnectionApprovalData.ApprovalPayload.Data.Payload.Length == 0)
                    return;
                m_Payload = ConnectionApprovalData.ApprovalPayload.Data.Payload;
            }

            // 现在我们准备发送有效负载，重置重新连接的就绪状态（重新使用当前有效负载）
            if (m_SendApprovalWhenReady)
            {
                UnityEngine.Debug.Log($"[{state.WorldUnmanaged.Name}] Client sending approval message to server once...");
                ReadOnlySpan<ComponentType> types = stackalloc ComponentType[] {ComponentType.ReadOnly<ClientRequestApproval>(), ComponentType.ReadOnly<SendRpcCommandRequest>()};
                var rpcEntity = state.EntityManager.CreateEntity(types);
                state.EntityManager.SetComponentData(rpcEntity, new ClientRequestApproval {Payload = m_Payload});
                m_SendApprovalWhenReady = false;
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ServerConnectionApprovalSystem : ISystem
    {
        FixedString512Bytes m_DummyPayload;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_DummyPayload = new FixedString512Bytes((FixedString4096Bytes)"ABC");
            ConnectionApprovalData.PendingApprovals.Data = new UnsafeRingQueue<PendingApproval>(32, Allocator.Persistent);
            ConnectionApprovalData.ApprovalResults.Data = new UnsafeRingQueue<ApprovalResult>(32, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            ConnectionApprovalData.PendingApprovals.Data.Dispose();
            ConnectionApprovalData.ApprovalResults.Data.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 检查尚未完全连接的连接并发送连接批准消息
            foreach (var (receiveRpc, approvalMsg, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRW<ClientRequestApproval>>().WithEntityAccess())
            {
                // 始终清理 RPC 消息 entity。
                ecb.DestroyEntity(entity);

                var connectionEntity = receiveRpc.ValueRO.SourceConnection;
                var conn = state.EntityManager.GetComponentData<NetworkStreamConnection>(connectionEntity);

                if (state.EntityManager.HasComponent<ConnectionApproved>(connectionEntity))
                {
                    UnityEngine.Debug.LogError($"[{state.WorldUnmanaged.Name}] {conn.Value.ToFixedString()} on {connectionEntity.ToFixedString()} sent approval while already approved!");
                    continue;
                }

                var payload = approvalMsg.ValueRO.Payload;

                // 如果虚拟有效负载匹配，我们将允许它；如果给定玩家 ID/令牌，我们将验证玩家
                if (payload.Equals(m_DummyPayload))
                {
                    UnityEngine.Debug.Log($"[{state.WorldUnmanaged.Name}] Approved with dummy payload {conn.Value.ToFixedString()} on {connectionEntity.ToFixedString()}!");
                    // 将消息发送者的连接标记为已批准
                    ecb.AddComponent<ConnectionApproved>(connectionEntity);
                }
                else
                {
                    var splitIndex = payload.IndexOf(':');
                    FixedString64Bytes playerId = new FixedString64Bytes();
                    playerId.Append(payload.Substring(0, splitIndex));
                    var accessToken = payload.Substring(splitIndex+1, payload.Length);
                    ConnectionApprovalData.PendingApprovals.Data.Enqueue(new PendingApproval(){ PlayerId = playerId, AccessToken = accessToken, Payload = payload, ConnectionEntity = connectionEntity});
                }
            }

            while (ConnectionApprovalData.ApprovalResults.Data.TryDequeue(out var approvalResult))
            {
                var connData = SystemAPI.GetComponentRO<NetworkStreamConnection>(approvalResult.ConnectionEntity);
                if (approvalResult.Success)
                {
                    UnityEngine.Debug.Log($"[{state.WorldUnmanaged.Name}] Approved with player account {connData.ValueRO.Value.ToFixedString()} on {approvalResult.ConnectionEntity.ToFixedString()}!");
                    ecb.AddComponent<ConnectionApproved>(approvalResult.ConnectionEntity);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD // 不要将其记录在产品中，以避免泄漏真正的身份验证令牌，并防止恶意用户导致日志垃圾邮件。
                    UnityEngine.Debug.LogError($"[{state.WorldUnmanaged.Name}] Disconnecting {connData.ValueRO.Value.ToFixedString()} on {approvalResult.ConnectionEntity.ToFixedString()} as sent incorrect approval payload '{approvalResult.Payload}'!");
#endif
                    // TODO - 请注意，此原因当前未作为关闭的一部分传输到 client，
                    // 但至少 server 可以 query （并记录）它。
                    ecb.AddComponent(approvalResult.ConnectionEntity, new NetworkStreamRequestDisconnect
                    {
                        Reason = NetworkStreamDisconnectReason.ApprovalFailure,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary> 来自 client 的传入审批请求的数据需要通过服务 </summary> 进行验证
    public struct PendingApproval
    {
        public FixedString64Bytes PlayerId;
        public FixedString4096Bytes AccessToken;
        public FixedString4096Bytes Payload;    // 存储完整的有效负载数据以用于调试目的
        public Entity ConnectionEntity;
    }

    /// <summary> 玩家账户验证结果的数据 </summary>
    public struct ApprovalResult
    {
        public bool Success;
        public FixedString4096Bytes Payload;
        public Entity ConnectionEntity;
    }

    /// <summary>
    /// DOTS 和 GameObjects 之间的通信桥梁，当任何一方的数据准备好时，它将在
    /// 对方。因为只有一个 client 为每种类型的消息验证单个容器就足够了。
    /// </summary>
    public abstract class ConnectionApprovalData
    {
        public static readonly SharedStatic<bool> PlayerAuthenticationEnabled = SharedStatic<bool>.GetOrCreate<PlayerAuthenticationEnabledKey>();
        /// <summary> server 上待处理且已验证的批准请求，因为可能有多个 clients 同时连接，这些请求已排队 </summary>
        public static readonly SharedStatic<UnsafeRingQueue<PendingApproval>> PendingApprovals = SharedStatic<UnsafeRingQueue<PendingApproval>>.GetOrCreate<PendingApprovalDataKey>();
        public static readonly SharedStatic<UnsafeRingQueue<ApprovalResult>> ApprovalResults = SharedStatic<UnsafeRingQueue<ApprovalResult>>.GetOrCreate<ApprovalResultDataKey>();
        /// <summary> Client 有效负载准备发送到 server，应该只有其中之一，因为 client 不会多次验证 </summary>
        public static readonly SharedStatic<ClientRequestApproval> ApprovalPayload = SharedStatic<ClientRequestApproval>.GetOrCreate<ApprovalPayloadDataKey>();

        // 共享静态字段的标识符
        class PendingApprovalDataKey {}
        class ApprovalResultDataKey {}
        class ApprovalPayloadDataKey {}
        class PlayerAuthenticationEnabledKey {}
    }
}
