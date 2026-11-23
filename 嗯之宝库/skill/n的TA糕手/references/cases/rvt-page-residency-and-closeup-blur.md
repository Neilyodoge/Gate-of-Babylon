# TA-47 · RVT 贴近仍糊：先分清页来不及、留不住，还是一直在读粗 mip

**制作决定**：相机停在近景后，先看所有虚拟纹理物理池的驻留偏置，再比较冷、热缓存的变清轨迹。如果另一个池正在施加全局偏置，仅扩地面自己的池可能没用。没有全局偏置、热缓存正常、冷缓存最终也能变清时，才用单变量对照查请求和生产速度。稳定后仍糊，则进一步确认采样解析到哪一级，以及细页本身有没有细节。

## 公开证据

- **Epic · 物理池驻留与原始统计。** [驻留偏置](https://dev.epicgames.com/documentation/unreal-engine/virtual-texture-memory-pools-in-unreal-engine#residencymipmapbias)取各物理池当前偏置的最大值，并应用到所有 VT 采样；这是给启用了相应设置的超量池使用的退化。没有该退化时，超量池反复换出可见页可能闪烁。[驻留 HUD](https://dev.epicgames.com/documentation/unreal-engine/virtual-texture-memory-pools-in-unreal-engine#physicalmemorypoolheads-updisplay)中红线是占用，黄线是锁定页占用，绿线是为减轻超量施加的偏置。锁定页可由当前不可见但已加载的 VT 资产占用；只看当前构图或目标池的红线会漏掉原因。编辑器自动扩容用的临时池设置也不会自动成为打包配置。
- **Epic · 可定位的池内异常。** [Oversubscription 页数转储](https://dev.epicgames.com/documentation/unreal-engine/virtual-texture-memory-pools-in-unreal-engine#oversubscription)中，同一个 DXT1 池里排首位的沙地 VT 占 1912 页，第二项 418 页。这个约 4.6 倍的差距提供了先查哪张贴图的线索，无法单凭计数断言它设置了负偏置。文档也给出 RVT 自定义 World Position 梯度为零的例子：该特定情况下可试 `Ignore Input WorldPosition`；不要套给依赖自定义坐标梯度的材质。
- **Epic · 更新与采样是两个控制面。** [RVT 调试及扩展性](https://dev.epicgames.com/documentation/unreal-engine/runtime-virtual-texturing-in-unreal-engine#console-variables-and-scalability)区分打包运行和编辑器的每帧上传配额。RVT 页由 GPU 按需绘制；若低 mip 已预烘焙为 SVT，那部分从流送而来。用 [Build Debug](https://dev.epicgames.com/documentation/unreal-engine/runtime-virtual-texturing-in-unreal-engine#streaming-virtual-texture-low-mips-debugging)给预烘焙低 mip 上色，只能定位它们的使用范围。[设置参考](https://dev.epicgames.com/documentation/unreal-engine/virtual-texturing-settings-and-properties-in-unreal-engine)另列 RVT Sample 的自动 mip 与显式 `MipLevel`，后者关闭各向异性过滤；反馈分辨率提高也有 CPU/GPU 成本。[RVT 限制](https://dev.epicgames.com/documentation/unreal-engine/runtime-virtual-texturing-in-unreal-engine#additional-notes-and-limitations)还注明顶点采样不会产生 VT GPU 反馈，普通流送贴图还没就绪时也可能把低清内容写进 RVT 页。

## 最小对照

固定版本、画质、动态分辨率、视角和移动轨迹；用同一近景停住，记录首个稳定清晰帧、静止时清晰度是否反复跳变、所有池曲线和该池实际内存。用相同冷启条件与热缓存各跑一遍；`stat virtualtexturing` 的全局计数只能辅助，能抓到时按该 RVT 的区域、页坐标和 mip 分开记反馈请求、页生成／映射与最终画面。

1. **页留不住。** 静止仍反复跳或偏置不归零，先看 `r.VT.Residency.Show 1` 的所有池，结合 `r.VT.DumpPoolUsage` 找到触发池及占页资产；只扩大该格式组合与 tile size 对应的池，或单独移除一个明确的用量来源。看绿线、清晰度和实际显存是否一起变化；若是大批锁定页占位，清理常驻资产也可作为单独对照。
2. **页来不及。** 先确认细页有请求、驻留无压力，且冷进慢、热进清。保持反馈与池容量不变，只改当前运行模式的上传配额；记录变清帧数以及 RVT 生成和总帧时峰值。若反馈请求本身未及时出现，先查采样阶段、反馈分辨率和视角，而不是加上传配额。
3. **页已到但仍糊。** 用相同读写路径输出无光照诊断图；可用 [Virtual Texture Output Level](https://dev.epicgames.com/documentation/unreal-engine/runtime-virtual-texturing-in-unreal-engine#additional-material-expressions)在写页时给各 mip 编不同标色，再由正常材质采样回读（插值区的混色不当精确页号）。采到粗页时检查手动 mip、坐标／导数与反馈；确认采到最细页还糊，再对照 RVT 覆盖范围和写入源。算例：512 m 宽的区域若在 mip 0 只有 2048 个有效纹素，约每 25 cm 才一个纹素，扩池也不会增加细节；应比较缩小覆盖、提高有效分辨率，或把近景高频细节留给实时材质。若同级页的内容随源贴图就绪而改变，对照源已就绪后再重建该页，避免误加池容量。

**状态**：核对 Epic 的公开文档和 DXT1 页数转储，覆盖范围为算例。未目视驻留 HUD 原图，未执行本地近景 A/B 或真机测量。
