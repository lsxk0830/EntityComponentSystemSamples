使用 `StreamingStressTest` scene 在运行时测试物理 colliders 的创建和删除。

当您第一次打开此 scene 时，您需要基于具有物理特性的 GameObject 立方体创建 entities。选择每个名为 _SubScene__* 的活动 subscenes，然后在检查器中单击“重建 Entities 缓存”。对于大型 subscenes，这可能需要 10 分钟或更长时间。每当您修改 subscene 时，您都需要再次重建其 entity 缓存。

子 scenes _SubScene_StaticColliders_Fixed_Small_ 和 _SubScene_StaticColliders_Fixed_Large_ 向主 scene 分别添加了大约 5000 个和 46000 个静态立方体。较大的 scene 默认情况下处于禁用状态。

_SubScene_StaticColliders_Streaming_ 包含大约 5000 个静态立方体，并附加了一个脚本，请求每个 `FramesBetweenStramingOperations = 10` 帧加载或卸载此 subscene。这模拟了游戏 world 的部分内容在游戏过程中流入和流出的场景。

/!\ 立方体将在游戏视图中闪烁，因为它们经常流入和流出。
