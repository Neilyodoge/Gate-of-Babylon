# TA 研究队列

## 当前续接点

**TA-54 · 卡通明暗分带的远景闪烁。** 阴影色阶在慢移镜头时跳动，如何区分色阶阈值、法线变化和实际阴影图抖动？查公开卡通着色实现和可见对照，分开验阈值过渡、法线过滤和阴影采样。

已核 [Roystan：Toon Shader，§2／§5](https://roystan.net/articles/toon-shader/) 与 [接收投影原图](https://roystan.net/media/tutorials/toon-shader-receive-shadow.png)：Unity 2018.3 示例在加投影后先乘 N·L 与阴影衰减，再用 0–0.01 的窄阈值；固定 N·L=0.25，衰减 0.02→0.05 时，阈值内乘法算出直射权重 0.5→1；阈值后乘为 0.02→0.05。两种顺序会对同一投影变化给出不同结果，先将衰减固定为 1 隔离材质色阶，再用仅投影灰度检查输入。已看静帧，未验证运动或设备；续查阈值后乘的公开实现及慢移对照，区分投影不稳被放大和法线输入本身变化。

## 待研究

| 主题 | 问题 | 复核重点 |
| --- | --- | --- |
| TA-55 · HDR 光照贴图压缩的暗部色带 | 同一光照烘焙在移动端出现的阶梯，怎样区分压缩块和色调映射或色阶量化？ | 查同烘焙不同编码的公开原图或测量，核对设备最终格式及运行时解码。 |

做完一个题后补下一项，持续轮换；同一方法有新反例或测量时更新原卡。

## 已归档

| 条目 | 有用判断 | 证据及仍未验证的范围 |
| --- | --- | --- |
| [TA-01 · Unity Alpha 导入与透明边缘](cases/texture-alpha-import.md) | Alpha 来源跟透明边缘扩色是两个阶段；打包数据遇到透明区 RGB 被改写，先核对扩色开关。 | Unity 6000.0 官方文档；未在编辑器或设备执行对照。 |
| [TA-02 · 透明特效的覆盖与叠层](cases/transparent-vfx-overdraw.md) | 先分开测覆盖和层数；过绘能指向片元压力，无法单独证明是混合或外部带宽。 | Arm Mali G2 与 Unity 6000.0 官方文档；有对照方案，目标设备未跑。 |
| [TA-03 · 移动端贴图压缩与通道打包](cases/mobile-texture-compression-and-packing.md) | 合图能少采样的前提是共享采样坐标和状态；块尺寸、导入分辨率和设备回退决定数据是否真的变小。 | Unity、Arm、Android 原文及块数据计算；未做设备对照。 |
| [TA-04 · 贴图导入预检](cases/texture-import-policy.md) | `importSettingsMissing` 判断的是缺 `.meta`；区分填一次的默认和持续强制的预设，给后者限定受控字段与例外。 | 公开覆盖复现、Unity 6000.0 扩展示例和刷新条件；未在 Unity 运行。 |
| [TA-05 · Player 缺 Shader 变体](cases/shader-variant-runtime-stripping.md) | Unity 公开实例化复现中全局保留设置仍可报缺；严格匹配结合 Shader/pass/stage 日志验收具体关键字。 | Unity Issue Tracker 原始日志与 6000.0 文档；未本地构建。 |
| [TA-06 · 远景高光闪烁与 Bloom](cases/specular-glint-stability-and-bloom.md) | 同一雪地镜头中，几何过滤与法线纹理过滤都降低了与参考图的误差；在玩法镜头先分开查高光源和 Bloom。 | Chermain 原始图与作者数据，JCGT 掠射角边界；未核验运动序列，未做本地或真机对照。 |
| [TA-07 · 大量同类特效的系统选择](cases/particle-system-batching-and-upload.md) | 短效果按发射数上传；持续跟随可能上传全容量状态表。系统数、粒子数和透明覆盖分开测。 | Unity Galaxy Sample 原始源码与 Built-in 文档；未跑样例和目标设备 A/B，字节数为估算。 |
| [TA-08 · 烘焙光照接缝与漏光](cases/lightmap-seams-and-leaks.md) | 单物体岛边先对照缝合；模块拼缝和几何相交分别查，远景再验 UV 岛串色。 | Jalnionis 图 20–21 与 Unity 6000.7 Beta 三联图；未在本地烘焙。 |
| [TA-09 · 材质参数与 SRP Batcher](cases/srp-batcher-per-object-properties.md) | 4 个颜色材质能同批；24 个 MPB 对象可从普通绘制，变成按材质 4 次，再到共材质 1 次实例化绘制。 | Catlike Coding 原始 Frame Debugger 截图和 Unity 6000.0 URP 文档；未测 CPU/GPU 时间。 |
| [TA-10 · LOD 交叉淡化与多通道](cases/lod-cross-fade-pass-consistency.md) | 互补裁剪避免淡入网格消失；自定义 Shader 要分开验颜色、深度、阴影与过渡峰值。 | Catlike Coding 原图和运动片段、Unity URP Lit 源码；未做硬切 A/B 或真机测量。 |
| [TA-11 · 法线烘焙与切线接缝](cases/normal-map-tangent-seam-diagnosis.md) | 只有镜像列翻面先查切线；普通法线也转向才查绿通道；沿三角边折影先核三角化。 | Khronos 三组样件图、Marmoset 原图及 Unity 6000.0 导入文档；未在 Unity 复现。 |
| [TA-12 · mip 流送与镜头切换](cases/texture-streaming-camera-cuts.md) | UE 报告中预卷加到 100 帧仍有跳变；分开查请求时机和位置、实际驻留以及画面采样。 | UE Sequencer 公开排查和 Unity 6000.6 预载文档；无本地或设备测量。 |
| [TA-13 · 骨权重预算与关节变形](cases/skinning-influence-budget.md) | 同为四骨，直接截断会出折痕时先对照在四骨限制内重求权重；Unity 先运行时切换做试验。 | SkinCells 图 7 与 Unity 6000.0 文档；无实机或二骨/四骨同素材结果。 |
| [TA-13a · GPU 蒙皮中的动态包围盒](cases/gpu-skinning-dynamic-bounds.md) | 自研 Compute Pass 变慢时先把动态包围盒与变形分开测；逐组归约与保守盒适用于不同网格分布。 | Traverse 作者 Android Pass 实测 7.0 → 1.7 ms；未复现，无骨权重数或最终帧时对照。 |
| [TA-14 · 移动端后处理带宽](cases/mobile-postprocess-tile-bandwidth.md) | 当前像素调色可试片上合并；有邻域采样的效果须另查；外部流量和帧时分别测。 | Meta Quest 2 计时、Khronos Mali-G76 计数与 Arm 反例；未做本地真机 A/B。 |
| [TA-15 · 顶点动画与运动矢量](cases/vertex-animation-motion-vectors.md) | 先看速度覆盖，再看前帧参数与重投影；只在 LOD 切换发生则查抖动覆盖是否匹配。 | AMD 原始速度截图和 Epic 正文；无本地复现或真机数据。 |
| [TA-16 · 自动 LOD 与边界保留](cases/automatic-lod-attribute-boundaries.md) | 同一减面率下接缝可大不同；分别按轮廓、贴图和高光验收，再决定哪些边要锁。 | Bistro 木椅原图、Scientific Reports 图 8/12、meshoptimizer 文档；未在目标视距复现。 |
| [TA-17 · 薄面叶片照明](cases/thin-foliage-lighting.md) | 先关阴影隔离遮挡；固定透射再查双面法线，两者共同影响背光角度。 | Epic 原图与 Unity HDRP 2022.3 源码；未在本地做单变量 A/B 或真机测试。 |
| [TA-18 · 实例化植被与剔除粒度](cases/vegetation-instancing-cull-granularity.md) | 整批包围盒会带入屏外实例；比较空间分块与逐实例筛选，分开计提交、绘制实例和剔除成本。 | Unity 6000.0 文档及 Cyanilux 原图和实现：50 万候选中约 8.9 万绘制；缺独立帧时与真机 A/B。 |
| [TA-19 · 蒙版叶片的 mip 与覆盖](cases/foliage-alpha-coverage-mips.md) | 保覆盖能救树冠密度；总面积接近也可能吃掉花茎、填实空隙，剪影要分开验。 | The Witness A2C 原图、Lisyarus 0.5 裁剪对照与 Unity 6000.0 文档；未做本地或真机 A/B。 |
| [TA-20 · 植被遮挡剔除的净收益](cases/vegetation-occlusion-culling-budget.md) | 开阔镜头可少花几何时间却被建 HZB 抵消；按镜头测阶段小计、总帧时和显露正确性。 | Steam Deck 建筑场景三组图与计时、Unity 6000.0 文档；缺植被同场景真机 A/B。 |
| [TA-21 · Flipbook 图集跨帧串色](cases/flipbook-atlas-mip-bleeding.md) | mip 0 双线性跨格先查 UV 与补边；粗 mip 点采样已混色则重做逐帧 mip 或改用独立层。 | Unity Flipbook 源码、0 FPS 原图与格边算例；未在本地 Flipbook 或真机验证。 |
| [TA-22 · 移动端蒙版植被的深度预通道](cases/mobile-masked-foliage-depth-prepass.md) | 先抓帧确认预通道和 `Equal` 是否真的变，再核对镂空、风动和净 GPU 帧时。 | Unity URP 6000.0 源码及公开硬件数据；缺同植被移动端应用层 A/B，未本地或真机复现。 |
| [TA-23 · Flipbook 帧插值的双轮廓](cases/flipbook-motion-blending.md) | 快速扩张先试同相位对照，再用同画质比较加帧或向量图的成本。 | TFlow 固定相位、Lozar 原图与采样/字节算例；未本地复现或真机计时。 |
| [TA-24 · 软粒子与场景深度](cases/soft-particles-scene-depth.md) | 先抓帧验深度是否在透明绘制前可用，再分别测生成和采样两笔成本；低分辨率测光晕与穿边。 | Unity 对照与 URP 6000.0 源码、GPU Gems 原图；未本地或真机验证。 |
| [TA-25 · VAT 数据贴图的精度与空间](cases/vat-texture-precision-and-bounds.md) | 把 UV 读错列、位置值量化和整次绘制被剔除分开；Bounds 参与解码时先解耦。 | SideFX FAQ、UE5.1 反馈、位置图和 CPU 数值对照；未复现实机。 |
| [TA-26 · 屏幕空间反射的边缘消失](cases/screen-space-reflection-edge-fallback.md) | 缺屏外数据、屏内不命中、时域残影分别验；用场景内容决定探针或平面回退。 | Epic 泳池原图与两个 GPU 截图、AMD SSSR 文档；缺同场景本地和设备对照。 |
| [TA-27 · 透明粒子的排序与穿插](cases/transparent-particle-sorting-and-intersections.md) | 系统内先查 Sort Mode，整批先查 Sorting Fudge；两侧反向穿插不能靠一个绘制顺序修全。 | Catlike Coding 静态对照、Unity 6.6 文档、Khronos 原图与两面算例；未核验动态帧、本地或真机。 |
| [TA-28 · 三平面材质的接缝与采样预算](cases/triplanar-material-seams-and-sampling.md) | 底色、法线和高光分别验；先按纹理组算 3/5/9 次，再判断是否值得换成双平面。 | Catlike Coding 与 Quilez 原图和源码；未做同岩石本地或真机对照。 |
| [TA-29 · 贴花投射的接收范围](cases/projected-decal-receiver-filtering.md) | 先按误染位置查盒深、接收层或角度；URP 屏幕空间加接收层时另查预通道。 | Unity 6000.0 油漆桶 A/B 和 Epic 原图、官方路径说明；无本地或真机量化。 |
| [TA-30 · 光照探针的室内外过渡](cases/light-probe-indoor-outdoor-transition.md) | 先区别采样点、跨墙插值和探针数据污染；静态分层之外，移动角色单独验门口渐变。 | Unity 6000.0 救护车和暗室原图、模式说明；缺动态门口 A/B、本地或真机对照。 |
| [TA-31 · 方向光级联阴影的切换线](cases/directional-shadow-cascade-transitions.md) | 级联边界先查世界纹素与混合；同一级内爬动先查投影跨度和光空间对齐。 | Microsoft 图 9、Catlike 原图与实现、纹素算例；未做连续运动、本地或真机对照。 |
| [TA-32 · UV 滚动特效的时间精度](cases/uv-scroll-time-precision.md) | 先分开量化时钟和大偏移；共用回卷前核对每层每轴是不是整数圈。 | Epic、Unity、微软文档与 Float16/32 CPU 数值对照；引擎画面与真机未测。 |
| [TA-33 · 动态分辨率下 UI 和特效的清晰度](cases/dynamic-resolution-ui-and-translucency.md) | 先验各层的渲染目标与合成顺序；透明模糊再分输入像素和历史。 | URP 源码、UE5.9 透明原图与样例 TSR 计时；动态序列和真机未测。 |
| [TA-34 · 体积雾的时间重投影拖影](cases/volumetric-fog-temporal-reprojection.md) | 灯灭后有亮尾先分历史权重和单灯体积贡献，再权衡噪点、光束和 GPU 成本。 | Epic 静态原图、Godot 4.4 源码和衰减算例；未做动态 A/B、本地或真机测试。 |
| [TA-35 · 发片各向异性高光](cases/hair-card-anisotropic-highlights.md) | 亮带走向错先核发丝切向；只在局部走向无法由几何表达时再试流向图，另测偏移和宽度。 | Epic Sparrow 静态原图与 Unity HDRP 源码；未做动态 A/B、本地或真机测试。 |
| [TA-36 · 移动端网格索引与拆分](cases/mobile-mesh-index-width-and-splitting.md) | 基顶点受局部编号跨度限制；拆块分开算索引、重复顶点与真正可剔除的范围。 | Unity、glTF、Assimp 源码与字节算例；未做本地导入或真机 A/B。 |
| [TA-37 · 接触阴影的屏幕空间边界](cases/screen-space-contact-shadow-limits.md) | 先验主阴影偏移；短程接触只补视内细节，离屏仍须可见的投影留给主阴影。 | Epic 门洞静态 A/B 与 HDRP 屏幕边界源码；未做离屏序列或真机计时。 |
| [TA-38 · 纹理数组与图集](cases/texture-array-atlas-batching-and-mips.md) | 共享绑定仍要核对材质与实例参数才知是否合批；混合三切片仍需三次采样，异尺寸先算占用。 | Catlike 地形源码和颜色抓帧、Unity API 与纹素算例；缺同场景数组/图集实测。 |
| [TA-39 · 屏幕空间描边的深度与法线](cases/screen-space-outline-depth-normal-thresholds.md) | 远景缺线分深度和折角；斜面误描先调掠射处的深度阈值，再验细轮廓。 | Roystan 同构图五张原图、URP 源码和深度算例；本地或真机未测。 |
| [TA-40 · 反射探针的盒投影与混合](cases/reflection-probe-box-projection-and-room-blending.md) | 屋内倒影错位先验投影盒；门口换色先核本地权重、全局回退和隔墙串色。 | Catlike 静态原图、URP 源码与权重算例；跨门动态和真机未测。 |
| [TA-41 · LUT 调色的 HDR 范围与合成排序](cases/hdr-lut-range-and-compositing-order.md) | URP 高光随用户 LUT 开启而收白时，先比较权重 0 与很小正值，定位查表前的夹值；UI 偏色另查合成阶段。 | URP 6000.0 源码、Epic 文档与夹值算例；未做编辑器、真机或同镜头图像验证。 |
| [TA-42 · 角色动画压缩的末端漂移](cases/animation-compression-end-effector-drift.md) | 手脚、挂点先按接触帧量世界空间误差；整体 P99 更低，仍可能有厘米级离群值。 | ACL 的 Matinee／Paragon 基准、Epic 设置文档与角度算例；未本地复现或真机计时。 |
| [TA-43 · 法线贴图粗 mip 与高光闪烁](cases/normal-map-mips-and-roughness-prefilter.md) | 平面远景闪先查贴图的粗糙度 mip；只过滤几何法线补不回纹理法线分布，方向性纹样另验。 | Filament／Unity 源码、Epic 接口、LEAN 海面原图与算例；未做本地同镜头动态或设备测试。 |
| [TA-44 · 骨骼 LOD 与挂点的可见跳位](cases/skeletal-lod-socket-transform-pops.md) | 先对照挂点位置、手部网格和求值间隔；给挂点保骨不等于每帧更新，没找到挂点还可能回退到组件。 | Epic 与 Unity 公开文档、固定速度算例；未目视官方 LOD 图或本地／真机复现。 |
| [TA-45 · 瞬时特效与自动曝光恢复](cases/transient-vfx-auto-exposure-recovery.md) | 测光目标仍偏先查采样和权重；目标回稳而实际曝光未跟上才调恢复速度，遮罩另测中央和边缘。 | Epic 同镜头原图和文档、Unity HDRP 6000.0 源码及算例；未本地运行闪光或真机验证。 |
| [TA-46 · 布料离屏后重新出现的抖动](cases/cloth-offscreen-resume-and-teleport.md) | 只跳一帧先查动画与布料的写入顺序；根瞬移先试随根移动，旧形状碰撞失效才对照重置。 | Magica Cloth 2 作者报告及 UE 5.5／Unity 6000.0 文档；未看演示视频，无本地 A/B 或实机验证。 |
| [TA-47 · 运行时虚拟纹理近景模糊与缺页](cases/rvt-page-residency-and-closeup-blur.md) | 地面池有余量也要看其他池的全局偏置；停住后能变清才进一步查请求、生产配额与实际采样。 | Epic 文档、DXT1 原始页数转储及纹素算例；未看 HUD 原图，未做本地近景或真机对照。 |
| [TA-48 · 局部光源阴影图集的分辨率跳变](cases/punctual-shadow-atlas-resolution-jumps.md) | 原灯请求不变也可能随图集缩小；按点光六块、聚光一块算，再抓实际 viewport。 | URP 6000.0 源码、Catlike 图集原图与面积算例；未本地或真机 A/B。 |
| [TA-49 · 网格半精度 UV 与图块偏移](cases/mesh-half-precision-uv-tile-offsets.md) | mip 0 点采样还错先检查构建后顶点；相同图案放到大坐标 UDIM 格子，量化可从零偏到四纹素。 | UE4 原帖图与修复反馈、UE 5.6 文档和 CPU 转换算例；未在本地引擎或真机测。 |
| [TA-50 · 多层折射透明与场景颜色](cases/layered-refraction-scene-color.md) | 后层画了却没进前层快照，调排序不会增加它的折射；前景错色另查偏移点深度和过滤。 | URP 6000.0 源码、Crytek 原文和 Catlike 原图；未做本地双层 A/B 或真机计时。 |
| [TA-51 · 骨骼重定向时的脚滑](cases/retargeted-locomotion-foot-skate.md) | 先量真实步距和支撑点漂移；根轨迹对齐后再按源接触锁脚，同时验膝盖和松锁跳变。 | Epic 工作流及 UE 5.5 接口、Kovar 等作者实验说明和算例；未目视步态视频，无本地或真机验证。 |
| [TA-52 · 地形分块 LOD 的裂缝](cases/terrain-lod-boundary-cracks.md) | 固定粗细级有缝先验证边缘高度与拼接；只有切换才露底，查共同边目标和块入场时机。 | GPU Gems 2 原图与实现、CesiumJS 裙边源码和剖面算例；未本地或真机复现。 |
| [TA-53 · 可变速率着色的画质与收益](cases/variable-rate-shading-quality-and-cost.md) | 海岸细节不适合整批降速；先核实际速率和画面，再分开量着色通道、速率图生成与整帧。 | Firaxis 原图及帧率、UE 5.4 公开 GPU 计时、D3D12 和 AMD 手册；缺逐通道瓶颈和本地或目标设备测试。 |
| [Paopu 程序化晶体](paopu-crystal.md) | 先分开对比切面、透光、内部层次和轮廓，再评估体积步进是否值得。 | 作者展示与公开制作资料；未取得完整源码与设备数据。 |
