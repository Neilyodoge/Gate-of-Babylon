# UV 滚动长运行跳格：时钟、偏移和回卷各自验

**一句话结论**：如果小时间下正常、长运行后停几帧再跳，先看 GPU 实际收到的时间和乘完速度后的偏移是否已经量化。可重复平铺的纹理，优先在高精度域给每层算好有界相位再传给 Shader；全局时间直接回卷只有在所有受影响的层都刚好完成整数周期时才无缝。

## 来源和数值对照

- **引擎文档**：[Epic《Constant Material Expressions》· Time](https://dev.epicgames.com/documentation/en-us/unreal-engine/constant-material-expressions-in-unreal-engine#time)给出 `Period` 回卷，说明移动材质的该计算在 CPU 侧用全精度，GPU 侧半精度的路径在超过一分钟的周期可能出问题。这是一项具体路径的提醒，不能据此假设所有材质都在同处、同精度折返。[Panner](https://dev.epicgames.com/documentation/en-us/unreal-engine/coordinates-material-expressions-in-unreal-engine#panner)可以显式传时间输入，并分别设置 U、V 方向速度；可以先用固定输入隔离时钟。[Unity 6000.0 · Time.timeAsDouble](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/time/timeasdouble)提供双精度帧时间。[Microsoft · HLSL Minimum Precision](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/using-hlsl-minimum-precision#declare-variables-with-minimum-precision-data-types)说明 `min16float` 是最低要求，驱动可以用 32 位执行，因此编辑器顺滑并不能覆盖实际采用低精度的设备。
- **已执行的数值对照**：每帧输入 `t0 + n/60` 秒，纹理速度为 1/8 周/秒。用 V8 的 `Math.f16round` / `Math.fround` 显式舍入，再观察相位。binary16 从 64 s 起相邻可表示值间距为 0.0625 s；binary32 从 262144 s（约 72.8 h）起间距为 0.03125 s。实际记录第 0–3 帧相位如下。这说明会停帧，不代表画面必然到这个时间才开始抖。

| 算法与起始时间 | 第 0 帧 | 第 1 帧 | 第 2 帧 | 第 3 帧 |
| --- | ---: | ---: | ---: | ---: |
| 先把 64 s 附近的 t 和偏移舍入到 binary16，再取小数 | 0 | 0 | 0.0078125 | 0.0078125 |
| 先把 262144 s 附近的 t 和偏移舍入到 binary32，再取小数 | 0 | 0.00390625 | 0.00390625 | 0.0078125 |
| 262144 s 附近，先用 CPU 双精度算并取小数，再转 binary32 | 0 | 约 0.002083 | 约 0.004167 | 约 0.006250 |

复算：令 `phase(x) = x - floor(x)`，前两行用 `phase(round(round(t0 + n/60) / 8))`，第三行用 `fround(phase((t0 + n/60) / 8))`。另一个隔离偏移的压力算例：时间保留双精度，65536 s 后以 20 周/秒滚动，把长偏移先舍入到 binary32 再取小数，前两帧的相位从应有的 1/3、2/3 变为 0.375、0.625。`frac` 无法恢复已丢掉的小数位；若直接把小 UV 加到大偏移上，小 UV 也可能在加法中丢失。

## 什么时候可以共用回卷

- 用 Repeat 采样且图案一周为 1 UV 时，周期 `P` 秒无缝回卷需要该层每个活动轴的 `速度 × P` 是整数。速度为 1/8 与 7/100 周/秒的两层，共用 60 s 时，回卷两侧相位分别从 0.5、0.2 跳回 0；200 s 则分别走完整 25、14 周。这个条件不适用于 Clamp、非平铺内容、随机序列或周期不一致的其他动画。
- 常速可用双精度 `phase(speed * time)` 分别求每层、每轴；速度随状态变化时，按 `phase(previous + speed * deltaTime)` 累积以保住切速瞬间的相位。负速度的相位用 `x - floor(x)` 保持在 [0,1)。Shader 将有界偏移加到基础 UV 后交给 Repeat 采样器；不要习惯性地对每像素的最终 UV 做 `frac`，否则跨 UV 折返的隐式导数可能让 mip 选粗。若基础 UV 本身巨大，仍要单独处理空间坐标精度。

## 最小验证

固定镜头、帧步长、重复纹理和 mip 策略，用注入时间跳到 0、64、262144 s 附近与回卷前后，分别记录时间、偏移相位和画面，避免等真实挂机。逐帧比较原 Shader、只升精度和 CPU 有界相位三组；再验不同速度、负轴、切速和实际目标图形 API。记录相位停滞帧、回卷前后的圆周差以及 GPU 与 CPU 的开销；有大量材质时先考虑共享更新、保留材质或实例批处理效率，避免按 Renderer 克隆材质。

**状态**：已读引擎文档并运行 CPU 数值对照；没有在 Unity、UE 或真机验证编译精度、可见抖动和帧时。
