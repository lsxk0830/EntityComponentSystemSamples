# 发行说明
该项目的所有显着更改都将记录在该文件中。

## [Physics 1.3.0 后的示例项目]
### 变化
* 更改了关节 - 游行演示，将 BreakJoint1 MaxImpulse 从 (10,10,10) 更改为 (2,2,2)。

### 修复

## [Physics 1.3.0 示例项目]
### 变化
* 添加了演示 scene `Modify Collider Geometry.unity`，演示运行时 collider 变形。在演示中，各种类型的 colliders 的几何形状通过新的 `ColliderBakeTransformAuthoring` component 进行修改，它允许您创建循环动画，在此期间游戏对象的 collider 及其视觉效果会随着时间的推移而变形。这是通过使用 `Collider.BakeTransform` 函数来实现的，该函数将仿射变换应用于 collider，在此过程中相应地旋转、平移、缩放和剪切其几何形状。
* 在自定义 authoring components 中添加了对电机弹簧和阻尼的支持。在电机演示中，现在使用这些字段。
* 带电机的演示 scenes 现在提供了如何使用内置 components 或自定义 components 进行创作的示例

### 修复
* 修改运行时 - 碰撞过滤器演示：修复了立方体的渲染网格被错误更改为球体的错误。
* 修复了在 OnDestroy 可以正确处置 collider blob 之前处置 subscene 导致的 ColliderBakeTransformSystem 中的内存泄漏

## [Physics 1.2.0 示例项目]
### 变化
* 重新组织了示例，清理了各种示例的代码和 scenes。
* 添加了演示 scene `Modify Collider Geometry.unity`，演示运行时 collider 变形。在演示中，各种类型的 colliders 的几何形状通过新的 `ColliderBakeTransformAuthoring` component 进行修改，它允许您创建循环动画，在此期间游戏对象的 collider 及其视觉效果会随着时间的推移而变形。这是通过使用 `Collider.BakeTransform` 函数来实现的，该函数将仿射变换应用于 collider，在此过程中相应地旋转、平移、缩放和剪切其几何形状。
* 在自定义 authoring components 中添加了对电机弹簧和阻尼的支持。在电机演示中，现在使用这些字段。

