# TA-48 · 新灯一进视野，旧阴影变糊：先看切块的请求尺寸和实际尺寸

**制作决定**：固定相机与原有灯，只打开一盏新灯的阴影。如果原灯的请求尺寸没变，而多个实际切块同时缩小，就按阴影切面的面积预算处理；优先节省不关键的切面或请求尺寸，别先提高原灯档位。若某盏灯的请求先变，回查分辨率设置；图集里的阴影都没变时，另查画面里的滤波与光照叠加。

## 公开证据

- **Unity URP `6000.0/staging` · 请求与分配。** [图集分配源码](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowAtlasLayout.cs#L105-L182)跳过主方向光，按附加灯分切面记录 `requestedResolution`：聚光 1 块，点光的立方体 6 面。该版本[以请求尺寸降序排序](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowAtlasLayout.cs#L326-L356)，同尺寸时先硬阴影，再以相机距离等决定先后。这里的相机距离排序与“请求尺寸改变”是两件事。
- **Unity URP `6000.0/staging` · 退化。** [面积估算](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowAtlasLayout.cs#L326-L339)按请求边长的平方求和，容量不够时把共同缩放因子按 2 的幂提高；[实际装箱](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowAtlasLayout.cs#L200-L286)再以 `requestedResolution / factor` 尝试，找不到格子还可重试。初估不满足尾端请求的最低尺寸会去掉这盏灯的请求；还存在非 Structured Buffer 路径的切面数限制。阴影 Pass [分别报告删除、仍装不下与整体缩小](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowCasterPass.cs#L405-L430)，并用[实际分配的边长和偏移](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/AdditionalLightsShadowCasterPass.cs#L605-L624)写切块。日志按请求组合控制重复次数，不能用没刷日志来证明没改分配。
- **作者的图集原图 · Catlike Coding 自定义 SRP（更新至 Unity 2022.3.5f1）。** [三盏聚光](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/spot-light-shadows/spot-shadow-map.png)是 2×2 布局，三个象限可见深度图案；[两盏点光](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/point-light-shadows/point-shadow-map-back-faces.png)里可看到更密的 4×4 切分。[六块分配](https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/#2.1)解释了为什么两盏点光需要十二块，纯黑区域也不能单凭颜色判为空位。两图演示切面和布局；它们来自自定义 SRP，不能作为 URP 的降档前后截图。

## 面积算例

1024² 的图集，四盏各请求 512² 的聚光正好铺满。加入第五盏同档聚光，所需面积达 1.25 倍。按上述 URP 面积估算，在没有其他限制和候选剔除的前提下，五块一起从 512 降到 256 边长；每个旧切块只剩四分之一纹素。此时只把新灯降到 256 仍溢出，因为旧四块已经占满。要么明确腾一块或一部分原有预算，要么让新灯不占实时阴影或加大图集，之后再用抓帧核对布局。

## 最小对照

1. 固定主相机、画质档、阴影距离、原灯和投影物；同一局部灯分别开关实时阴影，保留它的照明。用 Frame Debugger 或 RenderDoc 找 `_AdditionalLightsShadowmapTexture`，对照原灯每个切面的 viewport 像素宽、分配偏移、是否真的有绘制，记录请求尺寸和共同缩放因子。只比较整张图集的宽高会漏掉切块变化。
2. 请求不变但多个切块一起缩小：先比较去掉一盏低优先级灯的实时阴影，再比较只降它的请求尺寸。需要全向影子才保留点光；仅需要单方向投影时可另试聚光。若旧灯切块直接消失，先看是否掉出该路径的切面数／最小尺寸限制，再确认是否有烘焙或无阴影回退。不要用灯泡数量代替切面数。
3. 另做一组不增加灯的测试，只改变目标灯的请求尺寸（或在待查管线里改变其投影到屏幕的大小），记录请求、分配和同一阴影边的画面变化。请求和切块都没变时，单独对照 PCF、偏移、投影覆盖、阴影距离或时域滤波。最终用最拥挤的镜头测切面占用、阴影 Pass GPU 时间和显存；图集边长翻倍只说明理论纹素空间变成四倍，无法预报总帧时。

**状态**：已核对 Unity URP 源码及 Catlike Coding 两张图集原图；512→256 为照源码所做的算例，未在本地运行开关新灯的 A/B，也未做真机测试。
