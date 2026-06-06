using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.DotsUISample
{
    // 在物理之后更新，以便我们可以获得准确的玩家位置
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial struct QuestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<Cauldron>();
            state.RequireForUpdate<GameData>();
            state.RequireForUpdate<UIScreens>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var game = SystemAPI.GetSingletonRW<GameData>();
            var quest = game.ValueRO.Quest.Value;

            if (game.ValueRO.State != GameState.Questing)
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            Entity playerEntity = SystemAPI.GetSingletonEntity<Player>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xz;

            const float minDistanceSq = 9f;

            // 检查与大锅的互动以提交任务
            if (quest.HasAllItems)
            {
                Entity cauldronEntity = SystemAPI.GetSingletonEntity<Cauldron>();
                float3 cauldronPosition = SystemAPI.GetComponentRO<LocalTransform>(cauldronEntity).ValueRO.Position;

                // 如果靠近大锅
                if (math.distancesq(playerPosition, cauldronPosition.xz) < minDistanceSq)
                {
                    var proximityEventEntity = ecb.CreateEntity();
                    ecb.AddComponent<Event>(proximityEventEntity);
                    ecb.AddComponent(proximityEventEntity, new CauldronProximityEvent
                    {
                        Position = cauldronPosition
                    });

                    if (GameInput.Interact.WasPerformedThisFrame())
                    {
                        quest.Done = true;
                    }
                }
            }

            // 拿起收藏品
            {
                Entity closestEntity = Entity.Null;
                float3 closestCollectablePos = float3.zero;
                float closestDistanceSq = float.MaxValue;
                CollectableType collectableType = default;

                // 找到距离玩家最近的收藏品
                foreach (var (transform, collectable, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>, RefRO<Collectable>>()
                             .WithEntityAccess())
                {
                    float distancesq = math.distancesq(transform.ValueRO.Position.xz, playerPosition);
                    if (distancesq < minDistanceSq && distancesq < closestDistanceSq)
                    {
                        closestDistanceSq = distancesq;
                        closestEntity = entity;
                        collectableType = collectable.ValueRO.Type;
                        closestCollectablePos =  transform.ValueRO.Position;
                    }
                }

                if (closestEntity != Entity.Null)
                {
                    // 指示玩家非常接近收藏品的事件
                    var proximityEventEntity = ecb.CreateEntity();
                    ecb.AddComponent<Event>(proximityEventEntity);
                    ecb.AddComponent(proximityEventEntity, new CollectableProximityEvent
                    {
                        Position = closestCollectablePos
                    });

                    // 检查玩家输入以拾取收藏品
                    if (GameInput.Interact.WasPerformedThisFrame())
                    {
                        var collectableCountBuf = SystemAPI.GetSingletonBuffer<CollectableCount>();
                        var inventoryItemBuf = SystemAPI.GetSingletonBuffer<InventoryItem>();

                        var idx = (int)collectableType;
                        collectableCountBuf[idx] = new CollectableCount
                        {
                            Count = collectableCountBuf[idx].Count + 1
                        };
                        inventoryItemBuf.Add(new InventoryItem { Type = collectableType });

                        var eventEntity = ecb.CreateEntity();
                        ecb.AddComponent<Event>(eventEntity);
                        ecb.AddComponent<PickupEvent>(eventEntity);

                        state.EntityManager.DestroyEntity(closestEntity);
                    }
                }
            }

            // 检查是否收集了所有物品
            if (!quest.HasAllItems)
            {
                var counts = SystemAPI.GetSingletonBuffer<CollectableCount>();

                var hasAll = true;
                foreach (var item in quest.Items)
                {
                    if (counts[(int)item.Type].Count < item.GoalCount)
                    {
                        hasAll = false;
                        break;
                    }
                }

                quest.HasAllItems = hasAll;
            }
        }
    }
}