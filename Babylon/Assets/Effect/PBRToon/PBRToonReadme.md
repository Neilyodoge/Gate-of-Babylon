# PBRToon 卡通渲染系统

## 简介

从 DanbaidongRP 的 PBRToon 材质系统移植到 URP（Universal Render Pipeline）的卡通渲染方案。  
保留了原版的 PBR 物理参数控制 + 卡通风格化表现，适用于角色渲染。

## 文件结构

```
Assets/Effect/PBRToon/
├── Editor/                             # 自定义材质面板 GUI
│   ├── PBRToonBaseShaderGUI.cs
│   ├── PBRToonFaceShaderGUI.cs
│   └── PBRToonHairShaderGUI.cs
├── Scripts/                            # 运行时脚本
│   ├── CharacterShadowAtlasRenderFeature.cs  # 角色 Atlas 阴影 RenderFeature
│   ├── CharacterShadowAtlasTarget.cs         # 角色 Atlas 阴影目标组件
│   └── HairShadowRenderFeature.cs            # 前发投影 RenderFeature（屏幕空间 mask）
├── PBRToonFaceDirection.cs             # 面部朝向脚本（运行时组件）
└── Shaders/
    ├── PBRToonCommon.hlsl              # 公共工具函数库（光照/RimLight/IBL 等）
    ├── PBRToonOutline.hlsl             # 描边算法库（背面法线外扩 + 视距自适应）
    ├── PBRToonSkin.hlsl                # 皮肤 SSS 库（基于视角的轻量级假 SSS）
    ├── ToonShadowFilter.hlsl           # 自定义 PCF/PCSS 阴影滤波库
    ├── PBRToonBase.shader              # 基础 Shader（身体/衣服/裸露皮肤等）
    ├── PBRToonFace.shader              # 面部 Shader（SDF 阴影 + 鼻尖高光）
    └── PBRToonHair.shader              # 头发 Shader（各向异性高光）
```

## Shader 列表

### PBRToon/Base
- **路径**：`Universal Render Pipeline/PBRToon/Base`
- **用途**：身体、衣服、裸露皮肤等通用部位
- **特性**：
  - PBR Mask 贴图（金属度/光滑度/AO/自发光）
  - Normal Map
  - Shadow Ramp（可选，ramp 贴图由 `nTools/美术工具/Ramp生成工具` 生成，详见下文）
  - 自定义间接光照（SH + Cubemap）
  - 屏幕空间 Rim Light
  - **SSS 皮肤（可选）**：基于视角 (Fresnel) 的轻量级假 SSS，开 `_SKIN_ON` 启用，
    `_SSSColor` 控制肉粉色、`_SSSArea` 控制范围/强度，掠射角处 albedo 朝
    `_SSSColor` 偏移，模拟耳廓/鼻翼/手指边缘的红透感（实现见 `PBRToonSkin.hlsl`）

### PBRToon/Face
- **路径**：`Universal Render Pipeline/PBRToon/Face`
- **用途**：角色面部
- **特性**：
  - SDF 脸部阴影贴图（Face Lightmap，UV1 采样）
  - 鼻尖高光
  - 前发投影（可选，需配合 `HairShadowRenderFeature`）
  - **SSS 皮肤（可选）**：与 Base 共享 `PBRToonSkin.hlsl`，开 `_SKIN_ON` 启用，
    `_SSSColor` / `_SSSArea` 控制肉色和强度，对脸部边缘（颧骨外沿、下颌侧、耳廓）
    自动产生红透感
  - 需要挂载 `PBRToonFaceDirection` 组件
- **Face Lightmap 通道说明**：
  - R：SDF 阴影阈值
  - G/B：鼻尖高光区域
  - A：脸部阴影遮罩

### PBRToon/Hair
- **路径**：`Universal Render Pipeline/PBRToon/Hair`
- **用途**：角色头发
- **特性**：
  - 各向异性头发高光（HairSpecTex，UV1 采样）
  - AnisotropicSlide / AnisotropicOffset 控制高光偏移
  - BlinnPhong 风格的高光幂次