## [Physics 1.1.0 示例项目]
### 变化
* 添加了演示 scene `4e. Single Ragdoll`，其中包含使用内置物理 [Ragdoll Wizard](https://docs.unity3d.com/Manual/wizard-RagdollWizard.html) 创建的 GameObject 布娃娃，并使用 Unity Physics 模拟一侧的 DOTS 和内置 GameObject 物理上的另一个。
* 添加了演示 scene `5m. Collider Modifications` 以展示如何在运行时创建网格 colliders
* 在 `Physics_MeshCollider.cs` 中为 BlobAssetReference MeshCollider.Create（UnityEngine.Mesh，CollisionFilter，材质）添加了新的 API
* 在 `Physics_MeshCollider.cs` 中为 BlobAssetReference MeshCollider.Create（UnityEngine.Mesh.MeshData，CollisionFilter，材质）添加了新的 API
* 在 `Physics_MeshCollider.cs` 中为 BlobAssetReference MeshCollider.Create（UnityEngine.Mesh.MeshDataArray，CollisionFilter，材质）添加了新的 API
* 为了清楚起见，将 `4d. Ragdoll` scene 重命名为 `4d. Ragdolls`。
* `Mouse Hover Authoring` component 得到了改进，现在当鼠标悬停在物理 entities 上时也可以工作，其渲染 components 位于子 entity 中。因此，之前禁用的 `Mouse Hover Authoring` component 现在已在 `4d. Ragdolls` scene 中启用。
* 添加了新物理 component：强制唯一 Collider Authoring 以允许内置 colliders 在 baking 期间变得唯一
* 更新了 `5b. Change Box Collider Size` scene 以使用新的 Physics 强制唯一 Collider component
* `5. Modify` 部分现在有一个名为 `5g. Runtime Collider Modifications` 的新部分。
* 添加了新的演示：`5g1. Change Collider Material - Bouncy Boxes`。该演示实例化包含唯一 colliders 的prefab，并在运行时修改 collider blob 数据
* 之前名为 `5g. Change Collider Filter` 演示已重命名为 `5g2. Unique Collider Blob Sharing`。本次 demo 的重点是如何共享 collider 数据，而不是如何修改 `CollisionFilter`。
* `5g2. Unique Collider Blob Sharing` 演示使用 `SpawnExplosionAuthoring.cs` 中的新 `MakeUnique` 方法。新的 colliders 不再需要手动管理。
* 将 `5m. Collider Modification` 演示移至 `5g. Runtime Collider Modifications` 部分，并将其重命名为 `5g3. Runtime Collider Creation`
* 通过使用内置物理 authoring components（灰色）以及现有的创建的附加跷跷板扩展了 `Motion Properties - Mass` 演示
使用自定义物理 authoring components 的跷跷板（黄色）。
* 添加了新的演示：`5g4. Runtime Collision Filter Modification`。该演示展示了如何在运行时修改 CollisionFilter。

### 修复
* 通过不初始化以下文件中不需要的 systems 提高了 system 启动效率：
  * `Tests/Animation/Scripts/AnimationPhysicsSystem.cs`
  * `Tests/MultipleWorlds/ClientServer/Scripts/ServerPhysicsSystem.cs`
  * `Tests/MultipleWorlds/CustomPhysicsGroup/MultiWorldMousePicker.cs`
  * `Tests/MultipleWorlds/CustomPhysicsGroup/UserWorldCreator.cs`
* `Tests/SamplesTest/CreatePhysicsPerformanceTests.unity`：修复了 system 的 OnUpdate 方法中索引超出范围的错误
* 修复了演示 scene 5c 中的错误。更改 Collider 类型，其中 collider 类型未更新'
* 修复了演示 3a 中使用的 `QueryTester.cs` 中的错误。所有命中距离测试/3b。铸造测试/3c。最近命中距离测试，其中调试绘制线未绘制查询结果。任何使用 PhysicsDebugDisplaySystem 绘图功能的 system 都必须更新 PhysicsDebugDisplayGroup 内的此 system。

## [Physics 1.0.0 示例项目]

* 添加了 _ 自定义 Physics 创作 _ 体验的自动导入：<br> 围绕 `PhysicsBodyAuthoring` 和 `PhysicsShapeAuthoring` components 构建的自定义物理 authoring 体验不再嵌入在 Unity Physics package API。相反，它作为名为 _Custom Physics Authoring_ 的 Unity Physics package 示例提供。该示例现在自动导入到 `Assets/Samples/Unity Physics/<package version>/Custom Physics Authoring` 下的 PhysicsSamples 项目中。如果要更新 PhysicsSamples 项目中的 Unity Physics package，请确保在更新后使用 _Samples_ 选项卡下的 Package Manager 导入此示例的新版本，然后从项目中删除此示例的先前版本。

## [Physics 1.0.0-pre.15 示例项目]
### 变化
* 在 `Assets/Demos/4. Joints/4c. Motors` 下添加了新电机接头的演示 scenes。
* 添加了 `SimulationValidationAuthoring` component 用于物理行为验证测试​​，可用于确认关节的行为符合预期，并且如果需要，刚体处于静止状态。
* 为 `Assets` 文件夹中的以下 PlayMode 测试添加了模拟验证：
  * ZXQBTW 若果果 FZXQ
  * `Tests/JointTest/LimitedHinge/LimitedHingeSub.unity`
  * `Tests/JointTest/Prismatic.unity`
  * `Tests/JointTest/Hinge.unity`
  * `Demos/4. Joints/4a. Joints Parade/4a. Joints Parade SubScene.unity`
  * `Demos/4. Joints/4c. Motors/4c3. Linear Velocity Motor.unity`
  * `Demos/4. Joints/4c. Motors/4c4. Angular Velocity Motor.unity`
* 将 systems 的基类从 SystemBase 更改为 ISystem
### 修复
  * `Demos/5. Modify/5f. Change Surface Velocity.unity` 修复了 DisplayConveyorBeltJob 中的依赖关系
### 已知问题

## [Physics 0.51.0-preview.32 示例项目] - 2022-06-30
### 变化
* Packages：
  * 将 com.unity.physics 从 `0.50.0-preview.24` 更新为 `0.51.0-preview.32`
  * 将 com.havok.physics 从 `0.50.0-preview.24` 更新为 `0.51.0-preview.32`
  * 将 com.unity.collections 从 `1.2.3-pre.24` 更新为 `1.3.1`
  * 将 com.unity.entities 从 `0.50.0-preview.24` 更新为 `0.51.0-preview.32`
  * 将 com.unity.jobs 从 `0.50.0-preview.8` 更新为 `0.51.0-preview.32`
  * 将 com.unity.rendering.hybrid 从 `0.50.0-preview.24` 更新为 `0.51.0-preview.32`
  * 将 com.unity.render-pipelines.universal 从 `10.8.1` 更新为 `12.1.7`
* Editor：
  * 将 Editor 版本从 `2020.3.30f1` 更新为 `2021.3.4f1`
* 文档：
  * 更新的文档文件夹已重命名为 READMEimages，Samples.md 已重命名为 README.md。
* 由于 `2021.3.4f1` 出现一些渲染错误，着色器 PhysicsStatic 材质已被禁用，新材质会替代着色器，同时错误会得到修复。
* 将 NativeHashMap 更新为 NativeParallelHashMap。

### 修复
### 已知问题

## [Physics 0.50.0-preview.24 示例项目] - 2022-03-25
### 变化
* 项目：
  * 将项目名称从 UnityPhysicsSamples 更新为 PhysicsSamples。
  * 将 urp 添加到项目中。
  * 升级着色器，使其与 urp 兼容。
  * 清理 Shaders/PhysicsStaticInputs.hlsl 和 Shaders/PhysicsStatic.shader + 添加材质。
* Packages：
  * 将 com.unity.physics 更新为 `0.50.0-preview.24`
  * 将 com.havok.physics 更新为 `0.50.0-preview.24`
  * 将 com.unity.collections 更新为 `1.2.3-pre.24`
  * 将 com.unity.entities 更新为 `0.50.0-preview.24`
  * 将 com.unity.jobs 更新为 `0.50.0-preview.8`
  * 将 com.unity.rendering.hybrid 更新为 `0.50.0-preview.24`
  * 将 com.unity.render-pipelines.universal 更新为 `10.8.1`

### 修复
  * 所有演示的固定材料
  * 基于 collider 网格边界修复了布娃娃零件比例
  * 固定材质颜色

### 已知问题

## [Physics 0.10.0-预览版示例项目] - 2021-12-31
### 变化
* 添加了 `ImmediatePhysicsWorldStepper` 实用程序类，用于在当前线程上立即运行物理模拟。
* 将池演示中的 `ProjectIntoFutureOnCueSystem` 更改为使用 `ImmediatePhysicsWorldStepper`。
* 添加了 Assets/Tests/MultipleWorlds/Animation/Animation scene，展示了如何使用单独的 `PhysicsWorld` 来模拟少量非关键主体以用于动画目的（角色的马尾辫和刀鞘）。`DriveAnimationBodySystem` 在默认和侧面物理 world（角色的头部和躯干）之间同步关键主体，并为非关键主体准备数据（马尾辫和刀鞘的位置和速度校正）。`AnimationPhysicsSystem` 执行单线程 `PhysicsWorld` 构建、模拟并导出到 ECS ZXQFAQBNAYBKE​​UKZXQ。该演示展示了物理动画马尾辫的 2 种变体：带位置和速度校正（绿色）和不带位置和速度校正（蓝色）。
* 添加了 Assets/Tests/MultipleWorlds/ClientServer/ClientServer scene 作为简化的 client-server 示例，其中包含游戏关键的 ghost 主体（存在于两个主体上） server 和 client) 和 client 仅在本地模拟的主体，以增加游戏的视觉吸引力。
首先模拟 server 物理场 world 中的实体（将是基于真实用例中的 server 数据的 predicted），然后镜像到默认物理场 world 中。默认物理 world 包含 ghost 和 client-only 实体，其中 ghost 实体由其 server 对应实体驱动（游戏对象通过 Server Entity 链接）驱动器 Ghost 主体 Authoring 脚本中的设置）。`DriveGhostBodySystem` 通过位置或速度驱动 ghost 主体，比率（位置与速度）由 client ghost 主体游戏对象上的 Drive Ghost Body Authoring 脚本中的一阶增益设置确定。`ServerPhysicsSystem` 是构建、模拟和导出 server 物理 world 的关键 system。
该演示展示了不同的运动杆（仅限 client，动态和运动学 ghost）旋转并推动各种球体（仅限 client，动态和运动学 ghosts 由位置、速度等驱动）。
### 修复
### 已知问题

