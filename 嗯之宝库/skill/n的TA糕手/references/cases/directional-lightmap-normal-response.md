# TA-151 · 关闭方向光照贴图后烘焙材质的凹凸变平

**制作决定。** 凹凸只在纯烘焙光下变平时，先区分法线图还在不在，以及烘焙漫反射有没有使用它。Unity URP 无方向的静态光照贴图分支只读光照颜色；方向模式才用每像素法线对这份光照重新加权。重要静态表面靠这层细节时，先做同光照 A/B，再按场景和设备决定是否值得加方向图；若方向模式也弱，先确认实际分支和光照有无明显主方向。

## 公开证据

- **Unity 6000.0 [方向模式手册](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/configure-lightmapping-settings/directional)。** 两种模式都有记录颜色和强度的光照图；方向模式另外保存主导方向和方向性。额外纹理会被 Shader 采样，影响存储、显存和烘焙时间。这个模式在 Lighting Settings 资产上设置，不能按单张 lightmap 单独选；叠加加载的各场景需要用一致的方向模式。评估降档时按实际的加载组合一起核对。
- **Unity Graphics `6000.0/staging` 的实现。** [URP GlobalIllumination.hlsl 的 SampleLightmap](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl) 中，静态分支只有同时定义 `LIGHTMAP_ON` 与 `DIRLIGHTMAP_COMBINED` 才采方向图、传入 `normalWS`；只有 `LIGHTMAP_ON` 时调用不接收法线的 `SampleSingleLightmap`。[Core EntityLighting.hlsl 的 SampleDirectionalLightmap](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl) 让光照颜色乘 `(dot(normalWS, direction.rgb - 0.5) + 0.5) / max(0.0001, direction.a)`。减偏置后的向量长度编码方向性，不要将其归一化；Alpha 在该解码里是重新平衡亮度的系数，不能当作普通透明度。这个分支恢复的是烘焙漫反射的法线响应，不能重建多方向的完整光场或直接光高光。
- **公开画面 · Catlike Coding《Static Lighting》，Unity 5.6.0。** [§4 的实时光／无方向烘焙对照](https://catlikecoding.com/unity/tutorials/rendering/part-16/#4) 中，前排柱和横梁从凸凹清楚变为大致平滑；[§4.2 的方向烘焙图](https://catlikecoding.com/unity/tutorials/rendering/part-16/#4.2) 中，柱与梁的纹理重新可见。这是旧版自制 Shader 的静态示例；实时光对照还改变了光照路径，只用来观察细节能否恢复，不比较绝对亮度。文章也明确区分剩余的环境反射细节。

## 两个算例

- 固定一个光照纹素，设方向图采样为 `(0.8, 0.5, 0.5, 0.5)`，两片元的单位世界法线分别是 `(0.6, 0, 0.8)` 和 `(-0.6, 0, 0.8)`。按解码，两者的烘焙颜色分别乘 1.36 和 0.64；无方向分支两者都只拿同一颜色。若 RGB 都接近 0.5，即光照缺少明显主方向，两个法线的系数也会趋近，打开模式不保证能看见强烈起伏。数值是手算样例，不是烘焙器输出。
- 额外一个 2048² 方向图的 mip 0，实际是 RGBA32 时约 16 MiB；若设备上实际以 8 bpp 块压缩格式储存，则约 4 MiB。先查真实格式、Alpha、mip 和驻留数，再加总；资源从一张变两张，不意味着整套光照贴图的磁盘或显存恰好翻倍。Shader 采样点增加也不能直接换算带宽或帧时。

## 最小对照

1. 用带明显斜向纹路的静态平面，光源设为纯烘焙、固定机位与曝光，关实时补光、环境反射和后处理。保持灯、图集尺寸和 UV 一致，只换方向模式重新烘焙。每组再分别用原法线与平法线，比较同一位置的线性烘焙漫反射。用帧捕捉核对实际平面 Draw 的变体和绑定；方向图存在但画面不变时，先验材质是否把像素世界法线送进烘焙分支，再验图里方向性是否足够。
2. 恢复实时灯、反射时一次只开一个，确认它们能不能独立留下凹凸，避免拿最终画面倒推是哪一路。用玩法距离重复对比；若纹路无可见收益，才把关闭方向图列为候选。在目标机分别量实际纹理驻留、对应 Pass 的 GPU 时间，并复查叠加载场景。

**验证状态。** 已核对 Unity 6000.0 文档与 URP/Core 两段源码、查看 Catlike Coding 三张静态原图，算例为推导；未在本地重烘焙、抓帧或做目标设备测量。
