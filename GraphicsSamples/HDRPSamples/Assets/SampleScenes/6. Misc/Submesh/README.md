# 子网格

此示例演示了如何通过 Entities Graphics 使用具有多个子网格的网格。

<img src="../../../../READMEimages/Submesh.PNG" width="600">

## 它显示了什么？

scene 包含一个 GameObject，该 scene 具有一个带有三个子网格的网格和三个独立的材质，一个
对于每个子网格。由于 Entities 仅支持每个 Entity 的单一材质，因此这些类型的 GameObjects 将被烘焙
分成几个单独的 Entities，每种材料一个。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，确保子场景已关闭
2. 转至：**窗口 > Entities > Hierarchy**
3. 导航至子场景
4. 展开下面有层次结构的 Entity
5. 观察有 3 个 Entities，都是由同一个 authoring GameObject 烘焙而成