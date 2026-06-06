using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace HelloCube.FixedTimestep
{
    public class SliderHandler : MonoBehaviour
    {
        public Text SliderValueText;

        public void OnSliderChange()
        {
            float fixedFps = GetComponent<Slider>().value;

            // WARNING: 在重要的项目中，访问 World.DefaultGameObjectInjectionWorld 是一种破坏模式。
            // GameObject 与 ECS 的交互通常应该朝另一个方向进行：而不是
            // GameObjects 访问 ECS 数据和代码，ECS systems 应访问 GameObjects。

            var fixedSimulationGroup = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedSimulationGroup != null)
            {
                // 组时间步长可以在运行时设置：
                fixedSimulationGroup.Timestep = 1.0f / fixedFps;

                // 还可以检索当前时间步长：
                SliderValueText.text = $"{(int)(1.0f / fixedSimulationGroup.Timestep)} updates/sec";
            }
        }
    }
}