## 使用方法

### 基础使用
1. 在材质面板选择对应的 Shader
2. 配置基础贴图和 PBR 参数
3. 调整 Shadow 和 Rim Light 参数

### Face Shader 专用设置
1. 在角色的面部 Renderer 所在的 GameObject 上添加 `PBRToonFaceDirection` 组件
2. 将 `faceTransform` 指向角色的头部骨骼
3. 根据模型朝向调整 `flipRight` / `flipForward`
4. 制作 Face Lightmap 贴图并赋予材质

### Hair Shader 专用设置
1. 制作 HairSpecTex（头发高光遮罩贴图）
2. 调整 AnisotropicSlide 控制视角偏移
3. 调整 BlinnPhongPow 控制高光锐度

## 技术说明

### 与 DanbaidongRP 的主要差异
| DanbaidongRP | URP 适配方案 |
|---|---|
| 延迟渲染 GBuffer + 前向补光 | 纯前向渲染 |
| GPUCulledLights 光源循环 | GetMainLight + LIGHT_LOOP |
| PreIntegratedFGD LUT 查表 | Schlick 近似 |
| SampleSH9 自定义探针 | URP SampleSH |
| ToonFlags（GBuffer 标记） | 未移植（仅前向不需要） |
| 光追 Pass | 未移植 |

### 渲染队列
- Face：`Geometry-10`（先渲染脸部）
- Base：`Geometry`
- Hair：`Geometry+10`（后渲染头发）

### Pass 复用
Face 和 Hair 的 ShadowCaster、DepthOnly、DepthNormals 通过 `UsePass` 复用 Base Shader 的实现。

### UV 通道分配约定

| UV 通道 | 语义 | 用途 | 维度 | 写入工具 |
|---------|------|------|------|---------|
| UV0 (TEXCOORD0) | 主纹理坐标 | 基础贴图、PBR Mask、法线贴图等 | 2D | DCC 软件 |
| UV1 (TEXCOORD1) | Lightmap / 自定义 | Face Lightmap (SDF)、Hair SpecTex | 2D | DCC 软件 |
| **UV2 (TEXCOORD2)** | **Bent Normal** | Visibility Cone 遮蔽数据 | 4D (Vector4) | Bent Normal Baker |
| **UV3 (TEXCOORD3)** | **Outline 平滑法线** | PBRToon 描边用切线空间平滑法线 | 3D (xyz) | Outline 平滑法线烘焙 |

> ⚠️ UV2 和 UV3 的用途已固定分配，两个工具不会互相冲突。
>
> 关于"UV 通道维度"：Unity 的 TEXCOORD0~7 每个通道最多可存 4 个 float 分量（不是只能存 2 个 UV 坐标），
> 因此 UV2 可以装下 Bent Normal 的 Vector4、UV3 可以装下平滑法线的 Vector3，无需任何压缩。

### 阴影采样系统

**核心文件**：`ToonShadowFilter.hlsl`

#### 双路径架构（CSM + Atlas Shadow）

阴影系统采用 CSM 和角色 Atlas Shadow 双路径架构：

| 阴影来源 | 采样方式 | 说明 |
|---|---|---|
| **CSM（级联阴影）** | 自定义 PCF/PCSS | Editor 下默认使用，通过 `ToonMainLightShadow` 采样 `_MainLightShadowmapTexture` |
| **Atlas Shadow（角色阴影）** | 自定义 PCF/PCSS | Runtime 下使用，通过 `SampleCharacterAtlasShadow` 采样角色专用高分辨率阴影图集 |

入口函数 `ToonMainLightShadowWithCharacterAtlas` 自动融合两种阴影源：
- 当 Atlas 覆盖当前像素时，使用 Atlas Shadow 结果
- 否则回退到 CSM Shadow

#### The Witness 优化 PCF

