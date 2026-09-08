# 🔧 Debug 与工具说明

> 本文档面向程序员和AI Agent，说明ProjectR的Debug功能、编辑器工具和开发辅助系统。
>
> **注**：当前Unity编辑器菜单统一使用 `ProjectR/...`；旧产品与冻结关卡工具不再暴露菜单入口。
> 最后更新：2026-04-15

---

## 一、运行时 Debug 控制台

### 1.1 打开方式

| 方式 | 操作 |
|------|------|
| **快捷键** | `Tab` 键切换开关 |
| **鼠标** | 点击屏幕左上角 `Debug` 小按钮 |

> 文件：`Core/DebugConsole.cs`

### 1.2 功能列表

#### 【玩家状态】

| 按钮 | 功能 | 实现细节 |
|------|------|---------|
| 🛡 无敌模式 | 不受伤害 | 将 `damageReduction` 设为 1.0（100%减伤） |
| 🔒 锁血模式 | 血量不变 | 每帧将 `currentHp` 恢复到锁定值 |
| ♥ 满血恢复 | 立即满血 | `currentHp = maxHp`，发布 `HealthChanged` 事件 |
| ⚔ 一击必杀 | 秒杀所有敌人 | 将 `attackDamage` 设为 99999 |
| 👟 加速模式 | 3倍移速 | `moveSpeed *= 3` |

#### 【属性调整】

| 按钮 | 功能 | 数值 |
|------|------|------|
| ⚔ 攻击力 +50 | 增加攻击力 | `attackDamage += 50` |
| ♥ 最大生命 +100 | 增加血量上限 | `maxHp += 100`，同时回复100 |
| ✦ 灵力碎片 +500 | 增加货币 | `PlayerResources.AddShards(500)` |
| 💎 爆率拉满 | 100%掉落 | 设置 `GameConfig.debugMaxDropRate = true` |

#### 【房间跳转】

| 按钮 | 功能 | 说明 |
|------|------|------|
| ☠ 清除所有敌人 | 杀死场景中所有敌人 | 查找所有 `Enemy` Tag 对象，调用 `OnDamage(999999)` |
| ✓ 强制通关 | 通关当前房间 | 先清敌，再发布 `RoomCleared` 事件 |
| $ 跳转 → 商店 | 直接进入商店房间 | 调用 `GameManager.DebugGotoRoom()` |
| ⚔ 跳转 → 战斗 | 直接进入战斗房间 | 同上 |
| ☠ 跳转 → Boss | 直接进入Boss房间 | 同上 |
| ♥ 跳转 → 休息 | 直接进入休息房间 | 同上 |
| ★ 跳转 → 宝箱 | 直接进入宝箱房间 | 同上 |

#### 【系统】

| 按钮 | 功能 | 说明 |
|------|------|------|
| ⏱ 切换时间缩放 | 循环切换游戏速度 | 0.25x → 0.5x → 1x → 2x → 4x |
| ↺ 重新开始 | 重新开始整局游戏 | 清空背包/碎片/技能，重新加载场景 |

### 1.3 状态面板

Debug面板顶部实时显示：
- 当前境界和层数
- 生命值 / 攻击力 / 攻速 / 移速 / 碎片
- 各Debug模式的开关状态（ON/OFF）
- 当前时间缩放倍率

### 1.4 日志系统

面板底部显示最近一条操作日志，所有日志同时输出到 Unity Console（`Debug.Log`），带 `[DebugConsole]` 前缀。

### 1.5 爆率拉满的技术细节

`debugMaxDropRate` 使用 **static 字段** 存储（而非实例字段），确保 ScriptableObject 重新加载时不会丢失：

```csharp
// GameConfig.cs
private static bool _debugMaxDropRate = false;
public bool debugMaxDropRate
{
    get => _debugMaxDropRate;
    set => _debugMaxDropRate = value;
}
```

**状态同步机制**：`DebugConsole` 在 `Start()` 中会从 `GameConfig` 同步 `_maxDropRate` 状态，确保场景重新加载后（如点击"重新开始"）面板显示与实际状态一致。

**调试日志**：开启爆率拉满后，会输出调试日志：
- `[DebugConsole] debugMaxDropRate = true` — 确认按钮点击生效
- `[DebugConsole] ✓ GameManager.itemPool 有 N 个灵物` — 确认灵物池已配置
- `[DebugConsole] ⚠ 警告：GameManager.itemPool 为空！` — **这就是爆率拉满失效的原因！**
- `[Drop] xxx possibleDrops为空，跳过掉落` — 说明敌人没有被分配掉落池
- `[Drop] 爆率拉满但未掉落？` — 不应出现，如果出现说明有逻辑bug

