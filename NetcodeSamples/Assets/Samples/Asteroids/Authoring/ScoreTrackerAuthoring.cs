using UnityEngine;
using Unity.Entities;
using UnityEngine.UI;

public class ScoreTrackerAuthoring : MonoBehaviour
{
    class Baker : Baker<ScoreTrackerAuthoring>
    {
        public override void Bake(ScoreTrackerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.ManualOverride); // 因此它不包含在相关性半径检查中，因为我们默认 Score 在默认情况下始终是相关的。
            AddComponent(entity, new AsteroidScore());
        }
    }
}
