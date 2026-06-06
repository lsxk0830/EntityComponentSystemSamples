using System.Collections;
using System.Collections.Generic;
using Unity.Transforms;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    private Vector3 m_direction;

    private static float TooClose = 3.0f;
    private static float TooFar = 500.0f;
    private static float SpeedFactor = 1.0f;

    // Start 在第一帧更新之前调用
    void Start()
    {
        // 只要远离原点
        m_direction = transform.position.normalized;
    }

    // 每帧调用一次更新
    void Update()
    {
        float distance = transform.position.magnitude;
        if (distance >= TooFar || distance <= TooClose)
        {
            m_direction = -m_direction;
        }

        // 使速度与距离相当，以使 LOD 转换发生得更快
        float speed = SpeedFactor * distance;

        Vector3 translation = speed * Time.deltaTime * m_direction;
        transform.Translate(translation, Space.World);
    }
}
