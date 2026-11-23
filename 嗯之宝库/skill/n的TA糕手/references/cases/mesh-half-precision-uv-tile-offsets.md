# TA-49 · 网格半精度 UV：远 UDIM 格子错位，先验构建后的坐标

**制作决定**：同一张贴图在 DCC 对齐，导入后窗框、线条等细节在 UV 缝处跳位，而且无压缩、mip 0 点采样也跳，先对照这个 LOD 构建后的 UV 与源顶点，只切全精度 UV。若只有开过滤或读粗 mip 才出现相邻色块，再查采样边界和已生成的 mip。

## 公开证据

- **实际资产与作者修复报告 · UE4。** [Bengaman 原帖](https://forums.unrealengine.com/t/solved-uvs-from-imported-mesh-slightly-distorted-causing-texture-misalignment/151770)描述 3ds Max 网格和 Substance Painter 烘焙贴图在 3ds Max／Blender 对齐，UE4 中错位。他把网格从 UE 导回 3ds Max，发现 UV 顶点位置变了；复制源 UV 顶点位置后对齐。[原图](https://d3kjluh73b9h9o.cloudfront.net/original/3X/e/d/ed1a522e0ba916c0ca9b57f96ea9a847bfbb5849.jpeg)中，中间窗框的上、下边在同一条竖缝处各有小台阶。[第 2 楼](https://forums.unrealengine.com/t/solved-uvs-from-imported-mesh-slightly-distorted-causing-texture-misalignment/151770/2)报告给该 LOD 打开 `Use Full Precision UVs` 后恢复；[第 6 楼](https://forums.unrealengine.com/t/solved-uvs-from-imported-mesh-slightly-distorted-causing-texture-misalignment/151770/6)另有十块 UDIM 中 1005、1015 偏得特别明显、开全精度后解决的报告。目视的是修复前静态原图；修复与 UDIM 结果来自文字，缺修复后同帧或原始顶点数据。
- **反例边界 · 同一资产可能有两个来源。** [另一则 UE 论坛作者的 4K 反馈](https://forums.unrealengine.com/t/full-precision-uvs/311155/3)说大部分偏移来自 Photoshop 图层放置；校正后开全精度，UV 岛边仍有改善。即使全精度对一部分现象有帮助，也要核对源层和构建后坐标。
- **Epic · UE Python 5.6 [`MeshBuildSettings`](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/MeshBuildSettings?application_version=5.6)** 把 `use_full_precision_u_vs`（存储为全浮点精度）和 `use_backwards_compatible_f16_trunc_u_vs`（旧网格的 F16 截断转换）分开；前者是在网格构建时处理存储精度。已经在顶点缓冲丢掉的小数，后续用 32 位 Shader 变量接收也补不回来。独立核对实际 GPU 顶点格式、着色器运算精度与插值。

## 纹素算例

标准 binary16 在 U≈0.75 时，相邻可表示值间隔为 1/2048；在 U≈4.75 时，间隔为 1/256。两者局部图案位置相同，按每块 4096 纹素换算，坐标台阶分别是 2 和 16 个纹素。用 CPU 按就近舍入转换：`0.7509765625 → 0.7509765625`，`4.7509765625 → 4.75`，后一处少了 4 个纹素。16 是相邻值的距离，4 是本例的位移。不是所有顶点都偏 4；旧网格的截断不能套用就近舍入的半步上界。材质若再把 UV 乘以平铺倍数，已有误差也随之放大。

## 最小对照

1. 固定网格、LOD、照明和纹理导入，先用无光照材质、无压缩点采样、强制 mip 0。选跨 UV 缝的细线，并给 U≈0.75 和 U≈4.75 的位置提供相同图案；分别记录源角点 UV、构建后 UV、图案错位纹素数。只切该 LOD 的全精度 UV 并重建，不同时重展 UV 岛。核对 LOD 是否真的相同，接缝两侧角点不能按合并后的几何点比。
2. 半精度与全精度的 mip 0 点采样都对齐，但双线性或远距变色时，用[图集串色对照](flipbook-atlas-mip-bleeding.md)分开检查格内坐标跨界与粗 mip 已混色；切全精度不能清理已写进粗 mip 的邻格颜色。构建后坐标正确而最终采样仍错时，再查材质内部的平铺、取小数、插值精度和导数。
3. 优先给需要精确贴花边、远 UDIM 或高倍平铺的资产单独开全精度，再看实际顶点缓冲。由两个 16 位分量变两个 32 位分量，单组 UV 的理论增量是每渲染顶点 4 字节；多个 UV 组、LOD 和实际 stride 要单独统计。可替代的做法是存连续的小坐标，再在材质或分块数据中处理平铺和整数格号；跨整数边界三角形不能直接逐顶点取 `frac`，否则会改变插值路径。

**状态**：已目视 UE4 原帖修复前截图，读取修复反馈和 Epic 5.6 文档，CPU 核验两个 binary16 数值；未用本地网格重建、抓帧或做真机测量。
