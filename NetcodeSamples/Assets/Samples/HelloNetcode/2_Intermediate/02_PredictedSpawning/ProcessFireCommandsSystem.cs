using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    // 专门处理火灾输入事件类型
    [UpdateInGroup(typeof(HelloNetcodePredictedSystemGroup))]
    [BurstCompile]
    public partial struct ProcessFireCommandsSystem : ISystem
    {
        private ComponentLookup<LocalTransform> m_TransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnablePredictedSpawning>();
            state.RequireForUpdate<CharacterControllerPlayerInput>();
            state.RequireForUpdate<GrenadeSpawner>();
            m_TransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 由于这仅处理产生手榴弹的火灾输入，因此我们只想预测一次产生
            // （或者我们会在一个实例中生成大量手榴弹，因为一次蜱虫的 prediction 可以 run 多次）
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            var netDebug = SystemAPI.GetSingleton<NetDebug>();
            var config = SystemAPI.GetSingleton<GrenadeConfig>();
            var commandBuffer = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var grenadePrefab = SystemAPI.GetSingleton<GrenadeSpawner>().Grenade;
            var localToWorldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var time = state.WorldUnmanaged.Time;
            m_TransformLookup.Update(ref state);

            state.CompleteDependency();
            var originalGranadeScale = m_TransformLookup[grenadePrefab].Scale;

            foreach (var (character, inputBuffer, anchorPoint) in SystemAPI.Query<CharacterAspect,
                         DynamicBuffer<InputBufferData<CharacterControllerPlayerInput>>, RefRO<AnchorPoint>>().WithAll<Simulate>())
            {
                // 我们必须从 SecondaryFire InputEvent（存储了这个精确增量）的计数值中获取要生成的手榴弹数量。
                // 为什么？因为：
                // - 用户可能会丢弃数据包，因此用户点击计数器可以多次递增（表示他们已经点击了之前的“尚未确认”标记）。
                // - 用户可能会在部分刻度中重复单击。想象一下 SimulationTickRate 为 10。点击速度可能超过每 100 毫秒一次，因此我们必须在一次模拟滴答中计数 2。
                // - server 可能会将刻度一起批处理（由于性能问题）。
                var grenadesToSpawn = character.Input.SecondaryFire.Count;
                if (grenadesToSpawn <= 0) continue;

                // 现在获取 ABSOLUTE 计数器值，因为我们稍后需要它：
                inputBuffer.GetDataAtTick(networkTime.ServerTick, out var currentInput);

                // 在真实的游戏中，您将通过游戏设计选择来限制此类玩家操作的速率。
                // E.g。无论用户按下按钮的频率如何，投掷手榴弹都会具有最大射速。
                // 但对于此示例，我们将说明 EXACTLY 每次右键单击时会生成一枚手榴弹（忽略按住的按键）。
                const int maxGrenadesPerPlayerPerServerTick = 5;
                if (grenadesToSpawn > maxGrenadesPerPlayerPerServerTick)
                {
                    netDebug.LogWarning($"Clamping player input, as they're attempting to spawn {grenadesToSpawn} grenades in one tick (max: {maxGrenadesPerPlayerPerServerTick})!");
                    grenadesToSpawn = maxGrenadesPerPlayerPerServerTick;
                }

                // 批量实例化：
                using var grenadeEntities = new NativeArray<Entity>((int) grenadesToSpawn, Allocator.Temp);
                commandBuffer.Instantiate(grenadePrefab, grenadeEntities);
                if (netDebug.LogLevel == NetDebug.LogLevelType.Debug)
                {
                    netDebug.DebugLog($"[{state.WorldUnmanaged.Name}] Spawned {grenadesToSpawn} grenades on {networkTime.ServerTick.ToFixedString()}, fr:{UnityEngine.Time.frameCount}, {networkTime.ToFixedString()}!");
                }

                for (int spawnId = 0; spawnId < grenadesToSpawn; spawnId++)
                {
                    var grenadeEntity = grenadeEntities[spawnId];

                    // Note: 由于 component 存储自上一个刻度以来的增量，我们必须获取绝对计数器
                    // value (used for classification) from the actual buffer. Combining them, we can reconstruct previous
                    // SpawnId，使我们能够将 server 生成与 clients predicted 生成完美匹配。
                    uint secondaryFireCount = (uint) (currentInput.InternalInput.SecondaryFire.Count - spawnId);

                    // 生成点在播放器上嵌套了 3 层（插槽->启动器->spawnPoint），但元素 0 是根 entity
                    var spawnPointEntity = anchorPoint.ValueRO.SpawnPoint;
                    var grenadeSpawnPosition = localToWorldTransformLookup[spawnPointEntity].Position;
                    var grenadeSpawnRotation = localToWorldTransformLookup[spawnPointEntity].Rotation;
                    var granadeSpawnScale = originalGranadeScale;

                    // 通过设置向前方向的物理线速度和配置的初始速度来发射手榴弹
                    var initialVelocity = new PhysicsVelocity();
                    initialVelocity.Linear = localToWorldTransformLookup[anchorPoint.ValueRO.SpawnPoint].Forward * config.InitialVelocity;

                    // 将生成位置偏移其速度 * 一小部分刻度（基于 spawnID），这样，
                    // 如果我们在一帧中生成多个手榴弹，它们会相互偏移。
                    var spawnIdFraction = (float) spawnId / maxGrenadesPerPlayerPerServerTick;
                    grenadeSpawnPosition += (spawnIdFraction * time.DeltaTime) * initialVelocity.Linear;

                    // 将生成位置设置在手榴弹生成位置并旋转，但在 world 坐标中，因为它不是玩家的子级
                    commandBuffer.SetComponent(grenadeEntity, LocalTransform.FromPositionRotationScale(grenadeSpawnPosition, grenadeSpawnRotation, granadeSpawnScale));
                    commandBuffer.SetComponent(grenadeEntity, initialVelocity);

                    var grenadeData = new GrenadeData() {DestroyTimer = (float) time.ElapsedTime + config.BlastTimer};

                    // 为该特定本地生成设置生成 ID，以便稍后可以在分类 system 中使用它
                    // 需要包括所有者的网络 ID，因为每个人的柜台/spawnId 从 1 开始
                    grenadeData.SpawnId = (uint) character.OwnerNetworkId << 16 | secondaryFireCount;
                    commandBuffer.SetComponent(grenadeEntity, grenadeData);

                    // 设置所有者，以便 prediction 可以正常工作（很重要，直到它被 interpolated 版本取代）
                    commandBuffer.SetComponent(grenadeEntity, new GhostOwner {NetworkId = character.OwnerNetworkId});
                }
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
}
