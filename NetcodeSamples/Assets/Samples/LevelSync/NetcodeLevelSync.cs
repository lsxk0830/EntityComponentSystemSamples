using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

public enum LevelSyncState
{
    Idle,
    LevelLoadRequest,
    LevelLoadInProgress,
    LevelLoaded
}

public struct ClientLoadLevel : IRpcCommand
{
    public int LevelIndex;
}

public struct ClientReady : IRpcCommand
{
    public int LevelIndex;
}

// 当关卡加载同步开始时添加到网络连接，clients 加载完成时没有此连接是新连接
public struct LevelLoadingInProgress : IComponentData { }

// 当连接/client 完成加载时添加，因此当 server 可以启动时，进行中计数应等于完成计数
public struct LevelLoadingDone : IComponentData { }

public struct LevelSyncStateComponent : IComponentData
{
    public LevelSyncState State;
    public int CurrentLevel;
    // 当 client 状态为 LevelLoadInProgress 时应加载此级别
    public int NextLevel;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[CreateBefore(typeof(LevelLoader))]
public partial class NetcodeClientLevelSync : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();
        if (!World.IsHost())
            EntityManager.CreateEntity(typeof(LevelSyncStateComponent));
    }

    protected override void OnUpdate()
    {
        var connectionEntity = SystemAPI.GetSingletonEntity<LocalConnection>(); // 通过 NetworkId 仍然适用于二进制 world，但对于单个 world 主机，现在当其他玩家连接时您有多个网络 IDs
        var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
        if (!SystemAPI.QueryBuilder().WithAll<ClientLoadLevel, ReceiveRpcCommandRequest>().Build().IsEmptyIgnoreFilter)
        {
            FixedString64Bytes worldName = World.Name;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            // 当加载级别命令到达时，禁用 ghost 同步，卸载当前级别并加载指定级别
            foreach (var (level, entity) in SystemAPI.Query<RefRO<ClientLoadLevel>>().WithEntityAccess()
                         .WithAll<ReceiveRpcCommandRequest>())
            {
                UnityEngine.Debug.Log($"[{worldName}] received command to load {level.ValueRO.LevelIndex}");
                ecb.RemoveComponent<NetworkStreamInGame>(connectionEntity);
                levelState.State = LevelSyncState.LevelLoadRequest;
                levelState.NextLevel = level.ValueRO.LevelIndex;
                SystemAPI.SetSingleton(levelState);
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }

        if (levelState.State == LevelSyncState.LevelLoaded)
        {
            if (!EntityManager.HasComponent<NetworkStreamInGame>(connectionEntity))
            {
                var netId = SystemAPI.GetSingleton<NetworkId>();
                UnityEngine.Debug.Log($"{World.Name} enable sync on connection {netId.Value}");
                EntityManager.AddComponent<NetworkStreamInGame>(connectionEntity);
            }

            UnityEngine.Debug.Log($"[{World.Name}] notifying server it's finished loading {levelState.CurrentLevel}");
            var rpcCmd = EntityManager.CreateEntity(typeof(ClientReady), typeof(SendRpcCommandRequest));
            EntityManager.AddComponentData(rpcCmd, new ClientReady(){LevelIndex = levelState.CurrentLevel});

            levelState.State = LevelSyncState.Idle;
            SystemAPI.SetSingleton(levelState);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[CreateBefore(typeof(LevelLoader))]
public partial class NetcodeServerLevelSync : SystemBase
{
    private EntityQuery m_ClientsReadyQuery;
    private EntityQuery m_ClientsLoadingQuery;

    protected override void OnCreate()
    {
        m_ClientsReadyQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LevelLoadingDone>());
        m_ClientsLoadingQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LevelLoadingInProgress>());

        RequireForUpdate<NetworkId>();
        EntityManager.CreateEntity(typeof(LevelSyncStateComponent));
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var connections = GetComponentLookup<NetworkId>();
        var loadingInProgress = GetComponentLookup<LevelLoadingInProgress>();
        // TODO: 等级号未用于任何 atm
        foreach (var (level, req, entity) in SystemAPI.Query<ClientReady, ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            UnityEngine.Debug.Log($"Client {connections[req.SourceConnection].Value} finished loading {level.LevelIndex}.");
            ecb.AddComponent<LevelLoadingDone>(req.SourceConnection);
            if (!loadingInProgress.HasComponent(req.SourceConnection))
                UnityEngine.Debug.LogError("Ready client was never marked as starting level loading");
            ecb.DestroyEntity(entity);
        }
        ecb.Playback(EntityManager);

        var readyCount = m_ClientsReadyQuery.CalculateEntityCount();
        var loadingCount = m_ClientsLoadingQuery.CalculateEntityCount();

        // 所有 scenes 已加载完毕，clients 已准备就绪
        var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
        if (levelState.State == LevelSyncState.LevelLoaded && loadingCount == readyCount)
        {
            UnityEngine.Debug.Log("Server subscenes finished loading and all clients are ready");

            var conQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            var cons = conQuery.ToEntityArray(Allocator.Temp);
            var conIds = conQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            for (int i = 0; i < cons.Length; ++i)
            {
                if (!EntityManager.HasComponent<NetworkStreamInGame>(cons[i]))
                {
                    UnityEngine.Debug.Log($"[{World.Name}] Enable sync on {conIds[i].Value}");
                    EntityManager.AddComponent<NetworkStreamInGame>(cons[i]);
                }
            }

            levelState.State = LevelSyncState.Idle;
            SystemAPI.SetSingleton(levelState);
            ecb = new EntityCommandBuffer(Allocator.Temp);
            FixedString64Bytes world = World.Name;
            foreach (var (_, entity) in SystemAPI.Query<NetworkId>().WithEntityAccess())
            {
                ecb.RemoveComponent<LevelLoadingInProgress>(entity);
                ecb.RemoveComponent<LevelLoadingDone>(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}

public static class NetcodeLevelSync
{
    public static void TriggerClientLoadLevel(int level, World serverWorld)
    {
        // 所有 clients 上的 Trigger 级别负载
        var rpcCmd = serverWorld.EntityManager.CreateEntity();
        serverWorld.EntityManager.AddComponentData(rpcCmd, new ClientLoadLevel(){LevelIndex = level});
        serverWorld.EntityManager.AddComponent<SendRpcCommandRequest>(rpcCmd);

        // 将每个连接标记为正在加载
        var connectionsQuery =
            serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        var connectionEntities = connectionsQuery.ToEntityArray(Allocator.Temp);
        foreach (var connection in connectionEntities)
        {
            serverWorld.EntityManager.AddComponentData(connection, new LevelLoadingInProgress());
        }
    }

    public static void SetLevelState(LevelSyncState state, World world)
    {
        var levelStateQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<LevelSyncStateComponent>());
        var levelStateEntity = levelStateQuery.ToEntityArray(Allocator.Temp);
        var levelStateData = levelStateQuery.ToComponentDataArray<LevelSyncStateComponent>(Allocator.Temp);
        var levelState = levelStateData[0];
        levelState.State = state;
        world.EntityManager.SetComponentData(levelStateEntity[0], levelState);

    }
}
