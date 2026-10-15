# TATA / 苔间

这里是 TATA 桌宠卡牌原型的正式制作目录：

`D:\other\Fork\Gate-of-Babylon\嗯之宝库\TATA`

当前玩法基线为 **4.16**：单只桌宠入口、五战卡牌远征、盾牌分支、敌人习惯、醒目的能量栏、攻击反馈和出牌／结束回合前的体力预测，可查看实际内容的牌堆，包含强化辅助牌加盾效果的奖励搭配说明，记住位置的牌组/强化浏览，跟随实际出牌顺序的首次战斗提示，辅助牌的实际恢复/抽牌收获，鼠标与键盘交替查看时的提示恢复，持续显示的当前保盾/反击/减伤效果，敌人行动后可回看的实际反击、保盾与破盾抽牌结果，扣除敌盾后的手牌预计伤害，以及按当前体力和可强化张数提示的营地选择、与旧存档实际效果一致的小物说明。后续制作、素材、设计讨论与验收都在这里继续，不再编辑原来的 Codex 临时项目。

## 先玩起来

双击根目录 `Launch.cmd`，或 `dist/TATA/卡牌试玩.exe`。试玩入口只使用本次会话的内存，退出后重来。

需要保存进度时，打开 `dist/TATA/TataFarmAdventure.exe`。正式玩家进度仍在 `%LOCALAPPDATA%/TataDesktopPet`，移动制作目录不会搬动或清除玩家存档。

对外发包使用 `dist/TATA.zip`，完整解压后再启动。试玩入口只使用同一目录的 TataFarmAdventure.exe，旧版带版本号的文件不会抢占启动。目录和 ZIP 始终代表最新通过验收的版本。

## 目录存放规范

- `src/`：当前构建实际使用的 C# 源码，包含现有回归检查。暂时保持扁平结构，避免拆散大量 partial 类。卡牌核心看 `FarmCards.cs`、`FarmCardsUI.cs`、`FarmCardBranches.cs`、`FarmCardFeedback.cs`、`FarmCardForecast.cs`、`FarmCardDamage.cs`、`FarmCardPiles.cs`、`FarmCardRewardFit.cs`、`FarmCardBrowse.cs`、`FarmCardLesson.cs`、`FarmCardReceipt.cs`、`FarmCardInspect.cs`、`FarmCardStatus.cs`；程序入口和版本号在 `FarmApp.cs`。
- `assets/`：构建依赖的宠物逐帧 PNG 与图标。现有 `frames*` 和 `pet-icons` 目录及文件名参与资源嵌入，不能随意重命名。新增美术按用途建子目录，记录来源，不能只放到发布包中。
- `scripts/`：可重复使用的构建、验证、截图和打包工具。使用项目相对路径，不再写死旧项目路径，也不以版本号复制一份脚本。
- `docs/design/`：正在采用的设计方案。已实现、下一步和候选想法明确区分；更新现有文档，不堆积“最终版2”等副本。
- `docs/reviews/`：最近一次完整 UX 复核、当前专项验收及本次迁移记录。过去的路径和证据编号属于历史记录，不能当作当前入口。
- `docs/GAMEPLAY.md`、`docs/试玩说明.md`：当前玩法与玩家操作说明；根 README 只负责项目入口、目录和制作流程。
- `docs/RELEASE_CHECKS.md`：发布规范。`docs/releases.json` 只保存复核计数等必要元数据，即使删除旧包，也能保持每三次发布做完整 UX 复核的约定。
- `artifacts/build/`：编译输出和响应文件。后台测试的隔离存档、截图、日志生成在其 `farm-checks/` 下；`artifacts/latest-check.json` 指向最近一次完整通过的验证。
- `artifacts/imported-verification/`：迁移前 4.2 的文本验收依据，不含测试存档。`artifacts/migration-source-map.json` 用于校对迁移内容。
- `dist/TATA/` 和 `dist/TATA.zip`：唯一的最新交付目录与压缩包。不要建立 `release-v*`，不要保留旧 EXE、旧 ZIP 或复制历史发布目录。

`artifacts/`、`dist/` 是可重建产物，默认不进入 Git。源码、素材、脚本、设计与复核文档必须留在各自目录；不要把临时截图、测试存档或探针程序散落在根目录。清理只针对本项目已确认的产物路径，不删除正式玩家进度。

## 制作流程

需要 Windows、.NET Framework 4.x 的 WPF 与 C# 编译器。生成验收总览另需 Python 3 + Pillow、Node.js；它们不参与游戏运行。

在本目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-Checks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package.ps1
```

`Run-Checks.ps1` 默认先构建。刚构建过时可加 `-SkipBuild`。Python 或 Node 不在 PATH 时，用 `-PythonPath` 和 `-NodePath` 传入解释器完整路径；脚本不依赖 Codex 的安装位置。

验证会检查完整规则、正常/旧存档、取消和重复输入、三档字号、后台动作帧、逐阶段重开和合法完整对局，并生成画面总览。脚本全部成功后才更新最新验证指针。打包先生成候选目录、核对启动器与 ZIP 每个文件，再替换最新交付。

修改版本时同步 `src/FarmApp.cs` 和 `src/GuideTestLauncher.cs`，然后更新玩法说明与本轮验收。无需在文件名、脚本名或交付目录名里增加版本号。打包工具会读取版本号，并通过发布登记表判断是否需要完整 UX 复核。

## 持续遵循的产品约定

一次只教一个操作，新概念遇到时再解释；可选内容收起。能量、进度、战绩使用真实状态。浏览和取消不花材料；出牌、消费、换人、种植、进化和出发均要明确操作。默认单只桌宠，不重复展示。回合制冒险不会因用户离开而自行推进。

自动验证只使用分离后台 WPF、内存试玩和隔离测试存档，不打开可见游戏窗口、托盘或全局快捷键，不读写正式玩家进度。后台检查与实机输入、流畅度、真人理解和耐玩性分开记录。

当前优先继续根据实际试玩改清晰度和反馈；能量储存、新角色、路线专属奖励仍是设计候选。已按用户“一直推进”的要求恢复每 30 分钟自动接续；只在实质交付、失败或需要用户决定时通知。
