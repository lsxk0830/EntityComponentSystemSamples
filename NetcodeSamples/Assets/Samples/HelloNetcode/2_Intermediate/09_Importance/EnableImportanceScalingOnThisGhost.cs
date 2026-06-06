using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Samples.HelloNetcode
{
    public class EnableImportanceScalingOnThisGhost : MonoBehaviour
    {
        private class BarrelAuthoringBaker : Baker<EnableImportanceScalingOnThisGhost>
        {
            public override void Bake(EnableImportanceScalingOnThisGhost authoring)
            {
                // Note: 这依赖于将 `GhostDistancePartitioningSystem.AutomaticallyAddGhostDistancePartitionShared` 设置为 false。
                // 注 2：理想情况下，这应该是运行时的事情，因为我们在 client 上不需要这个 component。
                AddSharedComponent(GetEntity(TransformUsageFlags.Dynamic), default(GhostDistancePartitionShared));
            }
        }
    }
}

