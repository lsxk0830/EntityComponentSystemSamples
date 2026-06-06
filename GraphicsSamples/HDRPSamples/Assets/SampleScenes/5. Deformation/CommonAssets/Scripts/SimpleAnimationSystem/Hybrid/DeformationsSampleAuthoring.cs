using UnityEngine;

// 添加此 component 将为 trigger 转换 system 添加
// components 将根据变换数据计算 SkinMatrices。
internal class DeformationsSampleAuthoring : MonoBehaviour
{
    [Tooltip("Override the color in Deformation Material")]
    public Color Color = new Color(.9f, .3f, .5f);
}
