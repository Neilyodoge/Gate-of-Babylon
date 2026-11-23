# VAT 抖动与破面：分开查读错地址、读错值和剔除

**一句话结论**：冻结到一个有问题的整数帧。还在绘制但个别顶点飞线或破面，先验证读了哪个 texel；读对地址后再查存进去的坐标精度。整个物体或阴影在镜头移动时消失，先看绘制和包围盒；SideFX VAT 3.0 的一些路径会从 Bounds 取解码数据，不能直接靠放大 Bounds Scale 试错。

## 公开证据

- **UV 修复实例**：[SideFX Labs 官方 FAQ「Why is my VAT mesh full of holes in UE5?」](https://www.sidefx.com/forum/topic/81422/?page=1#post-350761)说明 UE5 的网格 UV 实现变化会让动态重拓扑 Fluid VAT 破面；该版建议先试 UE4 Compatible UVs 并重启，不行再关掉兼容选项、试 Use Full Precision UVs。[教程下 UE5.1 的使用者反馈](https://www.sidefx.com/tutorials/vertex-animation-textures-for-unreal/#reply-12461)明确说打开全精度 UV 后能工作；属公开修复反馈，未附同帧顶点误差和完整控制条件。SideFX [Rigid 模式的 Pivot Accuracy](https://www.sidefx.com/docs/houdini/nodes/out/labs--vertex_animation_textures-3.0.html#pivot-accuracy)还有另一种 UV 用法：枢轴直接存在网格 UV。Very High 用两组 16 位 UV 编码并用更多着色指令解码，Maximum 使用 32 位 UV；在 UE 若不保留全精度，选 Maximum 也不会得到预期精度。Fluid 的地址错误和 Rigid 的枢轴误差不应混为一谈。
- **位置值和编码范围**：[SideFX Texture Format 与 Split Positions](https://www.sidefx.com/docs/houdini/nodes/out/labs--vertex_animation_textures-3.0.html#texture-format)提示大范围运动会放大量化误差，可试 HDR 或拆成较小的局部段；没有相应 HDR 数据纹理支持时，两个 8 位位置图是候选，要额外存图并读取两份数据。[meta_t 的位置图对照](https://realtimevfx.com/t/vertex-animation-texture-compression/9003)里，外部编码的 BC6H 蓝通道比作者标注的 Unreal BC6H 图多明显横块；缺少相同编码链说明、未压缩参照和同一姿势误差，不能凭压缩选项标签推断精度或显存占用。
- **Bounds 的双重用途**：[SideFX VAT 3.0 的 Legacy JSON 段](https://www.sidefx.com/docs/houdini/nodes/out/labs--vertex_animation_textures-3.0.html#allow-exporting-real-time-data-json-file-legacy)说明默认流程把参数嵌入静态网格 Bounds，供 Shader 解码；新的导出 Bounds 本来覆盖完整可见动画。UE 中改 Bounds Scale 或某些实例变换可能破坏嵌入值，常规 Actor/Component/Object Scale 与此不同。必须独立改剔除范围时，先试该流程支持的显式参数，并验解码和可见性两项。

## 两个独立的数值对照

- **只改地址精度**：设 4096 列位置纹理，列从 0 开始，UV 存该列中心 `(i + 0.5) / 4096`，用 `floor(u * 4096)` 模拟点取样。把 UV 转成 binary16 后，列 3073 与 3074 的 UV 都变成 0.75048828125，读到同一列 3074。提高 texel 内的数据精度不会改变这个结果。算例未模拟特定 GPU 的取样舍入或实际 VAT 查表排布，不把 4096 当平台硬阈值。
- **只改数值存放空间**：以厘米计，binary16 在 8192 cm 往上的相邻数差 8 cm。把原点为 8192 cm、每帧移动 1 cm 的绝对位置直接存进去，前五帧的解码偏移都是 0，第六帧跳到 8 cm；存局部位移 0 至 12 cm、再用 32 位浮点加原点，这组输入能逐厘米还原。另一个独立场景是 8 位 UNorm 线性编码：把同样 256 级铺到 2 m，步长约 0.78 cm；铺到 20 m 则约 7.84 cm。两组是 Python binary16 转换和步长计算，不代表 SideFX 默认布局、误差分布或 GPU 帧时。

## 验证顺序

1. 锁动画帧、LOD、相机和实例路径。确认是否仍有 Draw；若有，临时输出顶点 ID、数据列或位置误差，看飞线来自错行错列还是正确 texel 的内容异常。先查资源实际尺寸、导出索引和 UV 精度；等价条件下分别只切全精度 UV、只切位置图格式或编码范围。保留数据线性，按导出器约定使用取样与 mip，避免纹理导入缩放脏了坐标。
2. 若整个 Draw 消失，固定动画帧扫过视锥边缘，检查动画所有帧是否落入实际剔除的包围范围，并分别查相机与阴影通道。保持解码参数不变再试保守盒；不要动依赖 Bounds 的解码链来证明是剔除。冻结帧的资产在近原点和远原点复测时，分别确认烘焙的是世界绝对值、物体局部值还是偏移。
3. 用高精度或原始网格作为基准，量代表顶点和轮廓的位移误差。比较全精度 UV 增加的网格存储，以及 HDR 或第二位置图增加的纹理字节和顶点采样；目标设备再量加载、纹理分配和绘制帧时。

**状态**：已读官方 FAQ、修复反馈及公开位置图，跑了 CPU 数值对照；未在 Houdini、UE 或目标设备做同资产复现和计时。
