# TA-138 · Shader 已预热，首次特效仍卡一帧

**制作决定。** Shader 变体已被打进 Player，也可能在首个真实 Draw 才创建所需的图形管线状态（PSO）。先在同一材质和真实渲染状态下区分：变体缺失、PSO 没覆盖或没按时准备完成、贴图与网格首次上传。只有第一条适合先追构建保留规则。

## 公开证据

- [Unity 6000.0 · ShaderVariantCollection.WarmUp](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShaderVariantCollection.WarmUp.html) 提醒：DX12、Vulkan、Metal 上，实际顶点布局或渲染目标设置不同于预热时，驱动可能仍要处理。Microsoft 的 [D3D12_GRAPHICS_PIPELINE_STATE_DESC](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_graphics_pipeline_state_desc) 确实同时含着色器、输入布局、RT／深度格式、采样参数和固定状态。只对上关键字，无法证明整份图形状态一致。
- [Unity 6000.0 · Shader prewarming](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-prewarm.html) 提供 `Shader.CreateGPUProgram` 与 `CreateGraphicsGraphicsPipelineImpl` 两个取证入口，分别看变体准备和 PSO 创建。实验性 [GraphicsStateCollection](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Experimental.Rendering.GraphicsStateCollection.html) 可记录运行时遇到的变体和对应的多个图形状态，可逐批预热；集合有图形 API、平台和质量档相关元数据。覆盖要来自会实际发生的路径，预热作业也应在目标效果调用前完成。实验性接口与 Profiler 标记均以所用版本和后端为准。
- [Epic UE 5.5 · PSO Precaching](https://dev.epicgames.com/documentation/en-us/unreal-engine/pso-precaching-for-unreal-engine?application_version=5.5) 的验证用 `Missed` 表示所需状态未预缓存、`Too Late` 表示已排队但首次 Draw 前没完成。公开日志用 SkyPass、材质、Vertex Factory 和 Shader Hash 定位漏项；同文的 Insights 正文还描述一处 Translucency 的 117 ms 案例及数个 5–10 ms 的漏预热尖峰，也说明另一些大尖峰无关 PSO。这里引用正文和日志，未读图或运行样例，时长不能外推到 Unity。
- [Unity · 贴图与网格数据加载](https://docs.unity3d.com/6000.0/Documentation/Manual/LoadingTextureandMeshData.html) 说明同步上传可在一帧内加载并上传数据，异步则能把二进制数据分摊到后续帧。预热 PSO 不保证真实贴图已上传；只等资源加载句柄也不能证明所有 GPU 数据或目标 PSO 都已就绪。

## 组合算例与验收

保持同一个 Shader 变体；顶点属性语义相同，但布局 A 是单顶点流、B 拆成两流，渲染目标可能是 1× 或 4× MSAA。CPU 枚举至多四种组合；如果实用路径只有 A/1×、B/1×、B/4× 三种，只用 A/1× 预热即使覆盖了这个变体，仍少两个实际状态。这是状态集合算例，不代表 Unity 的实测数量或缓存命中。

1. 用能复现尖峰的独立 Player，固定质量、图形 API、特效、纹理导入和镜头，从相近的冷启动状态记录首次和第 2 次出现。确认实际使用的 Pass 和变体，检查第一帧有没有变体缺失、PSO 创建、CPU 文件 I/O、上传或其它等待。已有驱动缓存会改变冷启动结果，记录测试缓存条件，别把第二次运行当成同一次的冷对照。若效果错误或有缺关键字日志，先走 [TA-05 的构建排查](shader-variant-runtime-stripping.md)。
2. 分别比较只准备好真实网格与贴图、只准备真实图形状态、两者同时准备。需要隔离第二项时，用捕获状态预热；若用一次真实 Draw 代替，优先用同状态的占位资源，并在抓帧确认布局、附件格式、深度、采样数一致，否则会把真实资源上传也提前。保持时间线可比，核对首帧尖峰移到了哪里、游戏首帧和加载页各新增多少时间，再决定窄范围预热还是调整资源上传。

**验证状态。** 已核对 Unity 6000.0、Microsoft D3D12 与 Epic UE 5.5 公开正文、日志和 API，并复算组合；未在 Unity 或 Unreal 抓冷启动，未在目标设备计时。
