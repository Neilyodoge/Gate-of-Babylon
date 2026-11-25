# TA-56 · GPU 粒子碰撞的离屏漏碰

**制作决定。** 相机一转雨滴就穿过树冠，先核对碰撞体还在不在粒子所用的深度缓冲；如果镜头外也必须持续遮雨，可以试距离场或简化的世界空间碰撞体。若相机固定、碰撞体一直可见仍穿墙，先分开查场的空间精度和粒子的每步位移。

## 证据

- **Epic 公开案例说明。** [UE 4.27：Using Particle Collision Mode for Distance Fields](https://dev.epicgames.com/documentation/unreal-engine/using-particle-collision-mode-for-distance-fields?application_version=4.27#endresult)的 End Result 用同一树冠说明两种结果：镜头移到树下后，场景深度漏掉离屏树顶，粒子落到镜头前的地面；距离场只要仍表示该树冠，离屏后还能拦住粒子。此处核对的是作者正文；页面的两段媒体未播放。这是 Cascade 示例。Epic 说该模式的碰撞计算开销大致与场景深度相当，但没有给硬件和计时；该版本不支持 SM5 时会回退到场景深度。
- **Epic 公开机制。** 同页指出全局距离场分辨率较低，薄片可能消失，尖角会变圆。[Mesh Distance Fields 的 Global Distance Field 小节](https://dev.epicgames.com/documentation/unreal-engine/mesh-distance-fields?application_version=4.27#globaldistancefield)说明全局场由相机周围的多级体积组成，合成网格的距离场。离屏可以有数据，但仍要确认碰撞处在体积覆盖内，且碰撞网格确实被场表示。已有距离场与为单个效果新增距离场的项目代价不同。
- **实现反例。** [Godot 4.4-stable：particles.glsl](https://github.com/godotengine/godot/blob/4.4-stable/servers/rendering/renderer_rd/shaders/particles.glsl)的 3D 分支把当前粒子位置转换到碰撞体空间，`COLLIDER_TYPE_SDF` 先检查体积边界，再取三维距离，距离小于粒子半径加容差才命中。命中时另取六个邻点估计法线。这个分支没有沿前后位置扫掠；即使场里完整保留薄墙，粒子一步跨过去也可能漏碰。这里验证的是 Godot 公开源码，不能据此假定 Cascade 或 Niagara 使用同一算法。

## 用法与边界

| 出错时机 | 把哪个变量固定，再试什么 |
| --- | --- |
| 转镜头才漏 | 保持发射器模拟、随机种子和粒子轨迹不变，只让碰撞体出画；与支持且已加载的世界空间碰撞体对照。先排除发射器停止模拟。 |
| 碰撞体在视野内却被前景挡住才漏 | 固定相机，分别移入、移开一个只写深度的前景遮挡物；抓帧看遮挡后粒子相关像素剩的是哪个表面的深度。测试碰撞结果时记录事件或隔离显示粒子，避免把粒子被挡住当作碰撞。 |
| 镜头固定也漏 | 用低速比较原薄片、厚代理，并看距离场实际表示；用已确认存在的碰撞体再提速或拉长步长，比较离散采样、缩短步长和扫掠。只有薄片低速也漏时，才优先修代理或场分辨率。 |

**单步算例。** 墙厚 2 cm、粒子半径 0.5 cm，沿墙法线移动。帧率 60、速度 6 m/s 时，一步走 10 cm；若从墙中心前 5 cm 到后 5 cm，起终点都不会重叠。理想平板上的 4 个等分子步每步走 2.5 cm，比墙加两侧粒子半径形成的 3 cm 命中区短；本例会采到墙中心。实际用哪种方案还要考虑可用子步、场采样、碰撞体运动和成本。

**验证状态。** 已核对 Epic 公开案例文字、Godot 源码和几何算例；未观看 Epic 动画，也未做本地或真机 A/B。实际验收记录漏碰率、首次落点和粒子数；GPU 分开量粒子模拟、距离场维护与总帧时，并确认目标档未退回屏幕深度。
