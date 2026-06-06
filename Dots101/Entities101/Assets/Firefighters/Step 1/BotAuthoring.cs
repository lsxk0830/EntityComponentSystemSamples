using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tutorials.Firefighters
{
    public class BotAuthoring : MonoBehaviour
    {
        private class Baker : Baker<BotAuthoring>
        {
            public override void Bake(BotAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent<Bot>(entity);
            }
        }
    }

    public struct Bot : IComponentData
    {
        public BotState State;
        public float2 TargetPos;   // 机器人要移动到的地方。
        public float2 LinePos;     // 机器人在空闲时站立的位置。
        public Entity NextBot;     // 排队的下一个机器人（将把桶传递给该机器人）。
        public bool IsDouser;      // 机器人是在最后灭火的机器人。
        public bool IsFiller;      // 是位于填充桶的行末端的机器人。还负责取桶。
        public Entity Bucket;      // 机器人携带的桶。
        public bool IsCarrying;    // 如果携带水桶则为真。
        public Entity Team;        // 机器人所属的团队。

        public readonly bool IsMoving()
        {
            return !(State == BotState.IDLE
                     || State == BotState.CLAIM_BUCKET
                     || State == BotState.WAIT_IN_LINE
                     || State == BotState.FILL_BUCKET);
        }
    }

    public enum BotState
    {
        IDLE,
        CLAIM_BUCKET,
        MOVE_TO_BUCKET,
        FILL_BUCKET,
        PASS_BUCKET,
        DOUSE_FIRE,
        MOVE_TO_LINE,
        WAIT_IN_LINE,
    }
}