参考 The Witness（见证者）的 GPU Shadow Mapping 优化技术，利用硬件 2x2 PCF 的双线性插值特性，用更少的纹理采样覆盖更大的滤波核。

| 模式 | 关键字 | 采样次数 | 等效比较次数 | 说明 |
|------|--------|----------|-------------|------|
| Base | `_TOON_SHADOW_BASE` | 1 次硬件 PCF | 4 次 | 最快，硬阴影 |
| PCF 2x2 | `_TOON_SHADOW_PCF_2X2` | 1 次硬件 PCF | 4 次 | 同 Base，显式选择 |
| PCF 3x3（默认） | 无 | 4 次硬件 PCF | 16 次 | 性能好，适合大部分场景 |
| PCF 5x5 | `_TOON_SHADOW_PCF_5X5` | 9 次硬件 PCF | 36 次 | 更柔和的阴影边缘 |
| PCF 7x7 | `_TOON_SHADOW_PCF_7X7` | 16 次硬件 PCF | 64 次 | 最高质量固定核 |
| PCSS | `_TOON_SHADOW_PCSS` | 动态 | 动态 | 可变半径软阴影（距离自适应） |

#### PCSS（Percentage Closer Soft Shadows）

PCSS 模式提供距离自适应的软阴影效果，近处阴影锐利、远处阴影柔和。

材质面板可调参数：
- **Softness**：整体软阴影强度
- **Softness Falloff**：软度随距离的衰减曲线
- **Blocker Samples**：遮挡物搜索采样数
- **Filter Samples**：PCF 滤波采样数
- **Blocker Gradient Bias**：遮挡物搜索的梯度偏移
- **PCF Gradient Bias**：PCF 滤波的梯度偏移

使用 IGN（Interleaved Gradient Noise）抖动采样点，减少带状伪影。

#### Shadow Edge Color

在阴影边缘区域叠加多段渐变颜色，增强视觉层次。搬运自 V114 yarp 管线的 `GetShadowEdgeColor2`。

- **Begin/End**：核心渐变区域的阴影值起止
- **Begin Color / End Color**：暗端/亮端颜色
- **Dark Color / Light Color**：全暗/全亮区域的颜色
- **Fade Width**：暗端/亮端的平滑过渡宽度

通过 `_SHADOW_EDGE_COLOR` keyword 控制开关。

#### Shadow Ramp 贴图（明暗交界线着色）

Shadow Ramp 用于把 `shadowNdotL` 这个 [0,1] 灰度变量映射成有色阶过渡的颜色，
形成卡通的明暗交界硬过渡 + 暖色暗部 / 冷色亮部之类的效果。贴图是一张横向 ramp
PNG，UV.x = `shadowArea`、UV.y = `0.5`（单行）或 `1.0 - (row + 0.5) / rowCount`
（多行）。

**生成工具**：`nTools/美术工具/Ramp生成工具`（详见 `Assets/Tools/ToolsReadme.md`
中"Ramp 生成工具"小节）。

- 工具用 Unity Gradient 配色，可以一张 PNG 烘多条 ramp（纵向叠 N 行），shader
  端通过 UV.y 选不同行
- 已生成的 ramp PNG 重新打开会自动恢复 Gradient 列表（数据保存在 importer
  userData 里），方便美术继续微调
- 工具会自动配置 importer：`Wrap=Clamp / Filter=Bilinear / sRGB=true / 不压缩
  / 不生成 mipmap`，避免 ramp 边缘出错
- 推荐尺寸：`SingleRampSize = 256 × 4`（X 高一些保过渡平滑，Y 给到 4 像素够采）
- 推荐保存路径：`Assets/Effect/PBRToon/RampTextures/`（与 shader 同工程区域）

**配套 shader 参数**（`PBRToonBase.shader`）：

| 属性 | 作用 |
|---|---|
| `_ShadowRampTex` | 上面工具生成的 ramp PNG |
| `_ShadowOffset` | 明暗交界线在 NdotL 上的偏移（推暗 / 推亮） |
| `_ShadowSharpness` | 明暗交界过渡锐度（越大越接近卡通硬阶） |

