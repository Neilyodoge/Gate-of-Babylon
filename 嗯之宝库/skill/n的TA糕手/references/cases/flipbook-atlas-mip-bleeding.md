# Flipbook 图集串色：先分清采样跨格和 mip 已混色

**一句话结论**：冻结到一帧后，若只在双线性采样时出现邻帧颜色，先查格边 UV 和补边；若粗 mip 用点采样也已混色，要从原帧重做 mip，缩 UV 无法恢复颜色。若只读到本帧透明区的 RGB，处理帧内扩色或预乘约定。

## 证据和适用范围

- **公开源码**：[Unity Graphics 2022.3/staging，Shader Graph FlipbookNode.cs 的 GenerateNodeFunction](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.shadergraph/Editor/Data/Nodes/UV/FlipbookNode.cs#L134-L155)。节点把帧内 UV 平移到格子再缩放到图集，未限制到格内 texel 中心，也没有 padding 参数。它只算 UV，不负责过滤或生成 mip。
- **作者原图**：[Mikola Lysenko，0 FPS，2013-07-09，Mipmapping Texture Atlases](https://0fps.net/2013/07/09/texture-atlases-wrapping-and-mip-mapping/#attachment_1333) 对照了同一体素图集的[逐格 mip 2](https://0fps.net/wp-content/uploads/2013/07/2.png)与[整图 mip 2](https://0fps.net/wp-content/uploads/2013/07/2_notile.png)。原图中，下半部浅蓝格与上方杂色格交接处在整图版带入的一侧颜色更明显。作者逐格生成再打包以隔离边界；该案例的格内纹理要重复，所以用了周期边界。Flipbook 通常在单帧内钳边或复制边缘，不直接照搬周期预滤波。作者另演示了 `fract` 导数引起的错误 LOD，那组灰缝不是本条对照的证据。
- **官方资源约束**：[Unity 6000.0 Texture2DArray](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/texture2darray) 使用同尺寸、同格式的独立层，着色器指定层索引；普通二维过滤不会取隔壁层。生成数组时也要从干净原帧填入各层和 mip；从已经串色的图集 mip 拷贝不会修复颜色。先检查目标设备的 `SystemInfo.supports2DArrayTextures`，再核对实际压缩格式与构建产物；Unity 的 API 不支持以 Crunch 格式创建数组。

## 两个算例：为什么缩半个像素和补 4 px 都有边界

- 两个 64 px 的纯色格水平相邻，局部 UV 为 1 恰落在格界；在 mip 0，边界位于像素中心 63.5 和 64.5 的正中，线性采样会各取一半。缩到本格最后一个 texel 中心能通过这组 mip 0 对照；对粗 mip，格内半个 texel 对应的原图距离更大。若原图 UV 按半个 texel 固定缩入，不能保证其他 mip 的格界隔离。
- 每边补 4 px 的 64 × 64 帧，基础层单格由 4096 变成 5184 texel，约多 **26.6%**。相同的 4 px 在理想 2 倍下采样下折成 mip 2 的 1 texel、mip 3 的半个 texel；粗 mip 还有三线性或各向异性过滤的足迹。补边要从镜头实际使用的粗 mip 和足迹反推，再看对齐、压缩块与图集占用。这里只算基础层格面积，不是 GPU 分配或带宽测量。

## 怎么判断该改哪里

1. 同一枚特效冻结帧索引并关闭帧混合，隔壁帧设为对比色。先用无压缩数据、强制 mip 0：点采样正确、线性采样只在格边带入标记色时，试复制当前帧边缘做隔离带，或限制格内 UV；整张图的 Clamp 只管外框。若颜色来自本格 Alpha=0 区域，沿[透明区 RGB 扩色](texture-alpha-import.md)的路线查，顺便核对透明混合约定。
2. 再固定采样到出错的粗 mip，先看内容区点采样的颜色。若已混入邻帧，逐帧生成 mip 后以相同格位合成各层；每层边界都要隔离，格子无法继续表示时限制最粗使用层级。之后恢复三线性、动态距离和目标压缩，确保不是以远距闪烁换干净边缘。
3. 若每帧同尺寸、要支持更粗的 mip 且平台可用，比较独立数组层和补边图集：记录实际 GPU 纹理字节、镜头中最粗的可用层、构建和加载方式。需要在两个动画帧间插值时，按相同质量分别计两次层采样与混合，不能把它算成一次无成本过滤。

**状态**：已核对 Unity 源码、数组文档和公开体素图集原图。两个算例是计算；尚未本地复现 Flipbook、测目标设备或比较实际资源占用。
