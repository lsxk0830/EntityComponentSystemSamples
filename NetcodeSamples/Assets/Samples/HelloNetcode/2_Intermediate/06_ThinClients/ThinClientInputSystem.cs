using System;
using System.Diagnostics;
using Unity.Entities;
using Unity.NetCode;

namespace Samples.HelloNetcode
{
    // 此 system 应该仅在薄 client worlds 中使用 run （并且仅在正常 client worlds 中进行正常输入处理）
    [UpdateInGroup(typeof(HelloNetcodeInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class ThinClientInputSystem : SystemBase
    {
        int m_FrameCount;
        uint m_WorldIndex;

        protected override void OnCreate()
        {
            RequireForUpdate<EnableThinClients>();
            RequireForUpdate<EnableSpawnPlayer>();
            RequireForUpdate<NetworkId>();

            // 给每一个瘦子 client 一些随意性
            var rand = Unity.Mathematics.Random.CreateFromIndex((uint)Stopwatch.GetTimestamp());
            m_FrameCount = rand.NextInt(100);
            m_WorldIndex = UInt32.Parse(World.Name.Substring(World.Name.Length - 1));
        }

        protected override void OnUpdate()
        {
            // 检查连接是否尚未设置命令目标，如果没有，则创建它（这是虚拟瘦 client 播放器）
            if (SystemAPI.TryGetSingleton<CommandTarget>(out var commandTarget) && commandTarget.targetEntity == Entity.Null)
                CreateThinClientPlayer();

            byte left, right, up, down, jump;
            left = right = up = down = jump = 0;

            // 向随机方向移动
            var state = (int)(SystemAPI.Time.ElapsedTime+m_WorldIndex) % 4;
            switch (state)
            {
                case 0: left = 1; break;
                case 1: right = 1; break;
                case 2: up = 1; break;
                case 3: down = 1; break;
            }

            // 每 100 帧跳转一次
            if (++m_FrameCount % 100 == 0)
            {
                jump = 1;
                m_FrameCount = 0;
            }

            // 薄 clients 不会生成任何东西，因此只有一个 PlayerInput component
            foreach (var inputData in SystemAPI.Query<RefRW<CharacterControllerPlayerInput>>())
            {
                inputData.ValueRW = default;
                if (jump == 1)
                    inputData.ValueRW.Jump.Set();
                if (left == 1)
                    inputData.ValueRW.Movement.x -= 1;
                if (right == 1)
                    inputData.ValueRW.Movement.x += 1;
                if (down == 1)
                    inputData.ValueRW.Movement.y -= 1;
                if (up == 1)
                    inputData.ValueRW.Movement.y += 1;
            }
        }

        void CreateThinClientPlayer()
        {
            // 创建虚拟 entity 来存储精简 clients 输入
            // 当使用 IInputComponentData 时，entity 将需要输入 component 及其生成的
            // buffer，与本地连接 ID 建立的 GhostOwner 以及最后的
            // CommandTarget 需要手动设置。
            var ent = EntityManager.CreateEntity();
            EntityManager.AddComponent<CharacterControllerPlayerInput>(ent);

            var connectionId = SystemAPI.GetSingleton<NetworkId>().Value;
            EntityManager.AddComponentData(ent, new GhostOwner() { NetworkId = connectionId });
            EntityManager.AddComponent<InputBufferData<CharacterControllerPlayerInput>>(ent);

            // NOTE: server 还必须手动设置瘦 client 播放器的命令目标
            // 即使播放器 prefab（和普通 clients）上使用了自动命令目标，请参阅
            // SpawnPlayerSystem。
            SystemAPI.SetSingleton(new CommandTarget { targetEntity = ent });
        }
    }
}