#### 阴影混合流程

```
shadowScene = ToonMainLightShadowWithCharacterAtlas(...)  // 实时阴影（CSM 或 Atlas）
shadowScene = SigmoidSharp(shadowScene, 0.5, _ShadowSmoothScene)  // 锐化

shadowNdotL = SigmoidSharp(NdotL, _ShadowOffset, _ShadowSharpness)  // 明暗交界线

shadowArea = min(shadowNdotL, shadowScene)  // 实时阴影 ∩ 明暗交界线
shadowArea = lerp(1, shadowArea, _ShadowStrength)  // 阴影强度

shadowRamp = Shadow Ramp 着色  // 可选
shadowRamp = Shadow Edge Color  // 可选
```

> **概念区分**：Shadow（实时阴影 shadowScene）和 Ramp（明暗交界线 shadowNdotL）是两个独立部分，最终通过 `min` 混合。

### Debug 模式

材质面板 **⚠ Debug (Editor Only)** 区域提供阴影可视化调试，使用 `shader_feature_local` 变体，不会被打包进最终构建。

| Debug 模式 | 显示内容 | 变量 |
|---|---|---|
| **Shadow** | 实时阴影（shadowMap 采样结果） | `shadowScene` |
| **Ramp** | 明暗交界线 | Base/Hair: `shadowNdotL`，Face: `faceMapShadow` |

### 角色 Atlas 阴影

通过 `CharacterShadowAtlasRenderFeature` 为角色渲染独立的高分辨率阴影图集。

**使用方式**：
1. 在 URP Renderer 中添加 `CharacterShadowAtlasRenderFeature`
2. 在需要投射 Atlas 阴影的角色上添加 `CharacterShadowAtlasTarget` 组件
3. Shader 中通过 `_CHAR_SHADOW_ATLAS_ON` keyword 启用

### 前发投影系统（HairShadowRenderFeature）

**核心文件**：`Scripts/HairShadowRenderFeature.cs`

#### 设计目的

把"头发投在脸上的阴影"从 URP 主光 CSM 体系里**单独抽出来**，用屏幕空间 mask 实现，
让美术可以专门控制阴影颜色（`_HairShadowColor`）和软度，不和场景统一阴影耦合。

#### 工作流程

1. **HairShadowMask Pass**（`BeforeRenderingOpaques`）：分配 R8 全屏 RT，
   用 Hair mesh 的 `HairShadowMask` Pass Tag 把前发区域画为白色，背景为黑色
2. **设全局贴图** `_HairShadowMask`，供 Face shader 采样
3. **Face Forward Pass**：在 `_HAIR_SHADOW` 开启时按屏幕 UV 采样 mask，
   `mask=1` 的脸部像素叠加 `_HairShadowColor` 染色

#### 推荐使用方式

> ⚠️ 这个 Feature 的设计意图是**替代** Hair 在脸上的 CSM 阴影，不是叠加在它上面。
> 如果两个都开，脸上会同时有 CSM 硬阴影 + 屏幕空间软 mask，效果重叠且 CSM 阴影会
> "穿模"出现在不该有的位置。

正确启用步骤：
1. 在 URP Renderer 中添加 `HairShadowRenderFeature`
2. 把 Hair 物体的 `MeshRenderer.Cast Shadows` 设为 **Off**（关掉 CSM 投脸路径）
3. Hair 物体所在 Layer 加进 `HairShadowRenderFeature` 的 `hairLayerMask`
4. Hair shader 必须实现 Pass Tag = `HairShadowMask` 的 Pass（`PBRToonHair.shader` 已带）
5. Face Material 上勾选 `_EnableHairShadow`、调 `_HairShadowColor`

#### Disable 时的清理（已修复）

