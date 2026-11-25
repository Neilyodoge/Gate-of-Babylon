# TA-141 · 把后处理移到异步计算反而更慢

**制作决定。** 后处理换到异步队列时，以同画质整帧的 GPU 完成时间为判据。先检查输入何时就绪、输出何时被第一处消费，再看重叠期间两个 Pass 是否同时变长。只看到两条队列轨道有重叠，无法判断这次迁移缩短了关键路径。

## 公开证据

- **Khronos Vulkan Samples 的设备对照。** [Async compute 样例](https://docs.vulkan.org/samples/latest/samples/performance/async_compute/README.html) 使用 Mali-G77，[关闭 async 原图](https://raw.githubusercontent.com/KhronosGroup/Vulkan-Samples/main/samples/performance/async_compute/images/noasync.jpg) 显示 22.9 ms、Fragment Cycles 602.8 M/s；[打开 async 原图](https://raw.githubusercontent.com/KhronosGroup/Vulkan-Samples/main/samples/performance/async_compute/images/async.jpg) 显示 21.8 ms、785.5 M/s，图中双缓冲 HDR 未勾选。作者说关闭时即便已有部分顶点工作重叠，后处理屏障仍让片元队列缺工作；开启后片元周期更接近 GPU 活跃周期，但顶点计算和片元仍争用同一着色核心。周期每秒读数用于同场景诊断，不能当成两个 Pass 各自的耗时。
- **AMD 的资源条件。** [Leveraging Asynchronous Queues for Concurrent Execution](https://gpuopen.com/learn/concurrent-execution-asynchronous-queues/) 公开正文解释其 G-Buffer 和深度窗口的利用率不同。同一 CU 的工作可争用寄存器、LDS，队列也可能争共享缓存或带宽；拆命令列表与栅栏有成本。先找瓶颈互补、依赖不紧挨的工作，不能假定 GPU 有两份资源。AMD 的讨论是特定架构及工作负载的证据。
- **同步的具体位置。** [Unity Graphics 6000.0 的 NativePassCompiler](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/Compiler/NativePassCompiler.cs#L559-L624) 根据资源生产者和读者查是否跨队列，给写入侧标记 fence 并在相关输入侧等待。[D3D12 多引擎同步](https://learn.microsoft.com/en-us/windows/win32/direct3d12/user-mode-heap-synchronization) 的公开示例也是画出输入、计算等待、最后合成等待计算。只打开异步开关无法消除这些资源依赖；实际硬件并行能力和图编译结果需以所测后端为准。

## 关键路径算例与验收

用四个同帧阶段做 CPU 算例：输入生产 P=3 ms，无数据依赖的图形工作 G=4 ms，后处理 C=3 ms，等待两边完成的合成 F=2 ms。下表的同步代价是未被其他工作隐藏的额外时间，数值不是设备测量。

| 调度 | 算式 | 整帧关键路径 |
| --- | --- | ---: |
| 串行 | `3+4+3+2` | 12.0 ms |
| 理想重叠 | `3+max(4,3)+2` | 9.0 ms |
| 两边重叠却都被拖长，G→6、C→6.8 | `3+max(6,6.8)+0.5+2` | 12.3 ms |
| 依赖让两边无法重叠，另加等待 | `3+4+3+0.5+2` | 12.5 ms |

1. 固定画质、输入、分辨率和场景重放，用同一个 Compute Pass 比较留在图形队列与调到异步队列。标出生产输入、第一处消费、两条队列的执行区间和被 fence 卡住的位置，再比整帧 GPU、单 Pass 时长与可用的带宽、占用或缓存指标。若异步区间同时出现但两边都变慢，试换成资源瓶颈互补的重叠对象；若几乎没重叠，优先找能提前生产或推迟消费的位置。
2. 至少比较轻负载、重透明或高带宽镜头，再在目标机型重复。拆屏障或用跨帧双缓冲要单独验证画面相等、工作量是否一致、峰值内存与输入延迟；颜色结果如果允许晚一帧，跨帧方案也要明确验响应。GPU 本就没有足够空隙时，保留串行往往更简单，按实测选择。

**验证状态。** 已看 Khronos 在 Mali-G77 上的两张原图，核对 AMD、D3D12 和 Unity 公开资料，并复算四条时间线；未在本地抓队列或对比真机帧时。
