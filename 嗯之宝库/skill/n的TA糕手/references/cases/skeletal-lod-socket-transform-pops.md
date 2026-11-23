# TA-44 · 骨骼 LOD：挂点跳了，还是手部网格跳了？

**制作决定**：道具在镜头拉远时错位，先固定相机和动画时刻手动切 LOD，同时记录挂点变换、手部网格上可见的接触标记和姿态更新间隔。挂点在同一姿态突然变化，查绑定骨或查找回退；挂点稳定但指节／掌心相对道具变形，查网格或蒙皮权重；LOD 固定仍出现隔几帧一格的阶梯，查动画更新频率。

## 原始证据及适用范围

- **Epic，骨骼裁剪与减面。** [Skeletal Mesh Editor 的 Asset Details](https://dev.epicgames.com/documentation/unreal-engine/skeletal-mesh-editor-in-unreal-engine#assetdetails)分列 Bones to Remove、Bones to Prioritize 和单帧 Bake Pose。移除设置按 LOD 生效；几何优先设置用于保留与指定骨关联的蒙皮网格。在 [Skeletal Mesh LODs 同一 LOD 6 的手部示例](https://dev.epicgames.com/documentation/unreal-engine/skeletal-mesh-lods-in-unreal-engine#bonestoprioritize)里，Epic 的步骤是加入手部骨骼、调高优先权重并重新生成 LOD，文档图注标出有／无优先设置。这个对照改变的是减面输入，不是挂点求值或游戏中的运动；本次图像资源未能目视复核。
- **Epic，挂点所依赖的骨链。** [Socket Details](https://dev.epicgames.com/documentation/unreal-engine/skeletal-mesh-sockets-in-unreal-engine#socketdetails)定义挂点相对父骨的偏移；Force Always Animated 让其父骨链在 LOD 原本会移除这些骨时仍参与求值。对远处仍必须对准的枪口、交互点，可以比较仅保留所需骨链，或在稳定的上游骨上制作经验证的远景挂点；后者应按最大动作位移验收。Mesh Socket 仅属于指定 Skeletal Mesh，换了网格资产另查名称、父骨和相对偏移。
- **Epic，区分缺失与静止。** [UE 5.5 SceneComponent API](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/SceneComponent?application_version=5.5#unreal.SceneComponent.get_socket_transform)说明找不到名称时，按默认世界空间查询会得到组件的变换。因此挂点突然贴到角色原点附近时，记录查找结果、原组件和父骨，不要把一个正常数值当作查找成功。存在性检查只能确认名称，不能证明当前帧有新姿态。
- **更新频率是另一条路径。** [Epic Animation Budget Allocator](https://dev.epicgames.com/documentation/unreal-engine/animation-budget-allocator-in-unreal-engine)可按重要性给组件降频、停更或选择插值；调试覆盖层的数字表示间隔帧数，I 表示插值。只给挂点保骨，不能据此推断一个被降频的组件每帧重新求姿态。以 60 FPS、每 4 帧更新一次且不插值为例，若挂点相对组件沿直线以 0.9 m/s 移动，下一次更新前的最后一个显示帧比即刻位置落后 3 帧，即 4.5 cm。这是固定速度算例，实际算法可能插值或使用不同间隔。
- **Unity 6000.0 的边界。** [SkinnedMeshRenderer.quality](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/skinnedmeshrenderer/quality)限制蒙皮时每顶点采用的骨权重数，不修改底层网格数据。它可以改变手部轮廓，即使 Transform 仍存在，因此不能凭“道具相对手滑了”就判定 Unity 删除了挂点所用的 Transform。

## 复核与取舍

1. 同一角色放一个绑在挂点上的可视标记，再选一个位于网格表面的手部接触标记。记录 LOD、组件空间与世界空间坐标、查找结果和时间戳。冻结动画相位，锁定每帧求值，比较强制 LOD0 和目标 LOD；只改几何保留或骨链保留中的一个，检查是哪对标记先分离。
2. 固定目标 LOD 并恢复播放，先关闭动画预算降频作为基线，再启用相同预算、插值开关与角色数量。录制挂点轨迹、每帧姿态更新状态和道具相对误差。若枪口火花对齐依赖该挂点，分别检查触发帧和后续跟随；动态重新 Attach 时另核对 Keep World／Snap 规则，免得把重新绑定本身的偏移算给 LOD。
3. 只在目标镜头和玩法真的需要时为关键挂点保骨或提高组件更新优先级。用同数量同动作分别记录参与求值的骨、动画和姿态更新 CPU 时间、蒙皮相关耗时、接触误差；减少三角形不能代替骨骼求值和更新频率的测量。

**状态**：已核对 Epic 与 Unity 正文和 API，位移为数学算例；未目视 Epic 手部原图，未在本地运行角色 A/B 或测量设备成本。
