using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Scenes;

public struct LoadNextLevelCommand : IRpcCommand { }

// 用于跟踪 scene 何时完成加载，加载开始时将其添加到此处，卸载时将其删除
public struct TrackedSubScene : IBufferElementData
{
    public Entity SceneEntity;
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ServerLevelTracker : SystemBase
{
    private LevelLoader m_Loader;

    protected override void OnCreate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LevelSync_Bootstrap")
            Enabled = false;
        m_Loader = World.GetExistingSystemManaged<LevelLoader>();
    }

    protected override void OnUpdate()
    {
        // 使用下一级加载命令处理来自 client 的 RPCs
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var shouldLoadNext = false;
        foreach (var (level, req, entity) in SystemAPI.Query<LoadNextLevelCommand, ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            UnityEngine.Debug.Log("Server received command to load next level");
            shouldLoadNext = true;
            ecb.DestroyEntity(entity);
        }
        ecb.Playback(EntityManager);
        if (shouldLoadNext)
        {
            var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
            var levelCount = m_Loader.Levels.GetUniqueKeyArray(Allocator.Temp).Item2;
            levelState.CurrentLevel = ++levelState.CurrentLevel % levelCount;
            SystemAPI.SetSingleton(levelState);
            UnityEngine.Debug.Log($"[{World.Name}] trigger loading of level {levelState.CurrentLevel}");

            // 禁用所有连接上的同步
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            FixedString32Bytes worldName = World.Name;
            foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithEntityAccess().WithAll<NetworkStreamInGame>())
            {
                UnityEngine.Debug.Log($"[{worldName}] disable sync on {netId.Value}");
                commandBuffer.RemoveComponent<NetworkStreamInGame>(entity);
            }
            commandBuffer.Playback(EntityManager);

            m_Loader.UnloadAndLoadNext(levelState.CurrentLevel);

            NetcodeLevelSync.TriggerClientLoadLevel(levelState.CurrentLevel, World);
        }
    }
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial class ClientLevelTracker : SystemBase
{
    private LevelLoader m_Loader;

    protected override void OnCreate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LevelSync_Bootstrap")
            Enabled = false;
        m_Loader = World.GetExistingSystemManaged<LevelLoader>();
    }

    protected override void OnUpdate()
    {
        var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
        if (levelState.State == LevelSyncState.LevelLoadRequest)
            m_Loader.UnloadAndLoadNext(levelState.NextLevel);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class LevelLoader : SystemBase
{
    private NativeParallelMultiHashMap<int, Level> m_Levels;
    // 当使用手动加载/卸载各个关卡时，需要禁用自动同步流程
    private bool m_DisableLevelSync;

    public struct Level {
        public Hash128 guid;
        [Flags]
        public enum Flags : int
        {
            None = 0,
            Client = 1 << 0,
            Server = 1 << 1,
        }
        public Flags flags;
    }
    public NativeParallelMultiHashMap<int, Level> Levels => m_Levels;

    protected override void OnCreate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LevelSync_Bootstrap")
        {
            Enabled = false;
            return;
        }
        m_Levels = new NativeParallelMultiHashMap<int, Level>(2, Allocator.Persistent);

        RequireForUpdate<LevelSyncStateComponent>();
        if (SystemAPI.TryGetSingletonEntity<LevelSyncStateComponent>(out var levelSyncEntity))
            EntityManager.AddBuffer<TrackedSubScene>(levelSyncEntity);
    }

    protected override void OnDestroy()
    {
        if (m_Levels.IsCreated)
            m_Levels.Dispose();
    }

    protected override void OnUpdate()
    {
        if (m_DisableLevelSync)
            return;

        if (m_Levels.Count() == 0)
        {
            if (SystemAPI.TryGetSingleton<SceneListData>(out var levelData))
            {
                m_Levels.Add(0, new Level(){flags = Level.Flags.Client | Level.Flags.Server, guid = levelData.Level1A});
                m_Levels.Add(0, new Level(){flags = Level.Flags.Client | Level.Flags.Server, guid = levelData.Level1B});
                m_Levels.Add(1, new Level(){flags = Level.Flags.Client | Level.Flags.Server, guid = levelData.Level2A});
                m_Levels.Add(1, new Level(){flags = Level.Flags.Client | Level.Flags.Server, guid = levelData.Level2B});
            }
        }
        CheckLevelLoading();
    }

    protected void CheckLevelLoading()
    {
        var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
        if (levelState.State == LevelSyncState.LevelLoadInProgress)
        {
            var levelStateEntity = SystemAPI.GetSingletonEntity<LevelSyncStateComponent>();
            var trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(levelStateEntity);

            bool allScenesLoaded = true;
            for (int i = 0; i < trackedScenes.Length; ++i)
            {
                if (!SceneSystem.IsSceneLoaded(World.Unmanaged, trackedScenes[i].SceneEntity))
                    allScenesLoaded = false;
            }
            if (allScenesLoaded)
            {
                // 通知 levelsync 逻辑我们已准备好进行下一步
                levelState.State = LevelSyncState.LevelLoaded;
                SystemAPI.SetSingleton(levelState);

                //UnityEngine.Debug.Log($"[{World.Name}] finished loading {EntityManager.GetName(m_SceneList[m_CurrentLevel])} GUID={m_Levels[m_CurrentLevel]}");
                UnityEngine.Debug.Log($"[{World.Name}] finished loading {trackedScenes.Length} subscenes");
            }
        }
    }

    public void UnloadAndLoadNext(int nextLevel)
    {
        var levelStateEntity = SystemAPI.GetSingletonEntity<LevelSyncStateComponent>();
        var trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(levelStateEntity);
        var sceneEntities = trackedScenes.ToNativeArray(Allocator.Temp);
        for (int i = 0; i < sceneEntities.Length; ++i)
        {
            var sceneEntity = sceneEntities[i].SceneEntity;
            UnityEngine.Debug.Log($"[{World.Name}] Unloading {sceneEntity}");
            if (SceneSystem.IsSceneLoaded(World.Unmanaged, sceneEntity))
                SceneSystem.UnloadScene(World.Unmanaged, sceneEntity);
        }
        trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(levelStateEntity);
        trackedScenes.Clear();

        var levelState = SystemAPI.GetSingleton<LevelSyncStateComponent>();
        levelState.State = LevelSyncState.LevelLoadInProgress;
        levelState.CurrentLevel = nextLevel;
        SystemAPI.SetSingleton(levelState);

        foreach (var level in m_Levels.GetValuesForKey(nextLevel))
        {
            Entity sceneEntity = Entity.Null;

            if (level.flags == Level.Flags.Client)
            { // 仅限 GO

                // 这实际上不会做任何事情或改变运行此代码的测试的结果。
                // 删除这个，因为使用 LoadAsGOScene 的功能已经消失了。将其保留在此处是为了在Netcode更新或删除它时保留一些上下文。
                //var loadParams = new SceneSystem.LoadParameters {Flags = SceneLoadFlags.LoadAsGOScene};
                //sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, level.guid, loadParams);
            }
            else
            {
                sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, level.guid);
            }

            if (sceneEntity != Entity.Null)
            {
                trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(levelStateEntity);
                trackedScenes.Add(new TrackedSubScene(){SceneEntity = sceneEntity});
            }
            UnityEngine.Debug.Log($"[{World.Name}] Loading '{World.EntityManager.GetName(sceneEntity)}' GUID={level.guid}");
        }
    }

    public void LoadLevel(int number)
    {
        m_DisableLevelSync = true;
        foreach (var level in m_Levels.GetValuesForKey(number))
        {
            var lookupScene = SceneSystem.GetSceneEntity(World.Unmanaged, level.guid);
            if (lookupScene != Entity.Null && SceneSystem.IsSceneLoaded(World.Unmanaged, lookupScene))
                continue;

            Entity sceneEntity = Entity.Null;

            if (level.flags == Level.Flags.Client)
            { // 仅限 GO
                // 这实际上不会做任何事情或改变运行此代码的测试的结果。
                // 删除这个，因为使用 LoadAsGOScene 的功能已经消失了。将其保留在此处是为了在Netcode更新或删除它时保留一些上下文。
                //var loadParams = new SceneSystem.LoadParameters {Flags = SceneLoadFlags.LoadAsGOScene};
                //sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, level.guid, loadParams);
            }
            else if ((level.flags & Level.Flags.Server) == Level.Flags.Server)
            {
                sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, level.guid);
            }
            else
            {
                continue;
            }

            UnityEngine.Debug.Log($"[{World.Name}] Loading '{World.EntityManager.GetName(sceneEntity)}' GUID={level.guid}");

            NetcodeLevelSync.SetLevelState(LevelSyncState.LevelLoadInProgress, World);
            var trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(SystemAPI.GetSingletonEntity<LevelSyncStateComponent>());
            trackedScenes.Add(new TrackedSubScene(){SceneEntity = sceneEntity});
        }
    }

    public void UnloadLevel(int number)
    {
        foreach (var level in m_Levels.GetValuesForKey(number))
        {
            if (level.flags == Level.Flags.Client) continue; // GO Scene

            // atm subscenes 必须完全消除（而不仅仅是卸载）才能正确进行 trigger 生成前清理
            UnityEngine.Debug.Log($"[{World.Name}] unloading {level.guid}");
            var sceneEntity = SceneSystem.GetSceneEntity(World.Unmanaged, level.guid);
            SceneSystem.UnloadScene(World.Unmanaged, level.guid);

            var trackedScenes = SystemAPI.GetBuffer<TrackedSubScene>(SystemAPI.GetSingletonEntity<LevelSyncStateComponent>());
            for (int i = trackedScenes.Length - 1; i >= 0; i--)
            {
                if (trackedScenes[i].SceneEntity == sceneEntity)
                    trackedScenes.RemoveAtSwapBack(i);
            }
        }
    }

    public void ToggleSync()
    {
        var conQuery = World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        var cons = conQuery.ToEntityArray(Allocator.Temp);
        var conIds = conQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
        for (int i = 0; i < cons.Length; ++i)
        {
            if (World.EntityManager.HasComponent<NetworkStreamInGame>(cons[i]))
            {
                UnityEngine.Debug.Log($"[{World}] Disable sync on {conIds[i].Value}");
                World.EntityManager.RemoveComponent<NetworkStreamInGame>(cons[i]);
            }
            else
            {
                UnityEngine.Debug.Log($"[{World}] Enable sync on {conIds[i].Value}");
                World.EntityManager.AddComponent<NetworkStreamInGame>(cons[i]);
            }
        }
    }
}
