# 角色过门时的间接光：定位采样位置和跨墙漏光

**一句话结论**：角色部件一起或各自整体跳亮，先查用的是传统 Light Probe Group 的物体采样点还是 APV 的逐像素采样；如果固定机位、角色贴墙横移也提前染上墙外颜色，检查有效探针来自哪侧。区域过滤适合固定隔间，跨区角色还需要单独验过渡。

## 公开证据

- **传统探针与采样点**：[Unity 6000.0 · Light Probes and moving objects](https://docs.unity3d.com/6000.0/Documentation/Manual/LightProbes-MovingObjects.html)的[无探针原图](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/LightProbes-MovingObjects-2.png)和[加探针重烘原图](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/LightProbes-MovingObjects-4.png)中，靠红楼一侧的救护车从偏灰变成偏粉。正文说明，传统方案按物体位置在四个探针组成的四面体内做插值；[Renderer.probeAnchor](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/renderer/probeanchor)可指定用于查找探针的 Transform。四面体交接不必然产生跳变：理想线性插值在共享面上，两个四面体的非共享顶点权重都趋于零。救护车两图说明探针传递了反弹色，没有证明物体穿门的连续性。
- **APV 跨墙的实图**：[Unity 6000.0 · APV light leak prevention with rendering layer masks](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/probevolumes/rendering-layer-masks-apv/prevent-light-leaks)中，[分层关闭](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/RenderingLayerEffect_02.jpg)的暗室里，背墙顶部和墙角有偏亮的反弹色；[开启分层](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/RenderingLayerEffect_01.jpg)后这些区域明显更暗。这是静态前后图。该版本文档说明：APV 以每像素周围八个探针作为候选，区域掩码使其优先使用同层探针；实际选取还要看有效性和漏光处理。Baking Set 中最多四个 APV 掩码（每个可含不止一个渲染层）；Renderer 一个都不匹配时会用所有有效近邻，有多个匹配时可取多区。启用掩码增加烘焙时间和内存，文档未量化。
- **采样位置与烘焙数据是不同杠杆**：[Unity 6000.0 APV 漏光排查](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/probevolumes/fixissues/troubleshoot-light-leaks)区分运行时沿法线或向相机偏移采样点，以及重新烘焙时调探针间距、Virtual Offset 和 Dilation；局部 Probe Adjustment Volume 可标记坏探针，更新烘焙后再验有效邻居。[Options Override](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/probevolumes/options-override-reference)给出 Leak Reduction 的 Performance 只采样 APV 一次，Quality 取一到三次；这是 APV 采样次数，不是纹理 fetch 数或实测 GPU 时间。View Bias 随相机变化，需从门两侧看同一位置。

## 最小对照和选法

1. 用不反光的中性角色替身，室外亮而带明显色相，室内暗，固定曝光、光源、分辨率和姿态。暂时隔离实时直射、反射与后处理，只看间接光。同一机位记录两条路线：正穿门口、保持在室内贴墙平移；再让角色站定，只把相机放到门的另一侧。看头、胸、脚在相同位置和朝向的亮度及色相，区分正常渐变、过早染色和相邻帧突跳。
2. 传统 Light Probe Group：选中 Renderer 看参与插值的四面体；若不同身体部件跳变不同步，只把相关 Renderer 锚点绑定在同一个随角色移动的稳定位置对比，避免被各部件范围变化带偏。锚点正确但探针仍取到隔墙一侧时，补门内外两边过渡样本并重烘，复查四面体有没有跨到不该混的空间。
3. APV：在 Rendering Debugger 显示 probes、有效性和区域归属。若室内探针本身正常，但隔墙探针被用上，室内固定物件可先试 APV 掩码；要穿过门的角色不要直接在门线硬切 Interior/Exterior，以免把采样范围的变化变成一帧亮度开关。给角色保留两区资格作为基线，再试门边局部加密或保留过渡带；双区时也要看贴墙路径是否重新漏色。如果室内选中的探针已经带错色，先复核几何遮挡、烘焙偏移或无效探针的 Dilation 来源；若缩小或移动后仍异常，再考虑局部调整体积后重烘。
4. 只对通过两条路径验收的版本比较成本。局部加密记录 brick、烘焙时间和运行时探针内存；APV 掩码记录烘焙及内存增量；Leak Reduction 模式在同一镜头、分辨率和硬件上测受影响的照明通道和整帧 GPU 时间。保持实际玩法镜头再回归一次，避免用近景修复换来远景骤变。

**状态**：已查看 Unity 的救护车和暗室静态对照，核对 6000.0 采样和漏光文档；尚无同一角色穿门的公开逐帧 A/B、本地烘焙或真机数据。
