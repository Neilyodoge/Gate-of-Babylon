# TA-36 · 移动端网格索引：按跨度、重复顶点和剔除来选

**制作决定**：目标设备都支持 32 位且希望保持原提交方式时，先把 32 位单网格当对照。要用 16 位时，先检查现有子网格的顶点**编号跨度**能否用基顶点缩到局部区间；不行再试重排或拆块。拆块是否值得，还要看跨块重复的顶点和是否真的能单独剔除。

## 实际约束和公开实现

- **Unity 6000.0：基顶点不做重映射。** [Mesh.SetIndices](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/mesh/setindices) 的例子把 `[10,11,12]` 加上 `baseVertex = 100000`，因此会引用顶点 100010–100012。选择该组最小的全局顶点编号作为基顶点后，最大局部索引就是 `maxIndex - minIndex`。例如 `[2,500,90000]` 虽然只有三个独立顶点，跨度仍有 89998，不能只靠一个基顶点塞进 16 位。要改的是这组顶点的排列或一对一重映射，给现有索引统一减一个常数无法缩短跨度。
- **从导入到目标设备。** Unity 的 [ModelImporterIndexFormat.UInt16](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/modelimporterindexformat/uint16) 明确超过 65535 顶点会在导入时拆块，不承诺如何划分。[Mesh.indexFormat](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/mesh/indexformat) 说明部分 API 或 GPU 可能跳过使用最大索引的三角形，切换格式还会清掉索引并把子网格数变成 1；运行时生成应先设格式，再填索引和子网格。[glTF 2.0 · Meshes](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#meshes) 明确禁用该类型的最大索引，输出可移植的 UInt16 编号时用 0–65534。用 Unity 的 [SystemInfo.supports32bitsIndexBuffer](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/systeminfo/supports32bitsindexbuffer) 在实际 Player 检查 32 位支持；Unity 文档举老 Mali-400 不支持的例子。这个布尔值不验证基顶点在项目目标后端的渲染结果或性能。
- **Assimp v5.4.3：拆块不是把每面顶点都复制，也不保证空间聚类。** [SplitLargeMeshes.cpp 的分块与映射重置](https://github.com/assimp/assimp/blob/v5.4.3/code/PostProcessing/SplitLargeMeshes.cpp#L405-L428) 为每块建新网格并重置旧到新编号；[按面累加](https://github.com/assimp/assimp/blob/v5.4.3/code/PostProcessing/SplitLargeMeshes.cpp#L453-L471) 遇到下一面超出本块顶点预算时结束本块；[重用和复制](https://github.com/assimp/assimp/blob/v5.4.3/code/PostProcessing/SplitLargeMeshes.cpp#L495-L549) 重用同块顶点，跨块共享的顶点则再次复制相关属性。这里按已有面序走，不是按空间包围盒划块。它是 CPU 端切块的公开案例，不能据此推断 Unity 的切法或 Assimp 已替客户端创建了 16 位 GPU 缓冲。

## 算例：什么时候省下的索引抵不过重复顶点

同一份含 20 万三角形的静态网格有 60 万个索引。每个索引从 4 B 缩到 2 B，少 **1,200,000 B**。若各顶点流合计 48 B/顶点，拆块新增 8,000 个顶点会多 384,000 B，合计还省 **816,000 B**；新增 30,000 个会多 1,440,000 B，合计反而多 **240,000 B**。在索引数量和顶点格式不变时，临界是 `2 × 索引数 / 顶点步长`，此例为 25,000 个额外顶点。这里比较原始 GPU 缓冲数据，不计对齐、CPU 副本、额外蒙皮流、提交或压缩包大小。

## 怎么验收

1. 用同素材和同材质对照 32 位、16 位加基顶点、16 位空间拆块。记录每组实际索引跨度、索引数量、总顶点数和各流步长，先在 Player 抓帧核对真实索引格式、基顶点、最后一个可用编号和是否缺面；对照三角拓扑、法线与 UV，蒙皮资产另验骨骼权重。
2. 用全部可见和只露一角的两个镜头，分别测可见分块数、实际提交数、CPU 渲染提交和 GPU 时间；阴影等额外 Pass 一并记。只有独立包围盒确实参与剔除，空间分块才可能减少送绘的几何。额外子网格可能增加绘制，却仍共用一个 Renderer 的剔除范围，须用实际路径确认。

**状态**：已核对 Unity、glTF 规范与 Assimp 源码，字节数是可复算的模型；未导入同素材对照，未做目标机兼容或帧时测试。
