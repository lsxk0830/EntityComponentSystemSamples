using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace KickBall
{
    // 当 server 接收到 GoInGameRequest 时，准备好 client 来接收 ghosts，生成玩家角色，并删除 RPC 请求
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct GoInGameServerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EntityPrefabs>();
            var query = SystemAPI.QueryBuilder().WithAll<GoInGameRequest, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var playerPrefab = SystemAPI.GetSingleton<EntityPrefabs>().Player;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (requestSource, requestEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<GoInGameRequest>()
                         .WithEntityAccess())
            {
                ecb.AddComponent<NetworkStreamInGame>(requestSource.ValueRO.SourceConnection);

                // 生成玩家
                {
                    var networkId = SystemAPI.GetComponent<NetworkId>(requestSource.ValueRO.SourceConnection);

                    var player = ecb.Instantiate(playerPrefab);
                    ecb.SetComponent(player, new GhostOwner { NetworkId = networkId.Value });

                    // 根据玩家的网络 ID 设置颜色。
                    var rand = Random.CreateFromIndex((uint)networkId.Value);
                    ecb.SetComponent(player, new Color { Value = new float4(rand.NextFloat3(), 0) });

                    // 将播放器添加到链接的 entity 组中，以便在断开连接时自动销毁
                    ecb.AppendToBuffer(requestSource.ValueRO.SourceConnection, new LinkedEntityGroup { Value = player });
                }

                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
