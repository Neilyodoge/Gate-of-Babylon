# Unity Alpha 导入与透明边缘

**问题**：同一张 RGBA 贴图既提供颜色也存掩码，为什么透明边缘会出色边，或者 A 看起来对了而 RGB 数据不对？先区分 Alpha 来源、透明边缘扩色、目标平台的导入格式，以及实际材质采样。

## 来源明确：Unity 6000.0 LTS

核对日期：2026-11-23。

- [TextureImporter.alphaSource](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/textureimporter/alphasource)：决定导入纹理的 Alpha 如何生成。排查没有变化的 Alpha 时，先核对源图 Alpha 的内容和这项设置。
- [TextureImporter.alphaIsTransparency](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/textureimporter/alphaistransparency)：若 Alpha 表示透明度，可将可见 texel 的颜色扩到全透明区域，减轻过滤时的边缘伪影。开关会使不可见 texel 的颜色值不再有保证；HDR 纹理不支持此功能。它负责 RGB 扩色，不负责决定 Alpha 的来源。

## 用法与边界

- **普通颜色图且 A 为透明度**：可把扩色作为候选解决方案。线性过滤可能把边缘附近的透明 texel RGB 混入可见边缘；先在固定底色与原始混合模式下比对，避免把混合模式、压缩或 mipmap 的问题都归于同一开关。
- **RGB 存独立数据且在 A=0 处依然要读**：优先禁用透明扩色，再确认各通道在目标平台的格式与采样结果。若需要同一像素同时提供透明边缘颜色和必须保留的独立数据，考虑拆图或重新分配通道。拆图可能增加采样和带宽，需在目标场景比较。

## 最小对照

制作一张合成 RGBA：不透明区域为亮色，全透明区域的 RGB 放可辨认的数据值。复制两份，保持 Alpha 来源、平台格式、sRGB、过滤、材质与混合一致，只改 `Alpha Is Transparency`。先用点采样单独观察各通道，再用双线性过滤在浅色、深色背景下观察边缘。先比无压缩和 mip 0，再单独切换到目标设备格式与正常 mip，确认 RGB 与 A 各自的变化发生在哪一步。

**状态**：文档已核对。该对照未实际运行，未测画面收益或设备成本，也未推断其他 Unity 版本或项目材质的实际结果。
