# MatrixPrevious

此示例演示了如何移动多个 Entities 并支持 HDRP 运动矢量，使 Entities 对运动模糊做出正确反应

<img src="../../../../READMEimages/MatrixPrevious.PNG" width="600">

## 它显示了什么？

scene 包含球体 GameObjects，其父级为 **球** GameObject。每个球体都包含附加的球体 ID Authoring component，MoveBallsSystem.cs 脚本会使用该球体 ID 迭代每个球体 id 并在每帧转换所有球体 entities。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择任何以 **Balls** GameObject 为父级的球体
2. 在 Inspector 中，请注意附加了球体 ID Authoring components
3. 进入播放模式。所有球体均烘焙至 Entities 并通过运动模糊轨迹更改其位置
