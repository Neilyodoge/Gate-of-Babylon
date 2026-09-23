# 小嗯的 Unity 渲染设置

> 适用 Unity 2022.3。最终以目标设备上的 Frame Debugger / Profiler 结果为准。

## 1. 渲染合批

### 1.1 选择与限制

| 方式 | 适用场景 | 关键条件 | 主要限制 |
|---|---|---|---|
| Static Batching | 固定不动的物体；Mesh 可不同 | 材质、Pass、渲染状态和顶点布局兼容 | 子物体不能单独改 Transform；增加 Mesh 内存；单个生成批次最多 64k 顶点 |
| GPU Instancing | 大量重复的同 Mesh、同材质物体 | 同 Mesh、同材质、同 Pass；Shader、材质和平台支持实例化 | 普通 `SkinnedMeshRenderer` 不走自动实例化；没有固定的“划算数量” |
| SRP Batcher | URP/HDRP；材质或 Mesh 不同，但 Shader 变体相同 | 连续使用同一 Shader 变体；Shader 常量布局兼容 | 主要降低 CPU 提交成本，不等于减少 Draw Call；Built-in 不支持 |

> **重点：Static Batching 可与 SRP Batcher 共用；但与 GPU Instancing 不能同时作用于同一个 Renderer。**

| 组合 | 结论 | 原因 |
|---|---|---|
| Static Batching + SRP Batcher | **可共用** | Static Batching 将静态几何预转换并放入共享批次；SRP Batcher 缓存 Shader/材质常量，降低 CPU 提交成本。两者优化环节不同 |
| Static Batching + GPU Instancing | **同一 Renderer 互斥** | GPU Instancing 需要保留多个“同 Mesh、同材质”的独立实例，并提交各自的 Transform/参数；静态合批后会改走静态批次路径，不再作为原 Mesh 的独立实例绘制 |

> 项目中可以同时开启 Static Batching 和 GPU Instancing，让不同 Renderer 各走合适的路径；同一 Renderer 同时满足时，Unity 优先使用 Static Batching。

### 1.2 常见坑

| 情况 | 结论 |
|---|---|
| Static Batching | 构建时要开启该功能并勾选 `Batching Static`；运行时 `StaticBatchingUtility.Combine` 要求 Mesh 开启 Read/Write |
| Transform | 静态批次根节点可整体移动，子物体不能单独移动 |
| MPB | 会破坏 SRP Batcher；若只设置 Shader 已声明的实例化属性，GPU Instancing 仍可能生效 |
| `Renderer.material` | 会生成独立材质，可能破坏 GPU Instancing；修改 `sharedMaterial` 会影响所有共享对象 |
| 烘焙光照 | 同一实例批次需使用同一张 Lightmap，可使用图集中的不同区域 |
| URP/HDRP | 普通 Renderer 同时兼容时，通常优先 SRP Batcher，而非自动 GPU Instancing |
| `RenderMeshInstanced` | 仅该 API 单次最多 1023 个实例；默认双矩阵时通常最多 511 个，实际还受平台和实例数据影响；按整批 Bounds 剔除 |

## 2. 项目设置

> 入口：`Edit > Project Settings`。以下按左侧菜单整理；具体选项会随 Build Target 和已安装包变化。

### 2.1 [Graphics](https://docs.unity3d.com/2022.3/Documentation/Manual/class-GraphicsSettings.html)

| 页面项 | 主要作用 | 注意 |
|---|---|---|
| Default Render Pipeline | 指定项目默认的 URP/HDRP Asset | `Quality` 某档绑定了 Asset 时，会覆盖这里的默认值 |
| Camera Settings | 设置透明物体的排序模式和排序轴 | 2D、等距视角常用；普通 3D 通常保持默认 |
| Tier Settings | 设置 Built-in 各硬件等级的渲染能力 | 主要用于 Built-in，URP/HDRP 不要只看这里 |
| Shader Settings / Stripping | 强制包含 Shader、预热 ShaderVariantCollection，或裁剪 Built-in 变体 | Always Included 会带入该 Shader 的全部变体；过多会增加包体、启动时间和内存 |
| [URP Global Settings](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/urp-global-settings.html) | Rendering Layers 名称、Shader Variant 日志与剔除 | URP 安装后显示在 `Graphics` 下；开启 Strip Debug 后，Player 中不能使用 Rendering Debugger |

