using Unity.Entities;
using UnityEngine;

namespace Tutorials.Firefighters
{
    public struct Team : IComponentData
    {
        public Entity Filler;
        public Entity Bucket;
        public int NumFiresDoused;
    }

    // 团队中的所有机器人（包括填充者和遮光者）按传递顺序，从填充者开始
    public struct TeamMember : IBufferElementData
    {
        public Entity Bot;
    }

    // 用作旗帜，表明团队需要重新定位
    public struct RepositionLine : IComponentData, IEnableableComponent { }

    public struct Heat : IBufferElementData
    {
        public float Value;
    }

    public class BotAnimation : IComponentData
    {
        public GameObject AnimatedGO; // 渲染和动画的 GO
    }
}
