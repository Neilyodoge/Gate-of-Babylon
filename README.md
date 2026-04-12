# Gate of Babylon

Unity URP (2022.3) 项目，包含 **Roguelike ARPG 游戏《仙途梦境》**、自定义渲染管线扩展、卡通渲染、后处理效果和实用 Editor 工具。

---

## 项目结构

```
Babylon/
├── Assets/
│   ├── 1Game/                      # 🎮 游戏《仙途梦境》核心代码 & 资源
│   │   ├── Scripts/
│   │   │   ├── Core/               #   核心系统（GameManager, GameConfig, Demo1Setup 等）
│   │   │   ├── Player/             #   玩家控制（移动, 战斗, 动画）
│   │   │   ├── Enemy/              #   敌人 AI（基础, 远程, 法师, 冲锋, Boss）
│   │   │   ├── Combat/             #   战斗系统（属性, 伤害, 技能, 投射物, 顿帧）
│   │   │   ├── Items/              #   灵物 & 技能系统（数据驱动, 叠加, 质变, Synergy）
│   │   │   ├── Room/               #   房间系统（战斗, 商店, 休息, 宝箱, 关卡过渡）
│   │   │   ├── UI/                 #   UI 系统（HUD, 技能栏, 背包, 小地图, 伤害数字）
│   │   │   └── Editor/             #   游戏 Editor 工具（数据创建, Debug, 材质修复）
│   │   ├── ArtRes/                 #   美术资源（角色, 怪物, 特效模型）
│   │   └── Data/                   #   ScriptableObject 数据资产
│   ├── PostProcess/                # 后处理效果（RenderFeature 实现）
│   │   ├── NPRDiffusion/           #   NPR 扩散效果
│   │   ├── NeilyodogBloom/         #   (已废弃) 旧版 Bloom RenderFeature 备份
│   │   └── ToonOutline/            #   卡通描边 RenderFeature
│   ├── Effect/                     # 渲染效果
│   │   ├── PBRToon/                #   PBR 卡通渲染系统（角色渲染）
│   │   └── VFXWaterMa/             #   特效水体着色器
│   ├── Shader/                     # 自定义 Shader
│   │   ├── Lit_BentNormal.*        #   基于 URP Lit 的 Bent Normal 扩展
│   │   └── Editor/                 #   ShaderGUI
│   ├── Tools/                      # Editor 工具集
│   │   └── Editor/                 #   美术工具 / TA 工具 / 性能优化工具
│   ├── Scene/                      # 场景文件
│   └── URPBaseSample/              # URP 基础示例
├── Packages/
│   ├── com.unity.render-pipelines.universal/   # URP 源码（含自定义修改）
│   └── com.unity.render-pipelines.core/        # SRP Core 源码（含自定义修改）
└── ProjectSettings/
```

---

## 模块概览

### 🎮 游戏《仙途梦境》

> **类型**：Roguelike Top-down 3D ARPG
> **代码目录**：`Assets/1Game/`
> **详细文档**：[docs/game/README.md](docs/game/README.md)

纯代码驱动的 Roguelike 游戏 Demo，所有场景、UI、敌人均在运行时动态生成。

| 系统 | 说明 |
|------|------|
| **玩家系统** | WASD 移动 + 鼠标瞄准、三段连招近战、闪避无敌帧、动画状态机 |
| **战斗系统** | 属性分层计算（绝对值+百分比）、暴击、减伤、灼烧 DoT、穿透、击杀回复、顿帧反馈 |
| **灵物系统** | ScriptableObject 数据驱动、5 品阶掉落权重、叠加算法、质变阈值、灵物槽位 |
| **技能系统** | 功法技能（范围/投射物/位移/增益）、纯 CD 模型、技能栏拖拽换位 |
| **Synergy 系统** | 隐藏组合触发、多灵物联动效果 |
| **敌人 AI** | 5 种敌人类型（基础/远程/法师/冲锋/Boss）、追踪/攻击/特殊技能 |
| **房间系统** | 战斗房间（波次）、商店、休息、宝箱、Boss、关卡过渡传送门 |
| **境界推进** | 6 层（练气→筑基→金丹→元婴→化神→渡劫）、难度曲线递增 |
| **UI 系统** | HUD（血条/境界/CD/消息）、技能栏+灵物槽位、背包面板、小地图、伤害飘字、世界空间提示 |
| **Debug 工具** | 运行时 Debug 控制台（Tab 键呼出）：无敌/锁血/秒杀/加速/房间跳转/时间缩放 |