## [Physics 0.9.0-preview.4 示例项目] - 2021-05-19
### 变化
* 依赖关系
  * 将数据流图更新为 `0.21.0-preview.1`
  * 将 IDE Visual Studio 更新为 `2.0.7`
  * 更新的混合渲染器 `0.13.0-preview.30`
### 修复
### 已知问题

## [Physics 0.8.0-预览版示例项目] - 2021-03-26

### 变化
* 依赖关系
  * 将 DOTS Editor 从 `0.13.0-preview` 更新为 `0.14.0-preview.1`
  * 将混合渲染器从 `0.12.0-preview.42` 更新为 `0.13.0-preview.17`
  * 将 Burst 从 `1.4.4` 更新为 `1.5.0`

* 重构了 `CollisionEvent` 和 `TriggerEvent` 演示，使状态事件更加清晰。
  * 要将有状态事件添加到您自己的项目中，请复制 [Stateful](Assets/Demos/2.%20Setup/2d.%20Events/Scripts/Stateful) 文件夹中的所有脚本。
  * 然后，如果你想要发生碰撞事件：
    1. 使用 `PhysicsShapeAuthoring` component 的 **“碰撞响应”** 属性的 **“碰撞引发碰撞事件”选项，以及
    2. 将 `StatefulCollisionEventBufferAuthoring` component 添加到 entity （并选择是否应计算详细信息）
    3. 运行时，从 `StatefulCollisionEvent`s 的动态缓冲区中读取
  * 或者，如果您想要 trigger 事件：
    1. 使用 `PhysicsShapeAuthoring` component 的 **“碰撞响应”** 属性的 **“引发 Trigger 事件”选项，并且
    2. 将 `StatefulTriggerEventBufferAuthoring` component 添加到该 entity
    3. 运行时，从 `StatefulTriggerEvent`s 的动态缓冲区中读取

