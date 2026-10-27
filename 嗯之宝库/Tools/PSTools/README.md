# Photoshop UI 外描边距离烘焙工具

当前编码：`TL_UI_SDF_OUTSIDE_V3`。用途是 **外描边**：主体内部完全不透明，只有透明区域保存向外扩展的距离灰度。不计算内部距离，不保存原始软边或半透明效果。

## 使用

1. 在 Photoshop 打开原始的 **8 位 RGB、透明背景** 图片或 PSD。
2. 文件 → 脚本 → 浏览，运行 `TL_UI_SDF_Baker.jsx`。
3. 默认距离范围 **16**、额外留边 **16**。工具自动按原始 Alpha **> 0.5** 判断主体（8 位输入为 A ≥ 128），其余视为透明，无需手动二值化。
4. 点击“生成 SDF”，得到新的未保存结果文档。原始文档不修改，结果是可见图层的合成图。
5. 按原有 PS 流程保存带 Alpha 的 PNG 等格式。
6. Unity 使用 `TL/UI/ImageOutlineSDF`，材质 **SDF Encoding 选 OutsideDistance**，**SDF Range** 与烘焙范围一致，默认 16。继续使用现有 **ASTC 8×8**，过滤使用 Bilinear。

**必须从原图重新烘焙。** 旧 V1 图对应 `LegacyPacked`，前一版 V2 图对应 `SignedDistance`；不能只切材质模式而不换贴图。Shader 保留这些旧模式，默认仍是 LegacyPacked，避免已有资源被自动改坏。

JSX 和 `tl_ui_sdf_core.py` 保持在同一目录。Python 3 是可选的加速后端，只使用标准库；缺少 Python 或 PY 文件时使用纯 JSX。可以直接分享新版 `PS_SDF描边工具.zip`，内含 JSX、PY 和本文档。

## 执行顺序与 RGB

1. 复制原始文档。
2. **先扩大结果画布**：默认四周各加 16 像素透明区，图像居中、不缩放。例如 349×318 → 381×350。
3. 读取扩完画布的合成像素，按原始 Alpha 的 0.5 阈值得到二值主体。
4. **只计算透明像素到主体的距离**，写入新 Alpha。
5. 打开新的 SDF 结果文档。

像素计算逐字节保留输入合成图的 RGB，不进行 RGB 模糊、扩色或重采样。只有 `resizeCanvas`，没有 `resizeImage`。原始文档的图层、历史和尺寸不变。

## A 通道如何存储

### 二值主体

```text
mask = originalAlphaByte >= 128 ? 1 : 0
```

这是对输入透明度的判断；与 RGB 的黑白或亮度无关。原始阴影、羽化和其他半透明效果不再保留。

### 只向外存距离

用一次二维欧氏距离变换求 `d`：透明像素到最近主体像素中心的距离，单位是贴图像素。主体内部直接写 255，不另算到边界的内部距离。

```text
主体：alphaByte = 255
透明区域：alphaByte = min(254, round(255 * saturate(1 - d / R)))
```

R 为范围，默认 16。越靠近主体，灰度越高；距离达到 R 时为 0。距离 1、4、8、12、16 像素时，外侧 A 分别约为 **239、191、128、64、0**。

255 保留给主体，其余 0–254 用于外侧距离，没有将半个 A 通道留给原始透明度或内部距离。距离步长约为 **R/255**；R=16 时约 0.0627 贴图像素，相比此前外侧只用半段灰度，量化步长约减半。8 位仍是 256 级，并不是所有图都一定用满每个灰度。

严格说这是供外描边使用的单侧距离场，内部距离为 0。工具名称保留 SDF，但此版本不支持基于内部距离的内描边。

## Shader 解码

一次采样的核心关系如下：

```hlsl
float4 texel = tex2D(_MainTex, uv);
float d = (1.0 - texel.a) * R;
```

**输出 A 的 0.5 不再是主体边界。** 例如 A≈0.5 表示外侧约 R/2 像素，不能将最终 A 再按 0.5 二值化。

主体像素中心 d=0，相邻外侧像素中心约 d=1，交界近似在 d=0.5。Shader 在此处做窄过渡重建主体边缘；内部平坦的 A=255 保持完全不透明。屏幕描边宽度通过 UV 导数换算为贴图像素，并从交界向外延伸：

```hlsl
float bodyCoverage = 1 - smoothstep(0.5 - aa, 0.5 + aa, d);
float extent = 0.5 + outlineWidthScreenPx * texelsPerScreenPixel;
float outlineCoverage = 1 - smoothstep(extent - softness, extent + softness, d);
```

最终主体显示原始 RGB，外侧显示描边颜色，描边之外输出 Alpha=0。实际 Shader 会限制超出烘焙范围的描边，并保证 A=0 的远处背景透明；具体保护见 `TLUIImageOutlineSDF.shader`。默认描边宽度 7、软度 2 屏幕像素，仍只采样一次贴图，保留 UI Stencil、裁剪和 Alpha Clip。

PS 默认预览会直接把距离灰度当透明度，因而看到半透明外圈。是否显示描边应在配套 Shader 中判断，不能以普通 PS 透明预览代替最终效果。

## 范围、限制和运行

- 留边和距离范围应覆盖所需描边。原图已留足空白时可以不额外扩画布；需要补留边时，必须让新增区域参与距离计算。
- 范围越大，8 位距离精度越低。修改 R 后同步材质 SDF Range；画布不足时描边仍会被截断。
- 二值轮廓和 RGB 仍受原图分辨率限制；ASTC 8×8 是有损压缩，不能保证完全没有毛刺。本次不改变压缩格式。
- 不要用已经烘焙的结果再次烘焙。工具会拒绝仍保留 V1/V2/V3 标记层名的结果；重新打开 PNG 或改名后仍需自行确认输入是原图。
- 最大输出为 4,194,304 像素，包含留边。保留进度与取消，计算默认不限时；大图推荐 Python。
- 仍是生成新的合成结果文档，不提供仅处理选中图层功能。
- 建议保持原尺寸导入、使用 Bilinear，并为图集中的距离区域留足间隔；不要将外侧距离灰度作为普通透明度裁掉。

## 自动化接口

```javascript
$.global.TL_SDF_NO_AUTO_RUN = true;
try { $.evalFile(new File("D:/path/TL_UI_SDF_Baker.jsx")); }
finally { $.global.TL_SDF_NO_AUTO_RUN = false; }
var result = $.global.TLUISDFBaker.bake(app.activeDocument, {
    range: 16, padding: 16
});
// result.encoding === "TL_UI_SDF_OUTSIDE_V3"
// result.outputDocumentId 是新的未保存文档。
```

可选 `backend: "python"` 或 `"extendscript"`；省略时自动选择。`maxComputeMs: 0` 默认不限时，正数为毫秒上限（最多 600000）。兼容旧调用的 `threshold` 参数固定为 128，其他值会报错。
