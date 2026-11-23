# TA-46 · 布料离屏后回来抖：先区分首帧姿势被覆盖、瞬移和旧状态失效

**制作决定**：仅相机离开又回来、角色没瞬移，先确认布料是否真的停算，以及第一可见帧最后是谁写入骨骼姿势。短暂停算后，如果旧褶皱仍贴合身体和碰撞体，优先尝试继续模拟；角色根节点瞬移时，先尝试让布料随根移动并消除这次位移的额外冲击；若旧姿势已经穿入新碰撞体，才把重置列入对照。

## 公开证据

- **作者报告 · Magica Cloth 2。** [Camera culling](https://magicasoft.jp/en/mc2_magicacloth_culling/#Camera_culling_mode) 区分 `Keep`（停算并保留状态）和 `Reset`。[Animator culling issue](https://magicasoft.jp/en/mc2_magicacloth_culling/#Animator_culling_issue) 描述了一个会误判成布料抖动的案例：启用 `Cull Update Transforms` 后，重新可见时 Animator 可能在渲染期间写入 Transform，覆盖已求出的布料结果，露出一帧未模拟的姿势。作者还指出 Scene 视图或可见的阴影可能让对象没有真正进入剔除。这里依据正文，没有据视频判断持续时间或效果。
- **Epic · UE 5.5 `SkeletalMeshComponent`。** [API 文档](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/SkeletalMeshComponent?application_version=5.5#unreal.SkeletalMeshComponent.force_cloth_next_update_teleport) 把 `suspend_clothing_simulation`（保留最后状态）、`resume_clothing_simulation`（下次布料更新时瞬移）、`force_cloth_next_update_teleport`（随位姿变换粒子位置和速度以保留状态），以及 `force_cloth_next_update_teleport_and_reset` 分开。即使都叫“恢复”或“瞬移”，也要核对当前组件保留了哪部分状态。
- **Unity 6000.0 `Cloth` 与 Magica Cloth 2。** Unity 的 [`ClearTransformMotion`](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/cloth/cleartransformmotion) 说明它让布料粒子随 Transform 一起移走，以免待计入的 Transform 位移突然拽动布料；文档没有给出清零所有粒子速度或重置褶皱的保证。Magica Cloth 2 的[自动瞬移设置](https://magicasoft.jp/en/mc2_magicacloth_inertia/#Automatic_teleport_judgment)也单列了保留姿势和重置的处理。避免把“去掉根节点位移造成的冲击”“清粒子速度”和“重建布料形状”当作同一个操作。

## 最小对照

1. **只让相机离屏再回来。** 固定角色动画或保持相同动画相位，碰撞体与根位姿不变。先排除 Scene 视图、多相机和可见阴影造成的干扰，记录布料是否确实停步。保持布料剔除设置不变，比较 Animator 持续更新和 `Cull Update Transforms`：记录恢复首帧求值顺序、最终姿势和实际绘制结果。如果只在后一种设置下短暂露出动画姿势，先修求值顺序或调整 Animator 剔除；确认状态被保留后再验 `Keep` 与 `Reset` 的褶皱连续性。
2. **单测根瞬移。** 让角色一直可见、动画与碰撞体相对身体的位置相同，用相同初始褶皱和瞬移距离、角度对照普通更新与对应引擎的随根瞬移处理。分别记录布料相对身体的首帧偏移与随后速度变化；保留原有速度的候选需要检查回来的抖动是否其实源自已有运动。
3. **单测回来时碰撞体已变化。** 保持根位姿与离屏时长相同，只在离屏期间更换碰撞体或姿势。先看旧形状是否在首帧穿进身体，再对照保状态与重置的最大穿入量、达到稳定所需帧数和重置造成的轮廓跳变；重置本身也需要确认初始姿势无穿插。

**状态**：已核对作者报告及 Epic、Unity 的公开文档；对照为待执行方案，未运行本地 A/B，未做实机验证。
