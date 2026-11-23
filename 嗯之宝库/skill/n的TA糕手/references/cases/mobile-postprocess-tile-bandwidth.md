# 移动端后处理：哪些中间颜色该留在片上

**一句话结论**：色调映射、调色 LUT、淡入淡出若只依赖当前像素的场景色，可试同一硬件 Render Pass 内读取附件，丢弃用完的中间颜色。泛光、模糊与景深需要邻域数据，不能把它们原样塞进只读当前像素的路径。合并以后分别测外部读写与 GPU 时间，两项不保证同向改善。

## 两个公开对照：一个看效果附加耗时，一个看片上合并

- [Meta · Graphics Showcase，Implementation Details / Profiling data](https://developers.meta.com/vr/blog/graphics-showcase-using-vulkan-subpasses-in-ue4-for-performant-tone-mapping-on-quest/)：Oculus UE 4.26 fork 在 Vulkan 的色调映射 subpass 中直接取当前像素。非 MSAA 路径对场景色设 `Clear_DontStore`，色调映射输出设 `Clear_Store`；有 MSAA 时在 shader 取各样本并平均，最后写单样本。作者在 Quest 2 VRAPI 日志报告某些配置加色调映射最低约 **0.6 ms**，2× MSAA + FFR3 增量不足 1 ms，4× MSAA 取样效率变差。这是**加入效果的开销**，不能当作相同色调映射在两个 Render Pass 之间省下 0.6 ms；现有正文未给可核的外部带宽 A/B 或逐行计时表。
- [Khronos Vulkan Samples · Subpasses，Deferred rendering / Merging](https://docs.vulkan.org/samples/latest/samples/performance/subpasses/README.html)：同一 G-buffer 光照示例可切换两个 Render Pass 与单 Render Pass 的两个 subpass。文中 Galaxy S10（Mali-G76，2220×1080）给出合并与分离分别 **262.2k 和 614.7k 物理 tile/s**。实际查看的 Streamline 图在约 3.7 秒从合并切到分离后，Tile Buffer 写入和外部读写未完成事务的曲线抬升。物理 tile/s 会随帧率变化，不能换算为每帧 DRAM 字节，也不能把正文的近似带宽说法直接搬作后处理收益。该样例比较的是延迟光照，不是色调映射。
- [Arm · Vulkan subpasses: the good, the bad, and the ugly](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/vulkan-subpasses-the-good-the-bad-and-the-ugly)：合并取决于驱动、附件和配置。Arm 给出的反例是前一阶段只有局部几何、后一阶段全屏；合并后每个 tile 都有几何，前段可能失去跳过空 tile 的机会，特别是 `loadOp=LOAD` 时。由此在低覆盖、静止画面下测到的结果不一定代表实际游玩。

## 怎样决定要不要合并

1. 按数据依赖拆开效果。只需本像素及外部固定 LUT 的操作先试合并；要读邻像素、多分辨率链或历史帧的操作保留适合它的单独中间资源。中间颜色仅在后续没有读者时才可丢弃；先查图中的最后消费者和 Resolve，再检查结果颜色、采样数及透明合成顺序。
2. 用同一场景、效果和分辨率做 A/B：一版场景色先存后读，一版用片上本像素输入。核对两个方案输出画面和 pass 边界；在支持的 Vulkan 设备用合并反馈或硬件 Render Pass 时间线复核，不能只凭引擎 Frame Debugger 数逻辑 Pass。固定 MSAA、动态分辨率和帧率档；分开测静止低覆盖与运动代表场景。
3. 记录 GPU 时间、外部读/写字节每帧，以及稳定温度下的帧率和频率。没有字节计数器就标缺项，别拿物理 tile/s 或 L2 写字节冒充 DRAM。带宽降但帧时没降时，检查片上访问与调度是否成为瓶颈；再单独测试半分辨率等替代方案，别把少算像素的收益归给合并。

**状态**：有作者 Quest 2 附加耗时和 Khronos 合并示例截图；无本地或目标项目真机 A/B，未测降分辨率。
