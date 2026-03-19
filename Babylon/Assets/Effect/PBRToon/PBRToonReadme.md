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
├── PBRToonFaceDirection.cs             # 面部朝向脚本（运行时组件）
└── Shaders/
    ├── PBRToonCommon.hlsl              # 公共工具函数库
    ├── PBRToonBase.shader              # 基础 Shader（身体/衣服等）
    ├── PBRToonFace.shader              # 面部 Shader（SDF 阴影 + 鼻尖高光）
    └── PBRToonHair.shader              # 头发 Shader（各向异性高光）
```

## Shader 列表

### PBRToon/Base
- **路径**：`Universal Render Pipeline/PBRToon/Base`
- **用途**：身体、衣服等通用部位
- **特性**：
  - PBR Mask 贴图（金属度/光滑度/AO/自发光）
  - Normal Map
  - Shadow Ramp（可选）
  - 自定义间接光照（SH + Cubemap）
  - 屏幕空间 Rim Light

### PBRToon/Face
- **路径**：`Universal Render Pipeline/PBRToon/Face`
- **用途**：角色面部
- **特性**：
  - SDF 脸部阴影贴图（Face Lightmap，UV1 采样）
  - 鼻尖高光
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

### 描边系统
- **算法**：原神风格背面法线外扩描边
- **平滑法线来源**：固定从 UV3 (TEXCOORD3).xy 解码（2 通道编码，z 由勾股定理重建）
- **烘焙工具**：`Tools > ArtTools > 平滑法线烘焙工具`
- **RenderFeature**：需要在 URP Renderer 中添加 `ToonOutlineRenderFeature`
- **兼容性**：如未烘焙平滑法线（UV3 为空），会自动回退到 Tangent 方向
