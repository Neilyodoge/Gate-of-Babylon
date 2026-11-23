# 投射贴花误染：分开查投射盒、接收对象和表面朝向

**一句话结论**：贴花越过墙体厚度，先缩投射盒深度；同朝向的邻物必须留在盒内时，用接收层或局部材质隔离；只有侧面擦边或背向处被拉花时，再调角度渐隐。先让误染消失，再确认目标贴花没有被一起裁掉。

## 公开证据

- **同镜头接收层对照**：[Unity 6000.0 · Enable Rendering Layers for Decals in URP](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/rendering-layers/decals#example)，[油漆桶原图](https://docs.unity.com/api/media?file=/engine/6000.0/media/images/rendering-layers-decal-example.png)。图 1 中橙色线条染上白桶；图 2 只取消桶的 `Receive decals` 渲染层，桶上颜色消失，墙和地面上的橙色图样保留。这个例子是匹配投影器与接收网格的 Rendering Layers，不是修改 GameObject 的普通层或相机剔除掩码。
- **边界渐隐和受光朝向是两件事**：[Epic UE5《Decal Materials》· Decal Actors](https://dev.epicgames.com/documentation/en-us/unreal-engine/decal-materials-in-unreal-engine#decalactors)同一砖墙两图里，转动投射盒后橙色区域变窄，一侧出现明显淡出；原文将它解释为达到盒范围时的渐隐，不能用这组图证明法线角度过滤。[Actor Receives Decals](https://dev.epicgames.com/documentation/en-us/unreal-engine/decal-materials-in-unreal-engine#actorreceivesdecals)的原图里，绿盒穿过红柱和蓝球，但金色图样仍只出现在墙上；作者说明另两件不接收贴花。UE 示例的 `Receives Decals` 是该网格的接收开关，关闭会影响其他贴花，粒度不同于给某组投影器做专用接收层。[Unity 6000.0 · Decal Projector](https://docs.unity.com/en-us/engine/6000.0/manual/visual-effects/decals/renderer-feature-decal/projector-reference)分别定义了局部 Z 的 `Projection Depth`，以及按投射与表面朝向夹角渐隐的 `Angle Fade`。
- **移动端成本与重叠边界**：[Unity 6000.0 · Decal Renderer Feature](https://docs.unity.com/en-us/engine/6000.0/manual/visual-effects/decals/renderer-feature-decal/reference)明确说，屏幕空间方案启用 `Use Rendering Layers` 会让 URP 创建 DepthNormal 预通道；DBuffer 本身需要该预通道。屏幕空间从深度重建法线的低、中、高档分别取 1、3、5 次深度样本，这是文档参数，不是帧时。[Epic 性能说明](https://dev.epicgames.com/documentation/en-us/unreal-engine/decal-materials-in-unreal-engine#performanceimplications)把覆盖和材质复杂度列为主要成本；[重叠限制](https://dev.epicgames.com/documentation/en-us/unreal-engine/decal-materials-in-unreal-engine#recreatinglegacybehavior)特指用 DBuffer Material Expressions 重做旧式 Stain 的方案，叠放时不能按旧法混合。不能把这个限制推广到所有投射贴花。

## 最小排查与验证

1. 固定投影器和镜头，摆目标薄墙、墙后同朝向平板、与墙拐成直角的侧面、以及移动穿过投射盒的道具。先只缩深度或调整投射枢轴，让不该接收的面离开盒；验收同墙不同 LOD、必要的边缘包裹和预期移动范围。若邻物不能避开盒，且同样面向投射器，角度渐隐分不出两个物体，改接收层；如果误染的是同一网格里同朝向的两块面，整个渲染器共用一层时，再试收紧盒、局部 Mesh Decal 或在底材质里合成。
2. 暂把角度渐隐放宽作为对照，再收窄观察墙边是否先淡出。目标正面近距离不应变稀；如果要故意跨过拐角或触及斜坡，保留必要的角度范围。常规 URP 投射不覆盖透明表面，检查目标材质类型，避免把不受支持的表面当成接收层配置错误。
3. 帧时比较做两组：一张与四张同位置同大小的贴花，控制总覆盖并改变重叠深度；固定四张的材质和数量，再改变它们的屏幕尺寸，分离覆盖影响。用 Frame Debugger 确认实际 Technique、DepthNormal 和贴花绘制是否出现；在相同可见效果下比较收紧投射盒、启用接收层或改局部材质，记录目标设备的预通道、贴花和整帧 GPU 时间。若场景本来就需要预通道，单独核对增量，别把整个预通道都算在接收层开启上。

**状态**：已看 Unity 油漆桶 A/B、Epic 投射边缘和接收原图，核对 Unity 6000.0 路径；未做本地复现、目标设备计时或重叠量化。
