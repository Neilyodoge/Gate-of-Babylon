# TA-43 · 细法线缩进一个像素后，把丢掉的方向变化补进粗糙度 mip

**制作决定**：低粗糙度的平面在远处闪，换成平法线后若明显稳定，先用同一套 UV 和构建设置试粗糙度预过滤：近景保留原粗糙度，在更粗的 mip 只补该区域丢掉的法线变化。曲面换成平法线仍闪时再单试几何抗锯齿。诊断高光源与 Bloom 的流程见[远景高光案例](specular-glint-stability-and-bloom.md)；这张卡补贴图构建、通道选择及其边界。

## 原始资料与区别

- **Filament 的离线实现。** [Roughness Prefilter 说明](https://github.com/google/filament/tree/main/tools/roughness-prefilter#readme)接受常量粗糙度或原粗糙度图。[solveVMF](https://github.com/google/filament/blob/main/tools/roughness-prefilter/src/main.cpp#L193-L243)先把取到的每个原始法线解码、归一化，再求平均；用平均向量的**原始长度**估计方向分散程度，并给粗糙度增加有限的补偿。越分散越短；拿归一化后的 mip 法线长度做这个计算会丢掉信号。[构建分支](https://github.com/google/filament/blob/main/tools/roughness-prefilter/src/main.cpp#L386-L409)保留 mip 0 的输入粗糙度，后续层才预过滤。本工具是一个可核实现，它的参数不适用于所有着色器。
- **Unity 6000.0 的几何与纹理过滤。** [CommonMaterial.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl#L209-L266)对几何输入取世界空间几何法线的屏幕导数，纹理分支则读取平均法线长度。固定朝向平面的几何法线导数可为零，细纹贴图仍可产生闪烁；若只开以几何法线为输入的滤波，不能补回已经平均掉的贴图方向。
- **Epic 接口。** [UE 5.5 Texture](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/Texture?application_version=5.5#unreal.Texture.composite_texture_mode)和[CompositeTextureMode](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/CompositeTextureMode?application_version=5.5)提供从法线合成粗糙度到 R/G/B/A 的独立模式。若打包图的 G 才是粗糙度，就选写 G 的模式并核对其他通道。文档另有用于归一化 mip 法线的选项；它和写入粗糙度是两件事。UE 并非证实使用 Filament 同一公式。
- **作者图及方向性边界。** [Olano 与 Baker，LEAN Mapping（I3D 2010）](https://userpages.cs.umbc.edu/olano/papers/lean/)的[近景海面](https://userpages.cs.umbc.edu/olano/papers/lean/ocean-near.png)可见独立波纹与较亮的小反光，[远景海面](https://userpages.cs.umbc.edu/olano/papers/lean/ocean-far.png)波纹不再清晰，但仍可见一片较宽的细碎反光。这是同方法的不同视距原图，不能量化运动闪烁或当作开关 A/B。作者用可滤波的方向分布保留沟槽造成的各向异性；只用平均法线长度得到的单个粗糙度值，无法区分沿 X 与沿 Y 分散程度相同的纹样。拉丝金属或规则细沟槽应另验反光方向。

## 算例与复核

一个 mip 区域内，一半单位法线朝左倾 30°，一半朝右倾 30°。它和全平区域的**归一化平均方向**相同，但未归一化平均长度分别是 cos(30°)≈0.866 和 1。代入上述 Filament 实现，同取输入粗糙度 0.2，以代码中的上限计算，两块约为 0.283 与 0.2。这说明统一提高材质粗糙度会把平整区域一起磨钝。数字只演示源码计算，不代表推荐粗糙度。

1. 选平面细纹、同材质曲面和一块没有细纹的光滑参照，固定曝光、光源和相机路径。用平法线临时替换细纹，确认闪点是否随它消失，再分别比较未补偿、只补粗糙度 mip、只开几何过滤。先在 Bloom 与时域处理关闭时定位高光源，再恢复项目配置做匀速运动对照。
2. 抓同一块表面的法线与粗糙度 mip、近景高光形状、远景面积与亮度随帧变化。近景先确认 mip 0 没被改钝；粗 mip 若不见效果，检查导入是否重建了 mip、平台覆盖是否改变两张图的有效分辨率、采样 UV 和层级是否对应。运行时叠加的第二层法线不在烘焙输入里，要另做对照。
3. 若预过滤结果仍占原打包通道，且尺寸、格式和原有采样不变，可保持原运行时采样数量；如果必须另占纹理或改压缩格式，另测内存和 GPU 时间。以同画质和真机计时决定是否值得。

**状态**：已核对 Filament 与 Unity 源码、Epic API、LEAN 近远景原图和算例；没有本地同镜头的静态／动态 A/B，也没有真机成本。