## [Physics 0.7.0-preview.3 示例项目] - 2021-02-24

### 变化

* 依赖关系
  * 将数据流图从 `0.18.0-preview.3` 更新为 `0.20.0-preview.4`
  * 将 DOTS Editor 从 `0.12.0-preview.4` 更新为 `0.13.0-preview`
  * 将混合渲染器从 `0.10.0-preview.21` 更新为 `0.12.0-preview.42`
  * 将编码从 `0.1.0-preview.17` 更新为 `0.1.0-preview.20`

* 添加了新的 `MouseHoverAuthoring` 脚本，以帮助突出显示额外的 `CompoundCollider.Child.Entity` 字段和 `PhysicsRenderEntity` component 数据。查看简单的 `1b. Representations` 和更复杂的 `2a2. Collider Parade - Advanced` 演示 scene 作为设置变化的示例。
* 添加了 `Tests/VelocityClipping/VelocityClippingStacking` 演示，以展示已删除的 `SimulationCallbacks.Phase.PostSolveJacobians` 的简单替换。这个演示做了一个非常简单的速度剪辑，重点不是展示堆叠（`Tests/Stacking` 演示涵盖了），而是展示如何在不使用回调本身的情况下实现与之前的回调类似的效果。
* 添加了 `Tests/SystemScheduling/SchedulingSample`，它显示了如何安排与物理运行时数据（存储在 PhysicsWorld 中）交互的 jobs。

### 修复

* 修复了将多个 joint components 添加到同一游戏对象时创建重复 joint Entities 的错误。

### 已知问题

## [Physics 0.6.0-preview.3 示例项目] - 2021-01-18

### 变化
* 依赖关系
  * 将数据流图从 `0.18.0-preview.3` 更新为 `0.19.0-preview.5`
  * 将 DOTS Editor 从 `0.9.0-preview.1` 更新为 `0.12.0-preview.4`
  * 将混合渲染器从 `0.10.0-preview.21` 更新为 `0.11.0-preview.40`
* 添加了 `5g. Change Collider Filter` 示例，显示爆炸用例以及碰撞过滤器在这种情况下的重要性。在 `SpawnExplosionAuthoring` 中将力设置为 0 会导致性能峰值，因为当过滤器设置为默认值时，许多物体会相互渗透。

