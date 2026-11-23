# 移动端贴图：块大小、Alpha 和通道打包

**问题**：把多张遮罩合成一张 RGBA，是省带宽还是把小图白白拉大？先按实际导入尺寸与格式算 GPU 纹理数据，再看能否共享 UV、LOD 和采样状态，并在目标设备检查画面、加载和 GPU 时间。

## 来源明确

核对日期：2026-11-23。下文默认 LDR 2D 贴图。

- [Arm astc-encoder · ASTC Format Overview](https://github.com/ARM-software/astc-encoder/blob/main/Docs/FormatOverview.md)：2D ASTC 每个块固定 **128 位（16 B）**，块覆盖范围可选 4×4 到 12×12。相同块尺寸和纹理尺寸下，RGB 与 RGBA 的块数据大小相同，编码质量可以不同。共享权重对相关通道有效；双平面可以给其中一个通道独立权重，不保证四个独立遮罩都无明显误差。
- [Unity 6000.0 · GPU texture formats reference](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/textures/textures-getting-started/texture-compression-formats/texture-formats-reference#texture-formats-by-quality)：ETC2 RGB 是 4 bpp；RGB + 1-bit Alpha 也是 4 bpp，只能表达全透明/全不透明；需要连续 Alpha 时可用 ETC2 RGBA，8 bpp。EAC R 单通道是 4 bpp，可作为独立遮罩的对照。软边不应用 1-bit Alpha 替代后再宣称同画质。
- [Unity 6000.0 · Choose a GPU texture format by platform](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/textures/textures-getting-started/texture-compression-formats/texture-choose-format-by-platform) 和 [Android Developers · Texture compression format targeting](https://developer.android.com/guide/playcore/asset-delivery/texture-compression?hl=zh-cn)：Unity 推荐 Apple A8 及更新设备的 RGB/RGBA 用 ASTC；较新的 Android GPU 多数支持 ASTC，ETC2 覆盖 OpenGL ES 3.0 及以上设备。仍要按设备、API 和 ASTC profile 核对。Play 可分发适合设备的压缩变体；这与 Unity 加载到不支持 ASTC 的旧 Android 设备后解压为 RGBA32 是两件事。勿将 Unity 6000.0 的 LDR 回退规则外推到 ASTC HDR 或自定义加载器。

## 算一笔，再决定合不合图

ASTC 每级数据量 = `ceil(宽 / 块宽) × ceil(高 / 块高) × 16 B`。有 mip 时逐级累计，边缘和最小 mip 都要按整块计算。以下是 1024×1024 **仅 mip 0** 的纹理数据；不含驱动对齐、CPU 副本或包体压缩。

| 格式 | 纹理数据 |
| --- | ---: |
| ASTC 8×8 / 6×6 / 4×4（RGB 或 RGBA） | 256 / 456.89 / 1024 KiB |
| ETC2 RGB 或 RGB + 1-bit Alpha / ETC2 RGBA | 512 / 1024 KiB |
| RGBA32（示例回退） | 4096 KiB |

- 若材质总用同一 UV、同一 mip 和同一采样状态读取三个 1024² 遮罩，三张 EAC R 共 1536 KiB、三次显式采样；打成一张 ASTC 6×6 约 457 KiB、一次显式采样。前提是压缩后每个遮罩都达标；显式采样次数不直接等于缓存访问、GPU 时间或外部流量。
- 反例：一张 1024² 和一张 256² 的 EAC R 共 544 KiB；如果为了保持精细遮罩的质量，合图要用 1024² 的 ASTC 4×4，就变成 1024 KiB。若小图另有独立 UV、平铺速度、过滤、mip 或流送需求，合图还可能无法减少采样。纯数据遮罩按线性数据导入；颜色和独立数据不要在同样的 RGB 通道内共用不相容的颜色空间处理。使用 A 储存掩码时核对 [Alpha 导入和透明扩色](texture-alpha-import.md)。

## 验证计划

用同一组代表性遮罩，对比独立通道图、合图 ASTC 两种块大小，以及目标分发的 ETC2 方案。先在单通道预览查块状边缘、阈值漂移和渐变，再以实际材质、运动镜头及至少两个 mip 视距验收；同场景保留未压缩参考。各设备记录运行时格式、实际导入尺寸和驻留 mip，测加载时间、纹理内存和稳定 GPU 时间；不同交付渠道分别验证拿到的变体与回退。包体大小单独记录。

**状态**：厂商和引擎文档已核对；表中数据为计算示例，未导入贴图、未发包、未做实机对照。
