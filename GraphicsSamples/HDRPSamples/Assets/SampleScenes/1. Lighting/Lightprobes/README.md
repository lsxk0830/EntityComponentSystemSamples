# 光探针

此示例演示了 lightprobe 对 Entities 的支持。

<img src="../../../../READMEimages/Lightprobes.PNG" width="600">

## 它显示了什么？

scene 包含使用不同光照着色器的球体。此示例使用光探针照亮 scene。

## 如何使用这个示例 scene？

1. 在 Hierarchy 中，选择子场景
2. 在 Inspector 中，单击“**打开**”
3. 请注意，在 subscene 中，灯光是静态的，而球体不是静态的
4. 转到：**窗口 > 渲染 > 照明**
5. 在 Scene 选项卡中，确保启用 **烘焙全局照明**。配置光照贴图设置，单击**生成光照**
6. 保存 scene 和子场景，然后关闭子场景
