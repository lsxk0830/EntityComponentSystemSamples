using System;
using UnityEngine;

public class ExperimentalWarning : MonoBehaviour
{
    void Awake()
    {
#if UNITY_6000_3_OR_NEWER && NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
        GameObject.Destroy(this.gameObject);
#endif
    }

    void OnGUI()
    {
        // 根据实验定义，示例什么也没做。对于检查这些内容的用户来说，它可能会显示为错误。警告他们缺少关键部分
        // 使示例工作。
        // 这位于特定示例程序集之外，因为这些整个程序集可能有一个定义 constraint
        bool isU6 = false;
        bool ghostBridgeEnabled = false;
        #if UNITY_6000_3_OR_NEWER
            isU6 = true;
        #endif
        #if NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL
            ghostBridgeEnabled = true;
        #endif
        GUI.color = Color.red;
        if (!ghostBridgeEnabled)
        {
            GUI.Label(new Rect(10, 10, 600, 50), "This sample scene is using experimental features and is disabled, please use the NETCODE_GAMEOBJECT_BRIDGE_EXPERIMENTAL define.");
        }

        if (!isU6)
        {
            GUI.Label(new Rect(10, 70, 600, 100), "This sample scene requires Unity 6.3 and above and so is currently disabled.");
        }
    }
}