`ScriptableRendererFeature` 顶部那个 enabled checkbox 实际调的是 `SetActive(false)`，
URP 内部**只改字段不触发任何回调**，导致 `_HairShadowMask` 全局贴图槽里的旧 RT
引用残留，Face shader 在 `_HAIR_SHADOW` 仍开启时会继续采到上一次烘进去的 mask。

修复覆盖三条 disable 路径：

| 关闭方式 | 触发机制 | 处理 |
|---|---|---|
| 顶部 RenderFeature checkbox（`SetActive(false)`） | URP 不触发任何回调，靠 `EditorApplication.update` 每帧轮询 `isActive` | 切换瞬间清理 |
| 内层 `settings.enabled` | URP `OnValidate` → `Create()` + `AddRenderPasses` 状态机 | 双保险清理 |
| 删除 Feature / 切 Pipeline / Domain Reload | `Dispose(disposing)` + `OnDisable` | 兜底清理 |

清理动作 = 释放 `RTHandle` + `Shader.SetGlobalTexture(_HairShadowMask, Texture2D.blackTexture)`，
Face shader 在 `_HAIR_SHADOW` 仍开启时采到的 mask 永远是 0，等价于"无前发遮挡"。

#### 跟 CSM 主光阴影的区分

如果发现脸上还有头发形状的阴影残留，先排除 CSM：临时把 Hair 物体的
`MeshRenderer.Cast Shadows` 设为 Off：
- 阴影**完全消失** → 是 URP 主光 CSM 投影，跟本 Feature 无关
- 阴影**仍在** → 才是 `_HairShadowMask` RT 残留，按上面的 disable 路径排查

### 描边系统

- **算法**：原神风格背面法线外扩描边
- **RenderFeature**：需要在 URP Renderer 中添加 `ToonOutlineRenderFeature`
- **兼容性**：如未烘焙平滑法线（UV3 为空），shader 自动回退到原始 normalOS（描边会有锯齿和缺口）

#### 平滑法线编码（重要）

| 项目 | 说明 |
|------|------|
| 存储位置 | `TEXCOORD3.xyz`（默认；工具支持任意 UV0~UV7，但 PBRToonBase 当前 hard-code 读 TEXCOORD3） |
| 数据维度 | **Vector3 / 3 分量** —— xyz 全部直接存储，**没有任何压缩或重建** |
| 坐标空间 | 切线空间（已用 TBN 矩阵的转置把对象空间法线转过去） |
| 算法 | 角度加权平滑法线（按顶点位置分组，三角面夹角作权重累加面法线） |

> 早期版本的描述（"2 通道 + 勾股定理重建 z"）已废弃。当前方案直接 3 通道 xyz 存取，
> 顶点 1 个 UV slot 总共有 4 个 float 容量，多用 1 个 float 换不掉精度损失，没必要压缩。

#### Shader 解码（`PBRToonBase.shader` Outline Pass）

```hlsl
// 顶点结构里把平滑法线的 UV 通道声明为 float4（取前 3 分量）
struct OutlineAttributes {
    ...
    float4 uv3 : TEXCOORD3; // 平滑法线，切线空间 xyz
};

// 顶点着色器中
float3 snTS = input.uv3.xyz;
if (dot(snTS, snTS) > 0.001)  // 烘焙过 → 用平滑法线
{
    float3 T = normalize(input.tangentOS.xyz);
    float3 N = normalize(input.normalOS);
    float3 B = normalize(cross(N, T) * input.tangentOS.w);
    smoothNormalOS = normalize(T * snTS.x + B * snTS.y + N * snTS.z);
}
else                          // 未烘焙 → 回退原始法线
{
    smoothNormalOS = input.normalOS;
}
```

> 注意：`cross(N, T) * w` 是在算 **B（副切线）**——这是 Unity 标准做法（顶点只存 T+w 和 N，B 在 shader 里现算），
> 跟"平滑法线分量重建"无关。平滑法线本身是 3 分量直存直读。

#### 烘焙工具