> **Legacy说明**：旧灵物池不再提供自动生成菜单；需要回归旧Demo时由 `Demo1Setup`读取现有兼容资产。

**覆盖范围**：
| 掉落来源 | 是否受爆率拉满影响 | 文件 |
|---------|---|---|
| 普通敌人掉灵物 | ✅ | `EnemyBase.TryDropItem()` |
| 普通敌人掉功法 | ✅ | `EnemyBase.TryDropSkill()` |
| 远程/冲锋/法师掉灵物 | ✅ | 各自的 `TryDropItem()` |
| 精英怪掉灵物 | ✅ | `EnemyElite.TryDropItem()` |
| 精英怪掉功法 | ✅ | `EnemyElite.TryDropSkill()` |
| 通关奖励掉灵物 | ✅ | `BattleRoom.SpawnRewards()` |
| 通关奖励掉功法 | ✅ | `BattleRoom.SpawnSkillReward()` |
| 可破坏物掉碎片 | ✅ | `Destructible.OnDestroyed()` |
| 可破坏物掉灵物 | ✅ | `Destructible.TryDropItem()` — 正常概率8%，只掉凡品/灵品 |
| Boss掉灵物 | ⚠️ 不受影响 | `EnemyBoss.TryDropItem()` — Boss本身必定掉3个 |
| 商店商品 | ❌ | 商店固定展示，不受影响 |
| 宝箱 | ❌ | 宝箱固定掉落，不受影响 |

> **注意**：`EnemyMage`、`EnemyCharger`、`EnemyRanged` 只有灵物掉落，没有独立的功法掉落逻辑（功法掉落仅在 `EnemyBase` 和 `EnemyElite` 中实现）。

---

## 二、编辑器菜单工具

### 2.1 菜单入口

ProjectR仍需使用的编辑器工具统一位于 Unity 顶部菜单栏 `ProjectR` 下：

| 菜单项 | 功能 | 文件 |
|--------|------|------|
| 配置/运行参数总控 | 调整当前运行参数 | `ConfigDashboard.cs` |
| 配置/创建游戏配置 | 创建 `GameConfig.asset` | `GameConfigEditor.cs` |
| 数据工具/导入战斗配表 | CSV导入运行时JSON | `CsvToJsonImporter.cs` |
| 开发工具/相机定位器 | 场景相机定位 | `CameraLocatorWindow.cs` |
| 开发工具/运行载体与回路契约测试 | 执行ProjectR契约回归测试 | `ProjectRP0ContractTests.cs` |
| UI/生成中文TMP字体资产 | 创建TMP字体资产 | `TMPFontAssetCreator.cs` |

旧 `Demo1DataCreator`、KayKit生成器和关卡白盒／生成菜单已移除。关卡脚本仅隐藏入口，等待关卡策划解冻后重新评估。

### 2.2 工具栏快速启动

Unity播放控制左侧提供“启动游戏”按钮。编辑状态点击后会先提示保存未保存场景，再切换到 `Assets/1Game/Scenes/Demo1.unity`并进入播放；已经处于播放或播放切换状态时点击不会停止或重启当前游戏。实现位于 `Editor/ProjectRGameLauncher.cs`。

---

## 三、F1 工具搜索面板

### 3.1 打开方式

| 方式 | 操作 |
|------|------|
| **快捷键** | `F1` |
| **菜单** | `nTools → 工具搜索` |

> 文件：`Editor/ToolSearchWindow.cs`

### 3.2 三个Tab页

| Tab | 内容 | 来源 |
|-----|------|------|
| 通用工具 | 美术工具/TA工具/性能优化 | `Assets/Tools/Editor/` |
| 专用工具 | 核心系统/战斗/玩家/敌人/灵物/房间/UI/文档 | `Assets/1Game/Scripts/Editor/` |
| 个人收藏 | 用户收藏的工具 | `EditorPrefs` 持久化 |

### 3.3 搜索功能

输入关键词实时过滤，支持按名称、分类、描述模糊匹配。

### 3.4 常用搜索词