### 修复

### 已知问题

## [Physics 0.5.1-preview.2 示例项目] - 2020-10-14

### 变化

* 依赖关系
  * 将混合渲染器从 `0.7.0-preview.24` 更新为 `0.10.0-preview.21`
  * 将数据流图从 `0.16.0-preview.3` 更新为 `0.18.0-preview.1`
* 现在所有演示的目标帧率为 60HZ。

### 修复

### 已知问题

## [Physics 0.5.0-预览版示例项目] - 2020-09-15

### 变化

* 将 `LimitDOF` joint 移至核心 Unity Physics package

### 修复

* 修复了 `FreeHingeJoint.Create` authoring 脚本未设置 `BodyFrame.PerpendicularAxis` 的问题

### 已知问题

* `RaycastCar` 演示存在有时会产生 NaN 值的问题。

## [Physics 0.4.1-预览版示例项目] - 2020-07-28

### 变化

* 添加了简单的 `1b. Respresentations` 示例，突出显示 world 的图形和物理表示。
* 添加了简单的 `1c. Conversion` 示例，以及从 GameObjects（数据和逻辑）移动到 DOTS 的工作示例。
    * 新的 `1c1. GameObjects GravityWell` 示例 - 显示数据转换有效，但逻辑转换无效。
    * 新的 `1c2. Covertible GravityWell` 示例 - 显示数据和逻辑转换。
    * 新的 `1c3. DOTS GravityWell` 示例 - 显示相同的 scene，但没有 GameObject 转换。
* 更新了 `2a2. Collider Parade - Advanced` 示例，其中包含涉及多个图形网格和/或多个物理形状的额外设置示例。
* 更新了 `4. Joints\Ragdoll` 演示，以使用新的布娃娃 joint 界面并允许修改 joint 限制。
* 添加了 `5d. Change Velocity` 演示，显示局部速度变化。
* 添加了 `5f. Change Surface Velocity` 示例，突出显示没有移动部件的传送带用例。

## [Physics 0.4.0-preview.5 示例项目] - 2020-06-18

### 变化

* 更新了以下 packages：
    * 添加了数据流图 `0.14.0-preview.2`
    * 添加了 DOTS Editor `0.7.0-preview.1`
    * 从 `0.4.0-preview.8` 到 `0.5.1-preview.18` 的混合渲染器
* `PrismaticJoint` 示例不再包含 `MinDistanceFromAxis` 或 `MaxDistanceFromAxis` 参数。
* `RagdollJoint` 示例现在采用 (-90, 90) 范围内的垂直限制，而不是 (0, 180)。
* 添加了 `5d. Kinematic Motion` 以说明移动运动体的不同方式。
* 改进了 trigger 事件和碰撞事件的可用性：
	* 事件（StatefulTriggerEvent 和 StatefulCollisionEvent）具有指示两个物体重叠或碰撞状态的状态：
		* Enter - 物体在前一帧中没有重叠或碰撞，但在当前帧中却重叠或碰撞
		* 保持 - 物体在前一帧中确实重叠或碰撞，在当前帧中也是如此
		* 退出 - 物体在前一帧中确实重叠或碰撞，但在当前帧中却没有重叠或碰撞
	* 事件（StatefulTriggerEvent 和 StatefulCollisionEvent）存储在引发它们的 entities 的 DynamicBuffers 中
	* 重新设计了以下演示以演示新的 trigger 事件方法：
		* `2d1a. Triggers - Change Material`
		* `2d1b. Triggers - Portals`
		* `2d1c. Triggers - Force Field`
	* 添加 `2d2a. Collision Events - Event States` 以演示新的碰撞事件方法。
* 暴露 trigger 事件和 CharacterController 的碰撞事件
* CharacterController 主体现在在其主体上使用 `CollisionResponse.None` 碰撞响应，以避免报告来自物理引擎的重复碰撞/trigger 事件。
* `Demos/2. Setup/2b. Motion Properties/2b1. Motion Properties - Mass`、`Tests/Pyramids` 和 `Tests/Stacking` 下的一组演示现在展示新的求解器稳定性功能/优点和缺点/权衡。

## [Physics 0.3.2-预览版示例项目] - 2020-04-16

### 变化

