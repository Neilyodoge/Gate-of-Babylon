# 软粒子与场景深度：先验读到什么，再算加了多少钱

**一句话结论**：接触处有明显的硬交线，且当前相机已在粒子绘制前产生可采样的有效深度，就优先复用它；若必须提前拷贝深度或多绘一次场景，分别测资源生成与粒子读取的成本，再和抬高发射位置、只给贴地层做淡化等做法比较。

## 证据与决策

- **开关原图**：[Unity 6000.0，Soft Particles 两张图](https://docs.unity.com/en-us/engine/6000.0/manual/particle-color#soft-particles)。同一团白雾穿过灰方块：关闭时可以看到横穿方块面的直线切口；开启后那条线变成随深度差的平滑过渡。URP 的[粒子材质文档](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/particles-unlit-shader.html)要求透明表面和 Depth Texture；[URP 6000.0/staging 的 Particles.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Particles.hlsl#L86-L118)采样屏幕位置的 `_CameraDepthTexture`、把场景深度转到眼空间，再与粒子深度比较得到淡化。它处理的是该纹理里写入的表面；正常不写相机深度的半透明粒子和水面不会因此互相软化。
- **先后顺序决定能不能复用**：[URP 6000.0 Render Graph 排程](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L1048-L1121)会按深度需求和拷贝模式排 Copy Depth。普通 Forward 路径在有完整预通道且不开 Depth Priming 时可以直接产出可采样深度；同样有预通道，若启用 Depth Priming 则先写深度附件，可能还需要拷贝。深度被要求但不能拷贝或强制预通道时，[预通道判定](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L1701-L1714)成立；[CanCopyDepth](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L1904-L1917)明确排除了 GLES 的 MSAA 深度解析拷贝。若实帧中仅在 [AfterTransparents](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L1397-L1416) 才拷贝，同一相机的普通透明粒子此时已经画完。先抓帧看绑定、生产 Pass 和排序；看到 Depth Texture 开着或有 DepthOnly 事件还不够。
- **低分辨率反例**：[GPU Gems 3 第 23 章](https://developer.nvidia.com/gpugems/gpugems3/part-iv-image-effects/chapter-23-high-speed-screen-particles)，[图 23-3](https://developer.download.nvidia.com/books/gpugems3/23fig03.jpg)把 8×8 屏幕像素折成一个离屏像素并关上采样过滤：中图点取样过度遮住背景粒子，手臂边出现黄圈；右图取四个样本里最远的深度，黄圈减轻，但粒子会侵入物体轮廓。[图 23-4](https://developer.download.nvidia.com/books/gpugems3/23fig04.jpg)的 2×2 降采样加线性放大条件下，人物腿边仍可对比到点取样的细亮边。离屏低分辨率粒子和深度降采样是这组实验的条件，不等同于 URP 普通 Render Scale。若尝试相同方向的筛选，用眼空间判远近；原始 reversed-Z 不能照抄取数值最大。

## 怎么验收

1. 用同一组雾贴片穿过水平地面和细墙，锁粒子种子、镜头、覆盖和曝光。先在同一生产路径下只切材质软化，确认交线消失而主体密度没塌；再加薄遮挡、透明水面和正交相机，检查错误消失、穿边或稳定性。用眼空间深度差调距离，避免拿未线性化的深度缓冲值直接相减。若问题是遮挡缺失、采样坐标不匹配或纹理还未写好，先修数据，不加大淡化范围掩盖错误。
2. **把两笔开销拆开**：Depth Texture 的生产已保持开启时，只换粒子材质测透明绘制的增量；保留相同材质和镜头，再对照是否需要生成或提前生成深度纹理，记实际 Copy Depth、DepthOnly／DepthNormals、附件存取及 GPU 帧时。若其他效果必须读深度，不强关，改在没有其他需求的测试镜头评估整条链。注意轻量粒子也可能让整张相机图多一次深度处理；GPU 片上合并时以可见计数和完整帧时为准。
3. 若要把离屏粒子做半分辨率，对照全分辨率基准，在移动相机时同时找轮廓两侧的光晕、穿边、闪动。分开改变粒子缓冲和深度生成尺寸；近景有细前景时可保留关键粒子为全分辨率，或单独对比边缘分辨率修复的成本。目标机记录图形 API、MSAA、实际纹理尺寸、Pass 和稳定后的帧时区间。

**状态**：已看 Unity 和 GPU Gems 的公开原图及 Unity 6000.0/staging 源码；没有在本地运行 Unity 或在目标设备做 A/B。降采样取舍来自作者的离屏示例，移动端净成本需另测。