- **菜单路径**：`nTools/美术工具/Outline平滑法线烘焙`
- **核心特性**：
  - 支持 Hierarchy / Project 双源添加，列表区分 `[H]` / `[P]`
  - 写入 UV 通道可选（UV0~UV7），目标通道若已有数据会显示 ⚠ 警告
  - 烘焙结果输出到 `xxx_SmoothN.asset`（源 Mesh 同目录），永远不修改原始 `.asset` / FBX
  - `[H]` 烘焙后自动替换场景对象 `MeshFilter` / `SkinnedMeshRenderer` 引用
  - **↺ 用原始资源替换**：撤销烘焙，把场景里的 `mesh_SmoothN` 引用还原回原始 `mesh`
    - 仅改场景 / Prefab 引用，不动 `.asset`
    - 旁边的「删除原始资源」勾选后会顺带删除被替换掉的 `_SmoothN.asset`（带列表内引用安全检查）
- **算法保留各通道维度**：用 `Mesh.GetVertexAttributeDimension` 查询其它非目标通道的实际维度（2D/3D/4D）后原样写回，不会把 Bent Normal 等 3D/4D 数据降级
- **使用方法**：
  1. 在 Hierarchy 选中角色根节点（会递归收集所有 `MeshFilter` / `SkinnedMeshRenderer`）
  2. 打开 `nTools/美术工具/Outline平滑法线烘焙`
  3. 默认目标 UV3，直接点 "▶ 烘焙选中的 N 个 Mesh 到 UV3"
  4. 完成后场景对象的 Mesh 引用会自动指向新生成的 `_SmoothN.asset`

> 详细文档见 `Assets/Tools/ToolsReadme.md` 中"Outline 平滑法线烘焙"小节。

### ShaderGUI

三个 Shader 各有对应的自定义 ShaderGUI（继承 URP `BaseShaderGUI`），提供分组折叠面板。

**已知修复**：
- Play Mode 切换时 Inspector 点击无响应的问题：通过检测 `EditorApplication.isPlaying` 状态变化，强制重新初始化 GUI（重置 `m_FirstTimeApply`）

## TODO / 已知问题

### 前发投影（HairShadowRenderFeature）

- [ ] **马尾穿透前发投影**：当前 `HairShadowMask` Pass 不区分头发部位，
  把所有进 `hairLayerMask` 的头发都画成白色 mask，导致脸后面的马尾也被算
  作"前发遮挡"，从摄像机视角看脸上会出现马尾形状的阴影"穿透"。
  - 可能方案 A：让 mask Pass 写入深度并做 depth test，只保留比脸近的发束
  - 可能方案 B：在 Hair shader 上加一个 `_IsFrontHair` 属性，mask Pass 用
    `clip()` 把非前发部位剔掉（需要美术在材质上手动标记）
  - 可能方案 C：把前发拆成单独的 SubMesh / 单独 GameObject，只让前发参与 mask

### Hair Shader

- [ ] **高光采样 UV1 但模型缺 UV1**：`PBRToonHair.shader` 的各向异性头发高光
  纹理 (`HairSpecTex`) 默认从 UV1 (TEXCOORD1) 采样，目前角色模型尚未烘焙
  UV1，开启高光会得到错误结果或 fallback 全黑。
  - 短期：在 Hair shader 里加 `_USE_UV0_FOR_SPEC` 之类的 fallback keyword
  - 长期：由美术补 UV1，约定写入"头发流向贴图（HairSpecTex）"专用 UV
  - 也可考虑改用程序化噪声/渐变代替 HairSpecTex（不依赖 UV）

### 其他待补

- [ ] Face / Hair 暂未独立 Outline Pass（Hair 通过 `UsePass` 复用 Base 的
  Outline，但 UV3 平滑法线如果只烘了身体没烘头发，描边会回退到 Tangent 方向）
- [ ] PBRToonSkin 目前是基于视角 (Fresnel) 的轻量级假 SSS，没有真正的
  Pre-integrated Skin Shading / 透光 / 厚度图支持，特写镜头的耳廓透红效果
  仍较弱
