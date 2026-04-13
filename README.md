# Gate of Babylon

Unity URP (2022.3) 个人学习项目，包含两大部分：**Roguelike ARPG 游戏《仙途梦境》** 和 **渲染技术学习实践**。

- **Unity 版本**：2022.3.62f3c1
- **渲染管线**：Universal Render Pipeline (URP)
- **URP 源码**：项目内嵌 URP / SRP Core 源码（含自定义修改，位于 `Packages/` 目录）

---

## 项目结构总览

```
Babylon/
├── Assets/
│   ├── 1Game/                  # 🎮 游戏《仙途梦境》（代码 & 资源）
│   ├── PostProcess/            # 🖼️ 后处理 RenderFeature（NPR Diffusion / Toon Outline）
│   ├── Effect/                 # 🎨 渲染效果（PBRToon 卡通渲染 / VFX Water）
│   ├── Shader/                 # ✏️ 自定义 Shader（Lit_BentNormal）
│   └── Tools/                  # 🛠️ Editor 工具集（美术 / TA / 性能优化）
├── Packages/
│   ├── com.unity.render-pipelines.universal/   # URP 源码（含 Bloom & Tonemapping 扩展）
│   └── com.unity.render-pipelines.core/        # SRP Core 源码
└── ProjectSettings/
```

---

## 🎮 Part 1：游戏《仙途梦境》

> **类型**：Roguelike Top-down 3D ARPG
> **代码目录**：`Assets/1Game/`

纯代码驱动的 Roguelike 游戏 Demo，所有场景、UI、敌人均在运行时动态生成。

**核心系统**：玩家（三段连招 / 闪避无敌帧）、战斗（属性分层 / 暴击 / 灼烧 DoT / 顿帧）、灵物（数据驱动 / 叠加 / 质变）、功法技能（范围 / 投射物 / 位移 / 增益）、Synergy 联动、5 种敌人 AI、房间系统（战斗 / 商店 / 休息 / 宝箱 / Boss）、6 层境界推进、完整 UI 系统、运行时 Debug 控制台。

📖 **详细文档**：[仙途梦境文档索引](Babylon/Assets/1Game/Docs/README.md)

---

## 🎨 Part 2：渲染学习

渲染技术的学习与实践，涵盖后处理、卡通渲染、Shader 开发和 Editor 工具。

### 后处理扩展（URP 管线源码修改）

对 URP 内置后处理模块的源码级扩展，直接修改 `Packages/` 中的 URP 源码。

| 功能 | 说明 |
|------|------|
| **Bloom 扩展** | 新增 nBloom 模式：Kawase 模糊、二次阈值函数、Kill Fireflies（Karis Average） |
| **Tonemapping 扩展** | 新增 GT / ACESSimple / UE4 三种色调映射算法 |

📖 **详细文档**：[PostProcess_README.md](Babylon/Packages/com.unity.render-pipelines.universal/PostProcess_README.md)

### 后处理效果（RenderFeature 实现）

以独立 RenderFeature 形式实现的后处理效果，位于 `Assets/PostProcess/`。

| 模块 | 说明 |
|------|------|
| **NPR Diffusion** | NPR 风格扩散效果，基于亮度阈值提取 + Kawase 模糊实现光晕扩散 |
| **Toon Outline** | 卡通描边 RenderFeature，基于背面法线外扩描边，配合 PBRToon 使用 |

### PBRToon 卡通渲染

从 DanbaidongRP 移植的 PBR 卡通渲染系统，含 Base / Face / Hair 三套 Shader，支持角色 Atlas 阴影、自定义 PCF/PCSS 阴影滤波、Shadow Ramp、描边系统。

📖 **详细文档**：[PBRToonReadme.md](Babylon/Assets/Effect/PBRToon/PBRToonReadme.md)

### 其他渲染效果

| 模块 | 说明 |
|------|------|
| **VFX Water** | 特效水体着色器，支持双层法线混合、水晶通透 SSS、Matcap 反射 |
| **Lit_BentNormal** | 基于 URP Lit 的 Bent Normal 扩展 Shader，数据存储在 Mesh UV2 |

### Editor 工具集

自定义 Editor 工具，统一通过 Unity 菜单栏 **nTools** 访问。涵盖美术工具（批量重命名 / 贴图规范化 / SDF 生成 / 平滑法线烘焙 / Bent Normal 烘焙）、TA 工具（通道重映射 / 贴图调试）、性能优化工具（场景检查）。

📖 **详细文档**：[ToolsReadme.md](Babylon/Assets/Tools/ToolsReadme.md)
