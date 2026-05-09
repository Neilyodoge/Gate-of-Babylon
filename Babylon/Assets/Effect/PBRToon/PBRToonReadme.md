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
  - Shadow Ramp（可选）
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

| UV 通道 | 语义 | 用途 | 写入工具 |
|---------|------|------|---------|
| UV0 (TEXCOORD0) | 主纹理坐标 | 基础贴图、PBR Mask、法线贴图等 | DCC 软件 |
| UV1 (TEXCOORD1) | Lightmap / 自定义 | Face Lightmap (SDF)、Hair SpecTex | DCC 软件 |
| **UV2 (TEXCOORD2)** | **Bent Normal** | Visibility Cone 遮蔽数据 (Vector4) | Bent Normal Baker |
| **UV3 (TEXCOORD3)** | **平滑法线** | PBRToon 描边用平滑法线 (.xy 2通道) | 平滑法线烘焙工具 |

> ⚠️ UV2 和 UV3 的用途已固定分配，两个工具不会互相冲突。

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
- **平滑法线来源**：固定从 UV3 (TEXCOORD3).xy 解码（2 通道编码，z 由勾股定理重建）
- **烘焙工具**：`Tools > ArtTools > 平滑法线烘焙工具`
- **RenderFeature**：需要在 URP Renderer 中添加 `ToonOutlineRenderFeature`
- **兼容性**：如未烘焙平滑法线（UV3 为空），会自动回退到 Tangent 方向

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