* 更新了以下 packages：
    * 删除了 DOTS Editor
    * 从 `0.3.4-preview.24` 到 `0.4.0-preview.8` 的混合渲染器
    * 输入 System 从 `1.0.0-preview.5` 到 `1.0.0-preview.6`
* 修复了角色控制器隧道问题。
* 如果在 BasePhysicsDemo 或派生类中捕获异常，则使独立播放器退出（退出代码为 1）。

## [Physics 0.3.1-预览版示例项目] - 2020-03-19

### 变化
* 修复了潜在的角色控制器隧道问题。
* 删除了轻量级 RP package。

## [Physics 0.3.0-预览版示例项目] - 2020-03-12

### 变化

* Joint 示例现在可以正确地将新的 joint entity 添加到 prefab 的链接 entity 组，以便它们将与 prefab 的其余部分一起实例化。
* 将 `StiffSpringJoint` 重命名为 `LimitedDistanceJoint` 以反映 API 中的更改。
* 更新了以下 packages
    * 输入 System 从 `0.9.6-preview` 到 `1.0.0-preview.5`
    * 轻量级 RP 从 `7.1.6` 到 `7.1.7`
    * DOTS Editor 从 `0.2.0-preview` 到 `0.3.0-preview`
    * 从 `0.3.3-preview.11` 到 `0.3.4-preview.24` 的混合渲染器
* 角色控制器改进
    * `CharacterControllerUtilities.CheckSupport()` 现在使用 collider 转换
    * 角色现在不会与触发器发生碰撞，而是引发 trigger 事件
    * 修复了斜率等于 MaxSlope 的支撑检查问题
    * 修复了没有支撑平面时返回的支撑状态

## [Physics 0.2.5-预览版示例项目] - 2019-12-04

### 变化
* 添加了 `CharacterControllerAuthoring.MaxMovementSpeed` 以避免穿透恢复带来的大速度。
* 改进了角色控制器在多个对象或网格三角形上行走的行为：
    * 修复了失去支持状态的问题
    * 修复了与陡坡相互作用导致不稳定的问题
    * 改善渗透恢复
* 字符控制器现在使用本机列表而不是本机数组进行约束和 query 命中以避免大量预分配。
* 行星重力示例现在可以正确随机化轨道物体的质量。

## [Physics 0.2.4-预览版示例项目] - 2019-09-19

### 变化

* 修复了角色控制器中 collider 的问题（假定有序命中，可能会穿过对象）。
* 当使用低于 `0b5` 的 Unity `2019.3` 打开项目时，可能需要将 *Lightweight RP* package (com.unity.render-pipelines.lightweight) 更新到版本 `7.0.1` 并重新导入 `Assets/Common` 文件夹。


## [Physics 0.2.2-预览版示例项目] - 2019-09-06

### 变化

* 角色控制器的变化包括：
    - 角色控制器演示 scenes 合并为单个 scene。
    - 角色控制器现在可以使用任何碰撞过滤器，而不是之前被迫将其设置为“无”。
    - 默认最大攀爬坡度现在为 60 度。
    - 角色控制器现在可以使用任何 collider，而不是以前被迫使用胶囊。
    - 修复了当数据位于多个 chunks 中时调度字符控制器 jobs 的竞争条件问题。
    - 现在支持角色之间的简单碰撞（不互相穿过）。
    - 添加了默认值为 2cm 的 KeepDistance，以避免角色卡在狭窄的通道中。
    - 用户输入现在投射到支撑表面上，将其速度纳入输入中。
    - CharacterControllerUtilities.CheckSupport() 已弃用。使用新的 CheckSupport() 方法输出表面信息。
    - CharacterControllerUtilities.CollideAndIntegrate() 已弃用。使用新的 CollideAndIntegrate() 方法，该方法将 numConstraints 作为输入。
* 菜单加载器 scene 现在支持键盘和控制器输入。控制器输入现在可以跨平台正确映射。控件已更新为以下内容：
    * 键盘/鼠标
        * UI：
            * 箭头键进行导航
            * [返回]选择
        * 角色控制器
            - [WASD] 移动
            - 鼠标外观
            - [空格]跳跃
            - [LMB/Ctrl] 进行拍摄
        * 车辆
            - [WS] 加速/减速
            - [AD] 转向
            - [左/右]箭头查看
            - [左/RightBracket] 切换车辆
    * 控制器：
        * UI
            * 方向键导航
            * [西] 选择
        * 角色控制器
            - 左摇杆移动
            - 右摇杆外观
            - [南]跳
            - 【右 Trigger】进行拍摄
        * 车辆
            - [左/右 Trigger]加速/减速
            - 左摇杆即可转向
            - 右摇杆箭头查看
            - [左/右保险杠]切换车辆

