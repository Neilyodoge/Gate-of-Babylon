# TA-127 · 凹形道具导入后碰撞顶住空气

**制作决定。** 模型能看见凹口，角色却撞到空气时，先显示引擎实际使用的碰撞几何，再用与玩法一致的查询或角色胶囊做对照。单凸包会跨过凹口；要保缺口又让道具参与动态刚体，优先试几块保留通路的凸形。固定场景需要更精细的表面时，再比较三角面碰撞的适用限制与成本。

## 公开证据

- [Unity 6000.0 · Mesh Colliders](https://docs.unity3d.com/6000.0/Documentation/Manual/mesh-colliders-introduction.html) 的 Physics Debug 原图：[凹形香蕉](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/food-pack-banana-concave-mesh.png)的绿色网格沿着内弧，[凸形香蕉](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/food-pack-banana-convex-mesh.png)的外壳从柄跨到尖端，覆盖了内弧外侧原本的空处。文档说明凹形网格碰撞适用于静态或运动学物体，并且两个凹形碰撞体不互相碰撞；需要保留凹部并与其他形状交互时，可用多个凸形组成复合碰撞。两张图展示的是几何，未给物理查询成本。
- [UE 5.5 · FBX Static Mesh Pipeline](https://dev.epicgames.com/documentation/en-us/unreal-engine/fbx-static-mesh-pipeline-in-unreal-engine?application_version=5.5) 明确建议给有开口或凹区且必须留通路的模型做自定义碰撞。多个凸形可命名为 `UCX_模型名_00`、`UCX_模型名_01` 等，模型名须与 FBX 中对应渲染网格准确匹配。官方提醒多个凸包不要相交；接缝留多少还要以角色扫掠和挤压测试判断。该页也提醒非等比缩放的盒、球存在限制；球形和角色的有体积扫掠不能用射线成功与否直接替代。
- [UE 5.5 · FBX Import Options](https://dev.epicgames.com/documentation/en-us/unreal-engine/fbx-import-options-reference-in-unreal-engine?application_version=5.5) 说明启用 One Convex Hull per UCX 会把每个 UCX 转成一个凸包；关闭允许进一步分解，不能保证自动分解会保住想要的缺口。检测到自定义碰撞时，Auto Generate Collision 不会使用。[Simple vs Complex](https://dev.epicgames.com/documentation/en-us/unreal-engine/simple-versus-complex-collision-in-unreal-engine?application_version=5.5) 区分盒／球／胶囊／凸包与三角网格；若采用 Use Complex Collision As Simple，该对象本身不能模拟刚体，但可被其他使用简单碰撞的模拟对象撞到。

## 算例与最小对照

1. **先看模型的哪一层被填平。** 以俯视 U 形件为例，外宽 2.0 m，两个侧臂内缘在 `x = ±0.60 m`。只量凹口入口、射线停止在底边之前：单凸包会命中 `x = 0` 的空位；两个侧臂和底边分成三块后，该入口中心路径不命中，`x = 0.85 m` 仍命中侧臂。若用宽 0.80 m 的角色形状居中，理想几何单侧净空是 `(1.20 − 0.80)/2 = 0.20 m`，未计碰撞余量、缩放和旋转。射线通过仍须跑胶囊扫掠，看是否擦住侧臂或凸形接缝。
2. **核对实际导入件。** 固定渲染网格，对照自动生成、手工拆好的多个简单碰撞和三角面查询。查看引擎碰撞可视化、实际形状数量、资产缩放和查询使用的碰撞复杂度；射线与胶囊使用同一对象过滤，再记录命中对象和接触位置。若渲染几何正确而引擎碰撞形状不对，先查 FBX 命名、导入器和碰撞生成选项。若 UE 的复杂查询能进缺口、玩家扫掠却被挡，先比较两者用的形状和通道。
3. **按用途选。** 动态道具用满足交互轮廓的尽量少的凸形，并验移动、缩放和落地接触；固定道具若常被精确点击或穿行，才把三角面路径纳入对照。物理处理成本应在代表场景量查询、接触和内存。多个凸形或三角网格哪个更便宜，受形状数量、接触密度和目标设备影响。

**验证状态。** 已阅读 Unity／UE 文档，查看两张实际碰撞调试原图并复算 U 形截面。未导出碰撞 FBX，未跑引擎扫掠或真机计时。
