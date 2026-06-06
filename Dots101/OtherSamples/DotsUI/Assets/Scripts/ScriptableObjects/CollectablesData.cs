using System;
using UnityEngine;

namespace Unity.DotsUISample
{
    [CreateAssetMenu(fileName = "New Collectables List", menuName = "Collectables")]
    public class CollectablesData : ScriptableObject
    {
        // 列表中收藏品的数量和顺序
        // 必须与 CollectableType 枚举的成员匹配
        public CollectableItem[] Collectables;
    }

    [Serializable]
    public struct CollectableItem
    {
        public string Name;
        public Sprite Icon;
    }
}