> **Render Pipeline Asset 优先级：**当前 Quality 档绑定的 Asset → Graphics 的 Default Render Pipeline → 两处都为空时使用 Built-in。

### 2.2 [Quality](https://docs.unity3d.com/2022.3/Documentation/Manual/class-QualitySettings.html)

| 页面项 | 主要作用 | 注意 |
|---|---|---|
| Quality Levels | 设置各平台默认质量档 | 先确认设备实际进入了哪一档 |
| Render Pipeline Asset | 为每个质量档绑定不同的 URP/HDRP Asset | **优先级高于 Graphics 中的默认 Asset** |
| Textures | Mipmap Limit、分组、各向异性、[Texture Streaming](https://docs.unity3d.com/2022.3/Documentation/Manual/TextureStreaming.html) | Limit 管最高细节；Streaming 管当前实际驻留层级 |
| LOD | LOD Bias、Maximum LOD Level | Bias 越高越晚降级；Maximum LOD 还可能影响构建时的 LOD 裁剪 |
| VSync | 与屏幕刷新率同步 | 会影响帧率上限和输入延迟 |
| Shadows / Anti Aliasing | Built-in 的阴影和抗锯齿 | URP 下主要看该档绑定的 URP Asset |
| Async Upload | 控制纹理、Mesh 上传时间片和缓冲区 | 缓冲过小易卡顿，过大会增加内存 |

#### Mipmap Limit 速查

| 设置 | 效果 |
|---|---|
| Limit 0 / 1 / 2 / 3 | 宽、高依次为原图的 1、1/2、1/4、1/8 |
| Use Global | 跟随全局限制 |
| Offset -1 / +1 | 比全局清晰一级 / 模糊一级 |
| Override | 直接覆盖该组的限制 |

> 仅影响有 Mipmap 且支持该功能的纹理；不能突破导入 Max Size 或已有 Mip。Texture Streaming 还需在纹理导入设置中开启 Mip Streaming。Unity 2022.3 对 Texture2DArray 的说明有冲突，按目标平台实测。

### 2.3 [Player](https://docs.unity3d.com/2022.3/Documentation/Manual/class-PlayerSettings.html)

| 页面项 | 主要作用 | 注意 |
|---|---|---|
| Color Space | 选择 Gamma 或 Linear 工作流 | Linear 通常更符合物理光照，切换后需重查材质和 UI 颜色 |
| Graphics APIs | 设置图形 API 及优先顺序 | 关闭 Auto 后，第一个受支持的 API 会优先使用 |
| Multithreaded Rendering / Graphics Jobs | 将部分渲染提交移出主线程 | 支持情况因平台和 API 而异，必须实机验证 |
| Static / Dynamic Batching | 控制内置合批开关 | URP 的 Dynamic Batching 在 URP Asset；GPU Instancing 没有 Player 全局开关 |
| GPU Skinning | 将蒙皮计算交给 GPU | 仅在对应平台支持时出现，需比较 CPU、GPU 和内存 |
| Lightmap / HDR Cubemap Encoding | 控制烘焙数据编码质量 | 按平台生效，修改后重新构建验证 |
| Mipmap Stripping / Virtual Texturing | 剔除不用的 Mip，或启用虚拟纹理 | 仅部分平台显示；错误配置可能造成远近景模糊或纹理缺级 |
| Resolution and Presentation | 分辨率、方向、全屏和显示缓冲 | 移动端与桌面端字段不同 |

### 2.4 容易找错的位置

| 设置 | 实际位置 | 主要内容 |
|---|---|---|
| [URP Asset](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/universalrp-asset.html) | Project 资源的 Inspector | Render Scale、HDR、MSAA、灯光、阴影、后处理；同时开启时，兼容对象优先走 SRP Batcher，Dynamic Batching 只补剩余对象 |
| [Universal Renderer Data](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/urp-universal-renderer.html) | Project 资源的 Inspector | Rendering Path、Depth Priming、Intermediate Texture、Renderer Features |
| Lighting | `Window > Rendering > Lighting` | 环境光、GI、Lightmap、反射探针 |
| Camera / Volume | 场景组件的 Inspector | 相机覆盖项和后处理参数 |

### 2.5 实机检查

1. 确认设备实际使用的质量档位、渲染管线、URP Asset 和图形 API。
2. 用 Frame Debugger 检查绘制路径和断批原因。
3. 用 Profiler 比较 CPU、GPU 和内存；固定场景，一次只改一项。
4. 用接近正式发布的构建复测。
