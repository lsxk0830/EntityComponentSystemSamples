using UnityEngine;
using UnityEngine.UI;

namespace HelloCube.GameObjectSync
{
    // “目录”充当引用 GameObject prefab和托管对象的中心位置。
    // 然后，Systems 可以从一处获取对所有托管对象的引用。
    // （在一个大型项目中，如果转储，您可能需要多个“目录”
    // 所有托管对象都集中在一个地方变得太笨拙。）

    public class Directory : MonoBehaviour
    {
        public GameObject RotatorPrefab;
        public Toggle RotationToggle;
    }
}
