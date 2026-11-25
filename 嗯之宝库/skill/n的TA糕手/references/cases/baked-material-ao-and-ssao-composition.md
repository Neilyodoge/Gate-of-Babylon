# TA-161 · 烘焙 AO 的材质再开 SSAO 后缝隙过黑

**制作决定。** 先确认烘焙 AO 放在材质 Occlusion、底色还是光照贴图，再看 SSAO 的 `After Opaque`。Unity URP 6000.0 的常规 Lit Forward 路径，对间接光取材质与屏幕遮蔽的较小值，不相乘；变暗若发生在纯直射光下，先查 `Direct Lighting Strength`。开启 `After Opaque` 则另当别论：它按屏幕遮蔽乘已经画好的颜色，不能靠把直射影响调到 0 来隔离直射光。

## 公开实现

- **材质遮罩和间接光。** Unity Graphics `6000.0/staging` 的 [LitInput.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl) 把 Occlusion Map 的 G 通道按强度与白色插值，交给 `SurfaceData.occlusion`。在非透明 Lit 的常规 PBR 路径，[AmbientOcclusion.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl) 的 `CreateAmbientOcclusionFactor` 对屏幕与材质保留系数取 `min`。[Lighting.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl) 把它交给 `GlobalIllumination`；[GlobalIllumination.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl) 的环境 BRDF 整体乘这个系数，包含间接漫反射与环境反射。SSAO 关掉时屏幕系数是 1，材质遮罩仍然生效。
- **常规路径的直射光。** `GetScreenSpaceAmbientOcclusion` 另输出 `lerp(1, S, k)`，其中 `S` 为屏幕保留系数（越小越暗）、`k` 为 `Direct Lighting Strength`。[RealtimeLights.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl) 在相应变体用它乘主灯和走该重载的逐像素附加灯颜色，不使用材质 AO。直射变暗不等于材质 AO 已乘了两遍；取样条件和调试开关也要保持一致。
- **同版本的反例：打开 After Opaque。** [ScreenSpaceAmbientOcclusionPass.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/ScreenSpaceAmbientOcclusionPass.cs) 把结果写到相机颜色，只有非 After Opaque 路径启用供光照采样的关键字。[ScreenSpaceAmbientOcclusion.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/Utils/ScreenSpaceAmbientOcclusion.shader) 三种模糊的 After Opaque Pass 都输出 `(0,0,0,ao)`，用 `Blend One SrcAlpha, Zero One` 得到 `目标 RGB × ao`，并保留目标 Alpha。像素里已有的直射、间接和自发光会一起被压暗。以上范围是该版本的这些调用，其他渲染路径、自写 Shader 或屏幕混合需查其实际入口。

## 两张遮罩的算例

设同一缝隙材质遮罩的保留系数 `M=0.6`，两条路径的有效屏幕保留系数都为 `S=0.5`。表内读值是各光照项相对于无材质 AO、无 SSAO 的保留比例，忽略后续曝光和显示转换。

| 设置 | 间接光 | 直射光 |
| --- | ---: | ---: |
| SSAO 关闭 | 0.60 | 1.00 |
| 常规，直射强度 0 | 0.50 | 1.00 |
| 常规，直射强度 0.25 | 0.50 | 0.875 |
| After Opaque，直射强度 0 | 0.30 | 0.50 |

当 `S<M`，先把材质遮罩刷白，常规路径的间接光仍由 `S` 决定。此时应先隔离屏幕遮蔽，避免误判成材质坏了。如果 AO 已烘进底色或光照贴图，它已是被光照使用的输入，`min` 只能合并仍独立存在的两个遮罩，不能撤回那份暗色。

## 最小验证

1. 锁相机、几何、质量档和曝光。用常值 G 遮罩做白色与 0.6 组，先关 SSAO 看差异，检查材质确实走 Occlusion 输入。若无贴图且关 SSAO 仍黑，再分别换纯底色、隔离烘焙光照；必要时确认暗色是否来自原始入射光或反射。
2. 保留决定 SSAO 的场景几何，分别只开间接光与只开单个逐像素直射光。在色调映射前看线性 RGB，记录实际 SSAO 纹理与最终合成前后的像素。先测 `After Opaque` 关闭时强度 0 与 0.25，再打开它。两种模式若生成的 `S` 不同，各用自己的值算预期；有疑问再用只含自发光的不透明材质辨认整色乘法。
3. 确定问题后再选修法：只希望压暗间接光，先在常规路径将直射影响归零；仍黑且屏幕系数更低，再调 SSAO 半径或强度，逐个镜头确认接触区。若制作约束要求保留 After Opaque，比较改变屏幕遮蔽幅度与改管线的画面和 GPU 代价，不能仅凭开关推断两种路径的性能。

**验证状态。** 已读 Unity URP 6000.0 的材质、灯光和合成源码；表格是算例，未做本地抓帧、深度边界验证或真机计时。