### 🎨 后处理扩展（URP 管线内修改）

对 URP 内置后处理模块的源码级扩展，直接修改 Packages 中的 URP 源码。

| 功能 | 说明 |
|------|------|
| **Bloom 扩展** | 新增 nBloom 模式：Kawase 模糊、二次阈值函数、Kill Fireflies（Karis Average） |
| **Tonemapping 扩展** | 新增 GT / ACESSimple / UE4 三种色调映射算法，共五种可选模式 |

### 🖌️ 后处理效果（RenderFeature 实现）

以独立 RenderFeature 形式实现的后处理效果，位于 `Assets/PostProcess/`。

| 模块 | 说明 |
|------|------|
| **NPR Diffusion** | NPR 风格扩散效果，基于亮度阈值提取 + Kawase 模糊实现光晕扩散 |
| **Toon Outline** | 卡通描边 RenderFeature，配合 PBRToon 使用，基于背面法线外扩描边 |

### 🧑‍🎨 渲染效果

| 模块 | 说明 |
|------|------|
| **PBRToon** | 从 DanbaidongRP 移植的 PBR 卡通渲染系统，含 Base/Face/Hair 三套 Shader、角色 Atlas 阴影、自定义 PCF/PCSS 阴影滤波、Shadow Ramp、描边系统 |
| **VFX Water** | 特效水体着色器，支持双层法线混合、水晶通透 SSS、Matcap 反射 |
| **Lit_BentNormal** | 基于 URP Lit 的 Bent Normal 扩展 Shader，Bent Normal 数据存储在 Mesh UV2 中 |

### 🛠️ Editor 工具集

#### 美术 / TA 工具（菜单栏 nTools）

| 分类 | 工具 | 说明 |
|------|------|------|
| 美术工具 | 批量重命名 | 按序号批量重命名资产 |
| 美术工具 | 贴图规范化 | 根据文件名后缀自动设置 sRGB / Texture Type |
| 美术工具 | SDF Generator | 将贴图通道转换为 SDF 距离场 |
| 美术工具 | 平滑法线烘焙 | 角度加权平滑法线，写入 UV3 供描边使用 |
| 美术工具 | Bent Normal Baker | CPU Raycast 烘焙 Bent Normal，写入 UV2 |
| 美术工具 | Prefab 资源快速复制 | 提取 Prefab 引用的贴图/模型/材质到指定目录 |
| TA 工具 | 通道重映射 | 重新排列贴图 RGBA 通道，支持反转 |
| TA 工具 | 贴图调试 Shader | 可视化查看贴图通道、顶点色、法线、UV 等 |
| 性能优化 | 场景优化 | 特效/材质/模型面数三维度场景检查 |

#### 游戏 Editor 工具（菜单栏 Tools）

| 工具 | 说明 |
|------|------|
| **Demo1 数据创建器** | 一键生成灵物/功法/GameConfig 等 ScriptableObject 数据 |
| **GameConfig Inspector** | 自定义 Inspector，分组显示游戏配置参数 |
| **修复粉色材质** | 一键将 Built-in Shader 材质转换为 URP 对应 Shader |
| **工具搜索窗口** | 快速搜索并打开项目中所有 Editor 工具 |

---

## 模块文档

| 模块 | 文档链接 |
|------|----------|
| **🎮 游戏《仙途梦境》** | [docs/game/README.md](docs/game/README.md) |
| **Post-Processing（Bloom & Tonemapping 扩展）** | [PostProcess_README.md](Babylon/Packages/com.unity.render-pipelines.universal/PostProcess_README.md) |
| **PBRToon 卡通渲染** | [PBRToonReadme.md](Babylon/Assets/Effect/PBRToon/PBRToonReadme.md) |
| **Editor 工具集** | [ToolsReadme.md](Babylon/Assets/Tools/ToolsReadme.md) |

---

## 环境要求

- **Unity**：2022.3.62f3c1
- **渲染管线**：Universal Render Pipeline (URP)
- **URP 源码**：项目内嵌 URP / SRP Core 源码（含自定义修改，位于 `Packages/` 目录）
