# TA-260 · Shader Graph 自定义函数切精度后签名错配

**问题与结论。** 同一份 HLSL 在一处能编译、接到另一处却找不到 `_half` 或 `_float` 时，先查生成调用的后缀，再查 include 里的声明。图设为 Half 也可能生成 `_float`；反过来，在 Single 图里拔掉一条 Half 输入线，未接端口可能让节点从 `_half` 改回 `_float`。

## 公开证据

- **Unity 6000.0 / Shader Graph 17.0.4。** [精度文档](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Documentation~/Precision-Modes.md#L34-L46)说明 Half 与 Single 混接时 Single 优先。[节点源码](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Nodes/AbstractMaterialNode.cs#L563-L605)还逐个处理未接端口：在 Inherit 模式，这类端口参与合并时记为 Graph。[优先级](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Enumerations/Precision.cs#L36-L62)是 Single、Graph、Half，最后将 Graph 解析为图默认精度。显式设节点为 Half 或 Single 会绕过这套输入继承。
- **两种模式都生成带后缀的调用。** [节点构造函数名](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Nodes/Utility/CustomFunctionNode.cs#L110-L112)时拼接 `_$precision`，[生成调用](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Nodes/Utility/CustomFunctionNode.cs#L184-L196)使用该名称，[生成器](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Generation/Processors/GenerationUtils.cs#L1203-L1208)按节点实际精度替换。Name 填裸名 `Mix`。[File 模式](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Nodes/Utility/CustomFunctionNode.cs#L268-L296)只登记 include，不替外部文件生成或改写函数体；按[文档](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Documentation~/Custom-Function-Node.md#L68-L90)提供各调用需要的具体符号，检查参数类型、顺序和 `out`。只提供 `Mix_half` 时，`Mix_float` 是另一个符号，输入的隐式类型转换不能补出它。
- **String 模式生成定义。** [节点](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Nodes/Utility/CustomFunctionNode.cs#L284-L331)从端口拼声明并追加 Body；[注册器](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Data/Util/FunctionRegistry.cs#L73-L125)按通用函数名记录已生成的精度，并将正文中的 `$precision` 一起替换。两条活跃分支若用相同 Name、端口和 Body，可以在同一 Pass 生成两套签名；Body 里写死的 `float` 保持原样。若想为两档写不同算法，别在 String 模式用同一个 Name 区分：[主生成器启用实现校验](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.shadergraph/Editor/Generation/Processors/Generator.cs#L692-L695)，注册器比较的是替换精度前的代码，哪怕具体后缀不同，同名定义仍会冲突。可给算法不同名字，或在 File 中明确提供两套入口。

## 最小对照与验证状态

固定两个 Vector1 输入 A/B、一个 Vector1 输出 Out，以及只做 `Out = A + B;` 的函数。让两路都进入同一 Pass 的最终输出，避免分支未被生成。按上述规则复算：

| 图默认 | 节点 | A / B | 预期生成调用 |
| --- | --- | --- | --- |
| Single | Inherit | 显式 Half / 显式 Half | `Mix_half(A, B, Out)` |
| Single | Inherit | 显式 Half / 未接线 | `Mix_float(A, B, Out)` |
| Half | Inherit | 显式 Half / 显式 Single | `Mix_float(A, B, Out)` |
| Single | 显式 Half | 显式 Half / 未接线 | `Mix_half(A, B, Out)` |

第一、二行放在同图时，File 对应声明分别是 `void Mix_half(half A, half B, out half Out)` 和 `void Mix_float(float A, float B, out float Out)`。先让 include 只有第一套，再补第二套；另用相同图改成 String 并用 `$precision` 声明局部中间值，检查生成的函数体与调用。生产修复按意图选：确实需要两档就补两种入口；仅固定一种精度时可锁住节点，另验降精度会不会伤及 UV、大数减法或高亮范围。

**状态。** 已核对官方文档和固定提交源码，四行是按源码逻辑复算的预期；未在 Unity 生成或编译 Shader，也未测目标 GPU 的实际算术精度或耗时。

## 生成 half 后怎样核对实际精度

**制作决定。** 函数名的 `_half` 是调用约定，`half2` 是源码类型。要降低移动端算术成本，沿目标运算的输入追到反汇编中的指令类型和升降精度转换，再测目标 GPU；纹理格式、常量布局与算术位宽分开验。

- **Unity 6000.0 的映射。** [《Use 16-bit precision in shaders》](https://docs.unity3d.com/6000.0/Documentation/Manual/SL-Use16BitPrecisionInShaders.html)说明默认在部分平台把 `half` 当 `float`；Shader Precision Model 设为 Uniform 后，标量按 `min16float` 处理，Vulkan 是带 `RelaxedPrecision` 的 float，D3D11/12 保留 `min16float`。[微软最低精度说明](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/using-hlsl-minimum-precision#declare-variables-with-minimum-precision-data-types)明确允许硬件按 32 位执行，且运算精度由表达式操作数决定，写入半精度变量不能倒推运算也降了精度。Unity 文档还说 `2.0h` 会被视为 float；追混合精度表达式时同时检查常量和显式类型转换。
- **DXC v1.8.2505.1 的 SPIR-V 对照。** [映射表](https://github.com/microsoft/DirectXShaderCompiler/blob/v1.8.2505.1/docs/SPIR-V.rst#L702-L746)和[乘法用例](https://github.com/microsoft/DirectXShaderCompiler/blob/v1.8.2505.1/tools/clang/test/CodeGenSPIRV/decoration.relaxed-precision.basic.hlsl#L9-L46)里，未启用显式 16 位类型时 `min16float` 可生成带 `RelaxedPrecision` 的 `OpFMul %float`，其 IR 类型是 32 位；[开启显式 16 位的用例](https://github.com/microsoft/DirectXShaderCompiler/blob/v1.8.2505.1/tools/clang/test/CodeGenSPIRV/constant.scalar.16bit.enabled.hlsl#L18-L43)检查 `Float16` 能力和 `OpTypeFloat 16`。这是通用 DXC 用例，不能据此认为 Unity Player 采用了同样的编译开关。
- **同一个运算、同样的 half2 输出，内部仍可不同。** 在 DXC 同一[回归用例](https://github.com/microsoft/DirectXShaderCompiler/blob/v1.8.2505.1/tools/clang/test/CodeGenSPIRV/constant.scalar.16bit.enabled.half.zero.hlsl#L18-L48)中，`half2 e = saturate(d)`（`d` 是 `half2`）检查 `FClamp %v2half`；`half2 k = (half2)saturate(j)`（`j` 是 `float2`）检查 `FClamp %v2float` 后接 `OpFConvert %v2half`。即便输入也是 half，[ddx 用例](https://github.com/microsoft/DirectXShaderCompiler/blob/v1.8.2505.1/tools/clang/test/CodeGenSPIRV/intrinsics.ddx.half.hlsl#L5-L18)仍预期先升到 float、做 `OpDPdx`、再降回 half。所以模块出现 `OpTypeFloat 16` 也还要查热点指令。
- **存储另查。** Unity 6000.0 文档说明通常按标量 32 位大小和对齐存放缓冲里的 `half`，Metal 的 CPU 可见缓冲还有文档所列的兼容 pragma；以目标常量缓冲的反射偏移和布局为证。Vulkan 的 [`shaderFloat16`](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceShaderFloat16Int8Features.html)允许相应算术能力，不自动允许 Uniform、StorageBuffer 或阶段接口中的 16 位存储。Unity 的 `Texture2D<half4>` 是采样声明；纹理尺寸、资源格式与驻留字节须另看导入和实际绑定。

**验证计划与状态。** 固定 Unity 版本、图形 API、目标设备、采样器、纹理格式和场景；用动态输入驱动同一小段运算以免被折叠。分别对照 float、只把输出写成 half、输入与中间量都写成 half；在编译产物里记录具体指令类型、转换、常量布局与采样资源，再在同一 Pass 对照画面误差、GPU 时间和可用的寄存器指标。已读官方文档和 DXC 回归测试的预期反汇编；未运行这些测试或编译 Unity 变体，未测真机性能。
