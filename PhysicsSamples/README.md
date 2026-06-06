# Unity Physics 示例

*有关 Phyiscs 和 DOTS 入门材料，请参阅[此存储库的主页](../README.md)。*

## 控制

在*游戏*窗口中：

- 鼠标弹簧：用鼠标左键单击并拖动
- 相机旋转：用鼠标右键单击并拖动
- 相机移动：W、A、S、D 键

## 调试显示

许多演示将额外信息显示为 Editor 中的调试显示小控件，例如 Query 演示（光线投射、距离投射等）。这些小工具的显示位于 *Scene* 中，而不是 *Game* 窗口中。因此，如果有疑问，请密切关注两者：

| Scene 查看                                                                  | 游戏视图                                                                   |
|-----------------------------------------------------------------------------|-----------------------------------------------------------------------------|
| ![游戏窗口](READMEimages/closes_hit_scene_view.png)                      | ![游戏窗口](READMEimages/closes_hit_game_view.png)                       |

## Scene 列表

| 类别    | Scene                                                         | 描述                                                         |    等级     |                                                                                  |
|-------------|---------------------------------------------------------------|---------------------------------------------------------------------|:------------:|----------------------------------------------------------------------------------|
| 你好 World | <sub>Hello World.unity</sub>                                  | 用于刚体设置的介绍性 scene                             | 介绍 | ![演示图片](READMEimages/hello_world.gif)                                      |
| 你好 World | <sub>SphereAndBoxColliders.unity</sub>                        | 基本型 colliders                                                     | 介绍 | ![演示图片](READMEimages/representations.gif)                                  |
| 你好 World | <sub>GravityWell.unity</sub>                                  | 简介 scene                                                  | 介绍 | ![演示图片](READMEimages/conversion.gif)                                       |
| 设置       | <sub>2a1。Collider 游行 - Basic.unity</sub>                 | 显示碰撞检测的各种形状的演示                 | 介绍 | ![演示图片](READMEimages/collider_parade_basic.gif)                            |
| 设置       | <sub>2a2。Collider 游行 - Advanced</sub>                    | 演示显示了用于更高级碰撞检测的各种形状   | 介绍 | ![演示图片](READMEimages/collider_parade_advanced.gif)                         |
| 设置       | <sub>2b1。运动属性 - Mass.unity</sub>                | 演示如何使用自定义（黄色）和内置（灰色）authoring components 显式设置质量属性 | 介绍 | ![演示图片](READMEimages/motion_properties_mass.gif) |
| 设置       | <sub>2b2。运动属性 - Velocity.unity</sub>            | 设置初始线速度和角速度                       | 介绍 | ![演示图片](READMEimages/motion_properties_velocity.gif)                       |
| 设置       | <sub>2b3。运动属性 - Damping.unity</sub>             | 演示显示线性和角度阻尼的效果               | 介绍 | ![演示图片](READMEimages/motion_properties_damping.gif)                        |
| 设置       | <sub>2b4。运动属性 - 重力 Factor.unity</sub>      | 演示显示每个身体重力乘数的效果             | 介绍 | ![演示图片](READMEimages/motion_properties_gravity_factor.gif)                 |
| 设置       | <sub>2b5。运动属性 - Mass.unity</sub> 中心      | 演示显示覆盖质心的效果                | 介绍 | ![演示图片](READMEimages/motion_properties_center_of_mass.gif)                 |
| 设置       | <sub>2b6。运动属性 - Inertia Tensor.unity</sub>      | 演示显示覆盖惯性张量的效果                | 介绍 | ![演示图片](READMEimages/motion_properties_inertia_tensor.gif)                 |
| 设置       | <sub>2b7。运动属性 - Smoothing.unity</sub>           | 演示 interpolation 和外推法的效果          | 介绍 | ![演示图片](READMEimages/motion_properties_smoothing.gif)                      |
| 设置       | <sub>2c1。材料特性 - Friction.unity</sub>          | 显示不同摩擦材料值的效果                | 介绍 | ![演示图片](READMEimages/material_properties_friction.gif)                     |
| 设置       | <sub>2c2。材料属性 - Restitution.unity</sub>       | 显示不同恢复值的效果                      | 介绍 | ![演示图片](READMEimages/material_properties_restitution.gif)                  |
| 设置       | <sub>2c3。材料属性 - 碰撞 Filters.unity</sub> | 显示不同碰撞滤镜的效果                       | 介绍 | ![演示图片](READMEimages/material_properties_collision_filters.gif)            |
| 设置       | <sub>2d1。活动 - Triggers.unity</sub>                       | 演示触发器的使用                            | 介绍 | ![演示图片](READMEimages/events_triggers.gif)                                  |
| 设置       | <sub>2d2。活动 - Contacts.unity</sub>                       | 显示不同联系人的效果                                | 介绍 | ![演示图片](READMEimages/events_contacts.gif)                                  |
| Query       | <sub>3a。所有命中距离 Test.unity</sub>                   | 演示显示多个 colliders 之间的距离查询结果 | 介绍 | ![演示图片](READMEimages/all_hits_distance_test.gif)                           |
| Query       | <sub>3b。演员 Test.unity</sub>                                | 显示 collider 投射和光线投射结果的演示        | 介绍 | ![演示图片](READMEimages/cast_test.gif)                                        |
| Query       | <sub>3c。最近命中距离 Test.unity</sub>                | 显示距离查询结果的演示                            | 介绍 | ![演示图片](READMEimages/closest_hit_distance_test.gif)                        |
| Query       | <sub>3d。定制 Collector.unity</sub>                         | raycast 演示                                            | 介绍 | ![演示图片](READMEimages/custom_collector.gif)                                 |
| 关节      | <sub>4a。关节 Parade.unity</sub>                            | 演示展示了一系列 joint 类型                                 | 介绍 | ![演示图片](READMEimages/joints_parade.gif)                                    |
| 关节      | <sub>4b。限制 DOF.unity</sub>                                | 显示限制自由度的效果                       | 介绍 | ![演示图片](READMEimages/limit_dof.gif)                                        |
| 关节      | <sub>4c1。所有电机 Parade.unity</sub>                       | 显示不同电机的演示                                       | 介绍 | ![演示图片](READMEimages/all_motors_parade.gif)                                |
| 关节      | <sub>4c2。位置 Motor.unity</sub>                          | 显示位置电机的演示                                         | 介绍 | ![演示图片](READMEimages/position_motor.gif)                                   |
| 关节      | <sub>4c3。线速度 Motor.unity</sub>                   | 显示直线速度电机                                       | 介绍 | ![演示图片](READMEimages/linear_velocity_motor.gif)                            |
| 关节      | <sub>4c4。角速度 Motor.unity</sub>                  | 演示角速度电机                                | 介绍 | ![演示图片](READMEimages/angular_velocity_motor.gif)                           |
| 关节      | <sub>4c5。旋转 Motor.unity</sub>                        | 演示旋转电机                                      | 介绍 | ![演示图片](READMEimages/rotational_motor.gif)                                 |
| 关节      | <sub>4d。Ragdolls.unity</sub>                                 | 强制性的布娃娃演示堆栈                                   | 介绍 | ![布娃娃](READMEimages/ragdoll.gif)                                             |
| 关节      | <sub>4e。单 Ragdoll.unity</sub>                           | GameObject 用于 Unity Physics 的布娃娃（左）和内置物理功能（右），使用 [布娃娃向导](https://docs.unity3d.com/Manual/wizard-RagdollWizard.html) 创建 | 介绍 | ![演示图片](READMEimages/single_ragdoll.gif) |
| 调整      | <sub>5a。改变运动 Type.unity</sub>                       | 显示运动类型变化的演示                                  | 介绍 | ![演示图片](READMEimages/change_motion_types.gif)                              |
| 调整      | <sub>5b。零钱盒 Collider Size.unity</sub>                 | 演示 collider 大小的运行时更改                       | 介绍 | ![演示图片](READMEimages/change_box_collider_size.gif)                         |
| 调整      | <sub>5c。更改 Collider Type.unity</sub>                     | 演示 collider 类型的变化                               | 介绍 | ![演示图片](READMEimages/change_collider_type.gif)                             |
| 调整      | <sub>5d。更改 Velocity.unity</sub>                          | 显示速度变化的演示                                     | 介绍 | ![演示图片](READMEimages/change_velocity.gif)                                  |
| 调整      | <sub>5e。Kinematic Motion.unity</sub>                         | 演示与动态对象相结合的运动学运动   | 介绍 | ![演示图片](READMEimages/kinematic_motion.gif)                                 |
| 调整      | <sub>5f。改变表面 Velocity.unity</sub>                  | 显示表面速度变化的演示                             | 介绍 | ![演示图片](READMEimages/change_surface_velocity.gif)                          |
| 调整      | <sub>5g1。更改 Collider 材质 - Bouncy Boxes.unity</sub> | 演示显示独特的 prefab 实例化与 collider 材质更改的效果 | 中间的 | ![演示图片](READMEimages/runtime_modification_collider_material.gif) |
| 调整      | <sub>5g2。独特的 Collider 斑点 Sharing.unity</sub>            | 演示显示在运行时实例化prefab的效果，使 collider Blob 独一无二并共享 collider Blob 数据 | 先进的 | ![演示图片](READMEimages/runtime_modification_unique_sharing.gif) |
| 调整      | <sub>5g3。运行时 Collider Creation.unity</sub>               | 在运行时创建网格 colliders                                |   先进的   | ![运行时修改 colliders](READMEimages/modify_colliders_runtime.gif)    |
| 调整      | <sub>5g4。运行时碰撞过滤器 Modification.unity</sub>   | 在运行时修改碰撞过滤器                             |   先进的   | ![运行时修改碰撞过滤器](READMEimages/runtime_modification_collision_filter.gif) |
| 调整      | <sub>5g5。修改 Collider Geometry.unity</sub>                | 在运行时修改 collider 几何图形                             |   先进的   | ![在运行时修改 collider 几何](READMEimages/runtime_modification_geometry.gif) |
| 调整      | <sub>5h。更改 Scale.unity</sub>                             | 显示 entities 比例变化的演示                               | 介绍 | ![演示图片](READMEimages/change_scale.gif)                                     |
| 调整      | <sub>5i。申请 Impulse.unity</sub>                            | 演示显示脉冲的应用                                | 介绍 | ![演示图片](READMEimages/apply_impulse.gif)                                    |
| 调整      | <sub>5j。修改 Broadphase Pairs.unity</sub>                  | 通过从广泛阶段中显式删除对来过滤掉冲突  |   先进的   | ![修改宽相示例](READMEimages/modify_broadphase_pairs.gif)            |
| 调整      | <sub>5k。修改联系方式 Jacobians.unity</sub>                 | 修改接触生成结果，产生特效 |   先进的   | ![修改联系人](READMEimages/modify_contact_jacobians.gif)                    |
| 调整      | <sub>5l。修改窄相 Contacts.unity</sub>              | 将新用户联系人添加到模拟管道                        |   先进的   | ![修改 Narrowphase 触点](READMEimages/modify_narrowphase_contacts.gif)     |
| 使用案例    | <sub>6a。角色 Controller.unity</sub>                     | 显示基本 FPS 字符控制器的用户案例演示       | 中间的 | ![字符控制](READMEimages/character_controller.gif)                      |
| 使用案例    | <sub>6b。Pool.unity</sub>                                     | 调用即时模式物理演示                     | 中间的 | ![即时物理](READMEimages/pool.gif)                                      |
| 使用案例    | <sub>6c。行星 Gravity.unity</sub>                           | 使用 SP/HP 进行行星周围小行星的性能演示           | 介绍 | ![行星引力](READMEimages/planet_gravity.gif)                               |
| 使用案例    | <sub>6d。Raycast Car.unity</sub>                              | 显示一组车辆行为的用户案例演示                   | 中间的 | ![车辆](READMEimages/raycast_car.gif)                                        |