### 使用不同的 Unity Editor 版本打开项目

* 要使用 Unity `2019.3.0b1` 或更高版本打开项目，目前需要升级以下 packages：
   - *混合渲染器* (com.unity.rendering.hybrid) 必须升级到 `0.1.1-preview`
   - *轻量级 RP* (com.unity.render-pipelines.lightweight) 必须更新到版本 `7.0.1`
   - 如果在打开 Unity Editor 的情况下完成此操作，可能需要重新导入材料。


## [Physics 0.2.0-预览版示例项目] - 2019-07-18

### 变化

* `5a. Change Motion Type` 演示展示了如何交换刚体的运动类型。
* `5b. Change Collider Size` 演示展示了如何动态调整球体 collider 的大小。
* `5c. Change Collider Type` 演示展示了物体的 collider 在立方体和球体之间变化。
* 删除了 `4c. Newton's Cradle` 演示
* 删除了 `4d. Abacus` 演示
* 添加了加载器 scene 以更轻松地测试设备上的所有示例。


## [Physics 0.1.0-预览版示例项目] - 2019-05-31

### 变化

* `1. Hello World` 现在使用凸包来表示字母。
* `2b1. Motion Properties - Mass` 在预期未来堆叠改进方面展示了许多跷跷板。
* `2b6. Motion Properties - Inertia Tensor` 添加了无限质量动态对象的示例 - i.e 锁定旋转。
* `2c2. Material Properties - Restitution` 显示了恢复模型的变化，复杂的物体在恢复值较高时弹跳更多。
* `2c3. Material Properties - Collision Filters` 显示潜在碰撞对象之间的过滤设置
* `2d1. Events - Triggers` 显示了一个简单的 trigger 用例，该用例在体积内部时更改物体的重力分形。
    - 更高级的 trigger 用例捕获状态，例如进入、重叠和离开 trigger 卷。
    - `2d1. Triggers - Change Material` 显示一组触发器，当实体进入 trigger 体积时，这些触发器会更改实体材质。
    - `2d2. Triggers - Portals` 显示了一组触发器，可将物体变换到新的位置和方向，同时保留局部速度。
    - `2d3. Triggers - Force Field` 显示了 trigger 体积的作用以及穿过 world 的龙卷风。
* `2d2. Events - Contacts` 显示了一个简单的碰撞事件，用于向与标记的“排斥器”物体碰撞的任何物体施加脉冲。
* `4a. Joints Parade` 演示显示 Prismatic Joint 现在锁定旋转。
* `4b. Limit DOF` 演示展示了新的 Limit DOF Joint，锁定主体旋转和平移到各个轴。
* `4c. Newton's Cradle` 演示展示了如何欺骗碰撞响应来创建桌面玩具。
* `4d. Abacus` 展示了一款桌面玩具，作为极限 DOF Joint 的极限测试。
* `5. Modify` 演示已更改为使用新的 jobify 方法来修改模拟数据。

* 鼠标拾取行为现在忽略静态和 Trigger 主体，这说明了自定义点击收集器。
* Joint 编辑器现在将连接体偏移锁定到局部偏移，如 UI 的建议。
* 调试显示更改包括：
    - Colliders 显示为实心。
    - Collider 边缘可以独立于实体 Colliders 显示。
    - Trigger 事件在重叠的实体之间绘制连接线。
    - 碰撞事件会在碰撞体之间的中途产生冲量。

* 添加了各种测试来确认 API 更改，并作为 WIP 和即将发布的 Havok Physics 版本的比较。


## [Physics 0.0.2-预览版示例项目] - 2019-04-08

### 变化

* 角色控制器现在有一个最大坡度 constraint 以避免爬坡太陡。
* 角色控制器现在执行另一个 query 来检查是否可以到达解算器返回的位置。


## [Physics 0.0.1-预览版示例项目] - 2019-03-12

* 初始 package 版本和示例项目。
