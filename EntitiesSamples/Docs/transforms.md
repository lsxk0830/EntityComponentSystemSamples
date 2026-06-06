# Transform components 和 systems

[`LocalTransform`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.LocalTransform.html) component 表示 entity 的变换，并且 entity 变换层次结构由三个附加 components 形成：

- [`Parent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.Parent.html) component 存储 entity 父级的 id。
- [`Child`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.Child.html) 动态缓冲区 component 存储 entity 子级的 id。
- [`PreviousParent`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.PreviousParent.html) component 存储 entity 父级 id 的副本。

要修改转换层次结构：

- 将 `Parent` component 添加为 entity 的父级。
- 删除 entity 的 `Parent` component 以取消其父关系。
- 设置 entity 的 `Parent` component 以更改其父级。

[`ParentSystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.ParentSystem.html) 更新 `Child` 和 `PreviousParent` components 以确保：

- 每个具有父级的 entity 都有一个引用父级的 `PreviousParent` component。
- 每个具有一个或多个子项的 entity 都有一个引用其所有子项的 `Child` 缓冲区 component。

| ⚠ IMPORTANT |
| :- |
| 尽管您可以安全地*读取* entity 的 `Child` 缓冲区 component，但您不应该直接修改它。仅通过设置 entities' `Parent` components 来修改转换层次结构。 |

每一帧，[`LocalToWorldSystem`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.LocalToWorldSystem.html) 计算每个 entity 的 world 空间变换（来自 `LocalTransform` components entity 及其祖先）并将其分配给 entity 的 [`LocalToWorld`](https://docs.unity3d.com/Packages/com.unity.entities@latest?subfolder=/api/Unity.Transforms.LocalToWorld.html) component。

| &#x1F4DD; NOTE |
| :- |
| `Entity.Graphics` systems 读取 `LocalToWorld` component，但不读取任何其他变换 components，因此 `LocalToWorld` 是唯一的变换 component entity 需要渲染。 |