| 搜索词 | 定位到 |
|--------|--------|
| `GameConfig` | 游戏配置 .asset |
| `AudioConfig` | 音效配置 .asset |
| `怪物` | 怪物预制体配置 .asset |
| `火灵珠` / `落石术` | 对应灵物/功法 .asset |
| `配置速查` | 配置速查表文档 |

### 3.5 添加新工具

在 `ToolSearchWindow.cs` 的 `ToolRegistry.BuildToolList()` 中添加：

```csharp
list.Add(new ToolEntry
{
    Name = "工具名称",
    Category = "Core",              // 分类
    OnClick = () => { ... },        // 点击执行
    ScriptPath = "Assets/...",      // 关联脚本（可选）
    Description = "描述",           // 填写后显示?按钮
    IsSpecialized = true            // true=专用工具Tab
});
```

---

## 四、配置面板（ConfigDashboard）

### 4.1 打开方式

通过 F1 工具搜索面板搜索 "配置" 或 "Dashboard"。

> 文件：`Editor/ConfigDashboard.cs`

### 4.2 功能

提供 GameConfig 的可视化编辑界面，分区域显示所有配置项，并附带常用调整提示：

| 提示 | 操作 |
|------|------|
| 让功法更多 | `功法掉落概率` ↑ |
| 增加闪避 | `闪避充能层数` ↑ |
| 调精英怪 | `精英怪出现概率` / `精英怪最低层数` |
| 调可破坏物 | `可破坏物数量` / `可破坏物掉落概率` |
| 调高品质掉率 | 降低 `凡品掉率权重`，提高 `地品/天品掉率权重` |

---

## 五、GameConfig Editor

### 5.1 功能

> 文件：`Editor/GameConfigEditor.cs`

为 `GameConfig` 的 Inspector 添加自定义编辑器，提供更友好的分组显示和Tooltip。

---

## 六、运行时快捷键总览

| 按键 | 功能 | 上下文 |
|------|------|--------|
| `Tab` | 打开/关闭 Debug 控制台 | 任何时候 |
| `WASD` | 移动 | 游戏中 |
| `鼠标` | 瞄准方向 | 游戏中 |
| `鼠标左键` | 近战攻击（三段连招） | 游戏中 |
| `Q` | 技能1 | 游戏中 |
| `E` | 技能2 | 游戏中 |
| `R` | 技能3 | 游戏中 |
| `Space` | 闪避（2层充能） | 游戏中 |
| `F` | 拾取/交互（短按拾取，长按分解） | 靠近掉落物/NPC |
| `Tab` | 打开/关闭灵物背包 | 游戏中（与Debug面板共用） |

---

## 七、常见Debug场景

### 7.1 测试掉落

1. 打开Debug面板（Tab）
2. 点击 "💎 爆率拉满"
3. 进入战斗房间，每个敌人必定掉落灵物和功法

### 7.2 测试Boss

1. 打开Debug面板
2. 点击 "🛡 无敌模式"（可选）
3. 点击 "☠ 跳转 → Boss"

### 7.3 测试商店

1. 打开Debug面板
2. 点击 "✦ 灵力碎片 +500"（多次）
3. 点击 "$ 跳转 → 商店"

### 7.4 快速通关测试

1. 打开Debug面板
2. 点击 "⚔ 一击必杀"
3. 正常游玩，一刀一个

### 7.5 慢动作观察

1. 打开Debug面板
2. 点击 "⏱ 切换时间缩放" 直到显示 0.25x
3. 观察战斗细节（攻击判定、特效、AI行为）

---

## 八、日志规范

### 8.1 颜色编码

项目中的 `Debug.Log` 使用 Unity Rich Text 颜色标记：

| 颜色 | 含义 | 示例 |
|------|------|------|
| `<color=cyan>` | 房间/流程信息 | 房间类型、层级推进 |
| `<color=yellow>` | 战斗/数值信息 | 敌人数量、伤害倍率 |
| `<color=red>` | 危险/Boss信息 | Boss出场、玩家死亡 |
| `<color=green>` | 成功/恢复信息 | 通关、满血 |
| `<color=magenta>` | 系统/Debug信息 | 游戏开始、Debug操作 |
| `<color=#88CCFF>` | 资源信息 | 碎片获得 |

### 8.2 前缀规范

| 前缀 | 来源 |
|------|------|
| `[DebugConsole]` | Debug控制台操作 |
| `[Debug]` | GameManager的Debug接口 |
| `【境界名】` | 房间/关卡相关日志 |
