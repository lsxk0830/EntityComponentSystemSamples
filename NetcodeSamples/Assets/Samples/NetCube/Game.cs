using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// RPC 从 client 向 server 请求游戏进入“游戏中”并发送 snapshots / 输入
public struct GoInGameRequest : IRpcCommand
{
}

// 当 client 与网络 id 连接时，进入游戏并告诉 server 也进入游戏
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct GoInGameClientSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // 仅在带有 CubeSpawner component 数据的 entities 上运行
        state.RequireForUpdate<CubeSpawner>();

        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<NetworkId>()
            .WithNone<NetworkStreamInGame>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithNone<NetworkStreamInGame>())
        {
            commandBuffer.AddComponent<NetworkStreamInGame>(entity);
            var req = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<GoInGameRequest>(req);
            commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
        }
        commandBuffer.Playback(state.EntityManager);
    }
}

// 当 server 收到进入游戏请求时，进入游戏并删除请求
[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GoInGameServerSystem : ISystem
{
    private ComponentLookup<NetworkId> networkIdLookup;
    private ComponentLookup<NetworkStreamIsReconnected> reconnectedLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeSpawner>();

        var builder = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GoInGameRequest>()
            .WithAll<ReceiveRpcCommandRequest>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
        networkIdLookup = state.GetComponentLookup<NetworkId>(true);
        reconnectedLookup = state.GetComponentLookup<NetworkStreamIsReconnected>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 获取 prefab 进行实例化
        var prefab = SystemAPI.GetSingleton<CubeSpawner>().Cube;

        // 正在实例化的 prefab 的名称
        state.EntityManager.GetName(prefab, out var prefabName);
        var worldName = state.WorldUnmanaged.Name;

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        networkIdLookup.Update(ref state);
        reconnectedLookup.Update(ref state);

        foreach (var (reqSrc, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<GoInGameRequest>().WithEntityAccess())
        {
            // 获取请求 client 的 NetworkId
            var networkId = networkIdLookup[reqSrc.ValueRO.SourceConnection];

            // 如果此请求来自重新连接的连接，我们不需要生成和配置播放器 entity
            // 因为它已迁移到新主机并将重新连接到此 client
            if (reconnectedLookup.HasComponent(reqSrc.ValueRO.SourceConnection))
            {
                Debug.Log($"'{worldName}' connection '{networkId.Value}' has reconnected!");
                commandBuffer.DestroyEntity(reqEntity);
                continue;
            }

            commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);

            // 有关连接请求的日志信息，包括 NetworkId 分配的 NetworkId 以及生成的 prefab 的名称。
            Debug.Log($"'{worldName}' setting connection '{networkId.Value}' to in game, spawning a Ghost '{prefabName}' for them!");

            // 实例化 prefab
            var player = commandBuffer.Instantiate(prefab);
            // 将实例化的 prefab 与连接的 client 的分配的 NetworkId 关联
            commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId.Value});

            // 将播放器添加到链接的 entity 组中，以便在断开连接时自动销毁
            commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup{Value = player});

            // 给每个 NetworkId 自己的生成位置：
            {
                var isEven = (networkId.Value & 1) == 0;
                const float halfCharacterWidthPlusHalfPadding = .55f;
                const float spawnStaggeredOffset = 0.25f;
                var staggeredXPos = networkId.Value * math.@select(halfCharacterWidthPlusHalfPadding, -halfCharacterWidthPlusHalfPadding, isEven) + math.@select(-spawnStaggeredOffset, spawnStaggeredOffset, isEven);
                var preventZFighting = -0.01f * networkId.Value;

                commandBuffer.SetComponent(player, LocalTransform.FromPosition(new float3(staggeredXPos, preventZFighting, 0)));
            }
            commandBuffer.DestroyEntity(reqEntity);
        }
        commandBuffer.Playback(state.EntityManager);
    }
}
