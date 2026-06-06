using Unity.Entities;
using UnityEngine;

namespace ActivationPlates
{
    public class ZoneAuthoring : MonoBehaviour
    {
        public ZoneType Type;

        private class Baker : Baker<ZoneAuthoring>
        {
            public override void Bake(ZoneAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);

                AddComponent(entity, new Zone
                {
                    Type = authoring.Type,
                    State = ZoneState.Outside
                });
            }
        }
    }

    public struct Zone : IComponentData
    {
        public ZoneType Type;
        public ZoneState State;

        // 当此 trigger 发出 trigger 事件时上次物理更新的计数
        // （ulong 使翻转不再是问题）
        public ulong LastPhysicsUpdateCount;

        // 上次占用时经过的时间
        public float LastTriggerTime;
    }

    public enum ZoneState
    {
        Inside,
        Outside,
        Enter,    // 现在在里面，但在之前的更新中在外面
        Exit,     // 现在在外面，但在之前的更新中在里面
    }

    public enum ZoneType
    {
        OneTime,
        Continuous,
        Reenterable,
        OnExit,
    }
}