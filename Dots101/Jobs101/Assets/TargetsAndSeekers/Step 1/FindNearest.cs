using UnityEngine;

namespace Tutorials.Jobs.Step1
{
    public class FindNearest : MonoBehaviour
    {
        public void Update()
        {
            // 找到最近的目标。
            // 比较距离时，比较便宜
            // 距离的平方，因为这样做
            // 避免计算平方根。
            Vector3 nearestTargetPosition = default;
            float nearestDistSq = float.MaxValue;
            foreach (var targetTransform in Spawner.TargetTransforms)
            {
                Vector3 offset = targetTransform.localPosition - transform.localPosition;
                float distSq = offset.sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestTargetPosition = targetTransform.localPosition;
                }
            }

            Debug.DrawLine(transform.localPosition, nearestTargetPosition);
        }
    }
}
