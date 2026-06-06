# TransparencyOrdering

此示例演示了透明的 entities 排序。

<img src="../../../../READMEimages/TransparencyOrdering.PNG" width="600">

## 它显示了什么？

这个 scene 包含很多半透明的立方体，需要按照从后到前的顺序渲染
正确的结果。Entities Graphics package 通过 `DepthSorted_Tag` 标签 component 支持此功能，该标签
当 Entity 烘焙时，会自动添加到带有透明材质的 Entities 中。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，确保子场景已关闭
2. 转至：**窗口 > Entities > Hierarchy**
3. 选择层次结构最底层的 Entities 之一
4. 观察 Entity 应具有 `DepthSorted_Tag` 标签 component
