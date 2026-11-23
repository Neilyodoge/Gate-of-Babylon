# TA-40 · 反射探针：倒影错位先修投影，门口变色先查权重

**制作决定**：一间房内固定倒影的位置不对，先固定探针选择和捕获，只改盒投影与盒面尺寸；物体跨门才突然变亮或换了环境，先查影响体积、混合距离和默认环境的剩余权重。两个问题都可能表现成亮斑跳动，因此用低粗糙金属分别观察形状和亮度。

## 原始对照和不同管线的边界

- **局部倒影 · Catlike Coding Unity 5.4**：[《Rendering 8》§3](https://catlikecoding.com/unity/tutorials/rendering/part-8/#3) 的地镜[校正前原图](https://catlikecoding.com/unity/tutorials/rendering/part-8/box-projection/floor-mirror-wrong.png)里，镜中深色柱影和蓝色区域沿地面错位；[盒投影后](https://catlikecoding.com/unity/tutorials/rendering/part-8/box-projection/floor-mirror-projected.png)几根柱子的反射更靠近对应柱脚，但倒影还有明显的模糊和形状误差。作者随后还调整盒面与探针高度以处理地镜问题。盒投影用反射方向与代理盒求交，再从捕获点朝交点采样已有立方图；只重新定位已有捕获，不能补回捕获时缺掉的物体，也不保证精确平面镜倒影。
- **房间交界 · 同一作者的球列**：[§4.2](https://catlikecoding.com/unity/tutorials/rendering/part-8/#4.2) 的[盒不重叠](https://catlikecoding.com/unity/tutorials/rendering/part-8/blending-reflection-probes/without-overlap.png)与[重叠原图](https://catlikecoding.com/unity/tutorials/rendering/part-8/blending-reflection-probes/with-overlap.png)中，扩大第二个盒后，交界附近中间几颗球的深色反射带出现了混合，其余球变化很小。作者直接在 Renderer 检查所用探针及权重。这是旧版管线的静态对照，不能当作 Unity 6 的逐像素混合或运动连续性实测。
- **逐像素权重 · URP 6000.0**：[GlobalIllumination.hlsl 的局部校正与基础权重](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl#L214-L240)给出两个不同计算：交盒得到每只探针的采样方向；距最近盒面的内侧距离除以 `Blend Distance` 后限制至 0–1，作为基础权重。[后续着色分支](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl#L248-L359)中，普通 Forward 的混合分支处理两个候选和优先级；Forward+ 逐个遍历当前簇的探针，不能照搬“两只”的说法。该版本源码在本地权重总和低于 0.99 时把剩余部分给全局反射。两只同优先级、等尺寸的探针若在门中像素距各自最近盒面都为 0.25 m，Blend Distance 同为 1 m，则基础权重各 0.25，**剩下 0.50 是全局环境**；这是固定候选的算例。见[URP 说明](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/reflection-probes/introduction)的盒面权重与环境回退。
- **影响盒与投影盒是否独立要看管线。** [Unity 6000.0 Reflection Probe 面板](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/reflections/class-reflection-probe)中的 Box Size 与 Offset 既定影响范围也用于 Box Projection；URP 要同时在资产和探针启用盒投影。为补门口权重而放大盒子，可能连屋里倒影一起改了。相比之下，[Unity 2022.3 HDRP Reflection Proxy Volume](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Documentation~/Reflection-Proxy-Volume.md)默认复用 Influence Volume，但允许以形状匹配的独立代理盒贴房间的墙，同时让影响体积在门口有过渡。

## 怎样对照与取预算

1. 用两间反射颜色和展品轮廓不同的房间，放一颗低粗糙金属球及一块贴墙金属面。固定曝光，先只用一个烘焙探针，关闭 SSR 等其他反射路径；只切盒投影，检查倒影与实体在墙边是否对齐。代理盒独立时保持影响体积不变；URP 调盒之后回归权重。捕获点变动时重烘并单独对照。
2. 固定捕获和投影，走两条同速路径：正穿门口、在室内贴隔墙横移。记录候选探针、实际本地权重和全局回退，找过早染色、门口亮带、双重倒影和硬切。门口开始出现天空色时，先验两盒是否都有资格且本地权重是否不足；再单改重叠宽度或 Blend Distance。放宽重叠后另验隔墙路线，避免门口平滑但隔壁房间提前串进来。
3. 静态房间先以烘焙反射为基准。确需更新的探针，可比较脚本按需刷新、裁剪待捕获的层与时间切片。[Unity 6000.0 时间切片文档](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rendering/reflectionprobetimeslicingmode)记录 All Faces At Once 同帧渲六面、其余工作摊开，总更新 9 帧；Individual Faces 总计 14 帧，并可能在这期间光照变化时留下不一致。数字是更新跨度而非 GPU 毫秒。捕获与预过滤的峰值、多个探针采样的着色开销、更新可见延迟分开抓，别把盒校正和实时捕获成本算成同一笔。

**状态**：已核对四张作者静态原图、URP 源码与两条管线文档。权重为算例，尚无跨门动态、目标设备与实时刷新计时对照。
