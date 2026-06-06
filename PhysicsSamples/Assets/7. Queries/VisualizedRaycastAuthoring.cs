using Unity.Entities;
using UnityEngine;
using UnityEngine.Assertions;

namespace Query
{
    // 为 raycast 配置可视化的 authoring component
    [DisallowMultipleComponent]
    public class VisualizedRaycastAuthoring : MonoBehaviour
    {
        [Tooltip("The length of the desired raycast")]
        public float RayLength;

        [Header("Visualization Elements")]
        [Tooltip("An object that will be scaled along its z-axis to visualize the full length of the ray cast")]
        public Transform FullRay;

        [Tooltip(
            "An object that will be scaled along its z-axis to visualize the distance from the ray start to the hit position of the raycast, if the raycast is successful")]
        public Transform HitRay;

        [Tooltip("An object that will be snapped to the hit position of the raycast, if the raycast is successful")]
        public Transform HitPosition;

        class Baker : Baker<VisualizedRaycastAuthoring>
        {
            public override void Bake(VisualizedRaycastAuthoring authoring)
            {
                var fullRayEntity = GetEntity(authoring.FullRay, TransformUsageFlags.Dynamic);
                var hitRayEntity = GetEntity(authoring.HitRay, TransformUsageFlags.Dynamic);
                var hitPosEntity = GetEntity(authoring.HitPosition, TransformUsageFlags.Dynamic);

                Assert.IsTrue(fullRayEntity != Entity.Null);
                Assert.IsTrue(hitRayEntity != Entity.Null);
                Assert.IsTrue(hitPosEntity != Entity.Null);
                Assert.IsTrue(authoring.RayLength != 0.0f);

                VisualizedRaycast visualizedRaycast = new VisualizedRaycast
                {
                    RayLength = authoring.RayLength,

                    FullRayEntity = fullRayEntity,
                    HitRayEntity = hitRayEntity,
                    HitPositionEntity = hitPosEntity,
                };

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, visualizedRaycast);
            }
        }
    }

    public struct VisualizedRaycast : IComponentData
    {
        public float RayLength;

        public Entity FullRayEntity;
        public Entity HitRayEntity;
        public Entity HitPositionEntity;
    }
}
