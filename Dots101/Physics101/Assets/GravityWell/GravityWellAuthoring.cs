using Unity.Entities;
using UnityEngine;

namespace GravityWell
{
    public class GravityWellAuthoring : MonoBehaviour
    {
        public float OrbitPos;  // 轨道 pos 为以 rad 为单位的角度
        public class Baker : Baker<GravityWellAuthoring>
        {
            public override void Bake(GravityWellAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new GravityWell
                {
                    OrbitPos = authoring.OrbitPos
                });
            }
        }
    }

    public struct GravityWell : IComponentData
    {
        public float OrbitPos;   // 以弧度为单位
    }
}

