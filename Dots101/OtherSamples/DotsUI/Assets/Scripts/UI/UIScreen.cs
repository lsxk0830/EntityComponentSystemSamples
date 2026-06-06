using Unity.Entities;
using UnityEngine.UIElements;
using UnityEngine;

namespace Unity.DotsUISample
{
    // UI 元素的基类
    // 继承自 ScriptableObject，以便实例可以存储在 UnityObjectRef 中
    public abstract class UIScreen : ScriptableObject
    {
        public const string k_VisibleClass = "screen-visible";
        public const string k_HiddenClass = "screen-hidden";

        public EntityCommandBuffer entityCommandBuffer { get; set; }

        public VisualElement RootElement { get; set; }

        public void Show()
        {
            RootElement.AddToClassList(k_VisibleClass);
            RootElement.BringToFront();
            RootElement.RemoveFromClassList(k_HiddenClass);
            RootElement.SetEnabled(true);
            RootElement.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            RootElement.AddToClassList(k_HiddenClass);
            RootElement.RemoveFromClassList(k_VisibleClass);
            RootElement.style.display = DisplayStyle.None;
        }
    }
}