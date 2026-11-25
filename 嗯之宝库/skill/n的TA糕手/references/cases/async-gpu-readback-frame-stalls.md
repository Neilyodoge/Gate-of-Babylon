# TA-122 · GPU 数据回读引发周期性卡顿

**制作决定。** 回读帧有 CPU 尖峰，先用同样大小的数据对照同步读取与提交后稍晚消费的异步读取，分开量等待、GPU 拷贝和 CPU 处理。即使只读一个像素也可能排在前面的 GPU 工作后面；整屏异步回读又可能被数据量和排队深度限制。CPU 若只需要统计结果，优先在 GPU 上归约后只读统计；确需整帧像素时，再选频率、尺寸与队列上限。

## 公开证据

- [Unity 6000.0 · Texture2D.ReadPixels](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture2D.ReadPixels.html) 说明同步读取会等此前的 GPU 工作。[AsyncGPUReadback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.html) 以几帧延迟换取非阻塞读取；若每次提交后马上调 [WaitAllRequests](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.WaitAllRequests.html)，文档明确会同时卡住 CPU 和 GPU。较低层的 [D3D12 Readback Heap](https://learn.microsoft.com/en-us/windows/win32/direct3d12/readback-data-using-heaps) 示例也把「GPU 已写完」和「CPU 能 Map」分开：Map 本身不等 GPU，用 fence 判定。
- Alpenglow 的公开 [同步实现](https://dev.to/alpenglow/unity-fast-pixel-reading-cbc) 和 [异步对照](https://dev.to/alpenglow/unity-fast-pixel-reading-part-2-asyncgpureadback-4kgn)（2023）都用计算着色器读取首像素，输出缓冲仅一个 `float4`（16 字节）。旧版调用 `ComputeBuffer.GetData`，新版每帧提交 `AsyncGPUReadback.Request` 并稍后取结果。Part 2 报告空场景基线 57.53 FPS、同步 44.36 FPS、异步 58.01 FPS；这是一次公开平均值，没有给出逐帧等待记录或设备对照，不能用比基线高 0.48 FPS 断言读回会提速，也不能套到整屏截图。
- [Unity 请求说明](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.Request.html) 要保留临时 RT 直到请求完成；[`RequestIntoNativeArray`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.RequestIntoNativeArray.html) 也要求在完成前保留目标内存。用普通 [`GetData` 请求结果](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadbackRequest.html) 时，可读窗口为成功完成后的那一帧；要延迟消费，应在有效期内转移到自有存储。再检查目标平台和回读格式是否支持，以及 `hasError`。

## 算例与最小对照

- **数据预算。** 1920×1080 RGBA8 的逻辑载荷为 7.91 MiB/次，每秒 60 次约 474.61 MiB/s，四个同尺寸 CPU 槽位约 31.64 MiB。改读 256×256 RGBA8 是 15 MiB/s；只要区域或统计值时，再比较区域读取和 GPU 归约。这里不含缓冲对齐、运行时暂存、格式转换、降采样和 CPU 复制的成本。
- **排队预算。** 假设每帧发一次，从第 0 帧开始，固定在发出后的第 3 帧完成，并先回收再提交。两个槽位在 12 帧内漏发 4 次（第 2、5、8、11 帧），三个槽位在这个固定延迟模型下才全发出。实际延迟会变；按可接受的结果年龄与内存调槽位，槽满时降低频率、跳过本次或复用上一份已完成的结果。尚未完成的请求及 CPU 仍在读取的缓冲不能覆盖。需要当前帧结果的逻辑，先看是否能留在 GPU 上；异步结果带源帧号，不能当成本帧。
- **抓帧顺序。** 同镜头、同 API、同帧率设置下，先只比较同步与同字节数异步，把编码、分配和磁盘写入拿掉；再比较缩小后的载荷，最后单独加回 CPU 处理。记录提交/完成帧号、在途数、跳帧数、CPU 等待及回调耗时，并在分析器可见时量 GPU 拷贝和对应 Pass；看帧时尖峰和高分位，不只看平均 FPS。若异步提交变平但完成帧仍尖，检查回调中的数组复制、编码和累计结果，别把它们算成等待 GPU。

**验证状态。** 已核对公开实现、作者平均帧率和 Unity／D3D12 API，并跑过载荷与槽位算例。未在本地引擎抓帧，未做整屏、队列深度或真机 A/B。
