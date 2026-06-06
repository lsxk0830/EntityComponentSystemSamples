using Unity.Entities;
using UnityEngine;

// 此类设置 run 演示的 FPS 的上限。
// 它用作 JobTempAlloc 问题的解决方法 CI 并使得
// 更容易测量性能。
public partial struct InitializeSamplesSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // 目前，当 job 需要超过 4 个帧才能完成时，也会引发 JobTempAlloc，与分配无关。
        // 将目标帧速率设置为 60 意味着给每个帧更多的时间来完成，因此 jobs 将在不到 4 帧的时间内完成。
        // 此外，由于 FPS 会更低，因此会有更多具有物理特性的帧，从而使性能测量变得更容易。
        Application.targetFrameRate = 60;

        // 禁用更新，因为 system 与 OnUpdate 方法无关
        state.Enabled = false;
    }
}
