using Unity.Mathematics;

namespace Unity.Physics.Authoring
{
    public abstract class BaseJoint : BaseBodyPairConnector
    {
        public bool EnableCollision;
        public float3 MaxImpulse = float.PositiveInfinity;

        void OnEnable()
        {
            // 包含，因此勾选框出现在 Editor 中
        }
    }
}
