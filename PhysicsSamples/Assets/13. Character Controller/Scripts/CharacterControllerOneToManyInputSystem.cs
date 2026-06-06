using Unity.Burst;
using Unity.Entities;

// 此输入 system 只是应用相同的字符输入
// scene 中每个角色控制器的信息
[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(DemoInputGatheringSystem))]
public partial struct CharacterControllerOneToManyInputSystem : ISystem
{
    [BurstCompile]
    partial struct CharacterControllerOneToManyInputSystemJobParallel : IJobEntity
    {
        public CharacterControllerInput Input;

        void Execute(ref CharacterControllerInternalData ccData)
        {
            ccData.Input.Movement = Input.Movement;
            ccData.Input.Looking = Input.Looking;
            // 跳转请求可能不会在此帧上处理，因此记录它而不是匹配输入状态
            if (Input.Jumped != 0)
                ccData.Input.Jumped = 1;
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CharacterControllerInput>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new CharacterControllerOneToManyInputSystemJobParallel
        {
            // 读取用户输入
            Input = SystemAPI.GetSingleton<CharacterControllerInput>()
        }.ScheduleParallel(state.Dependency);
    }
}
