# TATA 发布规范

本项目自 2026-09-29 迁入当前目录；用户已明确不保留旧版程序和包。此前“保留全部历史发布目录”的约定被这一要求替代。最新交付只使用 `dist/TATA` 与 `dist/TATA.zip`。

## 每次交付

1. 在 `src/` 修改当前源码，资源放 `assets/`，设计与说明放 `docs/`。
2. 运行 Build / Run-Checks。规则、正常与旧存档、取消和重复操作、三档字号最小视口均须通过。窗口、托盘、全局快捷键与正式玩家存档不得用于自动测试。
3. 核对最终构建实际绘制的总览、动作帧、灰度截图与合法对局结算。不要用构造状态冒充实际通关或真人理解证据。
4. 更新本次验收文档。若需要完整 UX 复核，读取 `C:/Users/admin/.codex/skills/game-ux-review/SKILL.md`，覆盖八项并先修复阻断问题。
5. 运行 Package；它核对最终程序哈希、必要报告、启动器和 ZIP 全部文件，不允许包含任何玩家或测试存档。
6. 验证成功后只保留最新交付。旧的临时测试目录可以清理，但不得删除 `artifacts/latest-check.json` 指向的当前证据，或为了省空间提前删除尚未核对的迁移源文件。

4.11 专项验收见 `docs/reviews/turn-receipt-check-v4.11.md`。打包须核对 cards-turn-receipt-regression.txt、三档回合结算总览与灰度图，包含真实反击、保盾、额外/总抽牌、牌不足、休息、旧规则、浏览返回和重复操作。

4.12 专项验收见 `docs/reviews/damage-check-v4.12.md`。打包须核对 cards-damage-regression.txt、三档伤害总览及灰度图，包含敌盾全挡、两段中途缩壳、即时刷新、致命上限、能量不足与隔离存档。4.12 是 4.10 后第 2 次专项发布，下一次新发布须完整复核。

4.13 为本轮完整 game-ux-review，见 `docs/reviews/ux-review-v4.13.md`。打包须核对 cards-camp-regression.txt、三档营地总览与灰度图，覆盖满血、缺血、实际可强化张数、全部强化、取消、重复和正常/旧存档。

4.14 为 4.13 后第 1 次专项发布，见 `docs/reviews/relic-check-v4.14.md`。打包须核对 cards-relic-regression.txt、三档小物总览及灰度图，覆盖无小物、树叶帽、遗留电花加成、首个及后续险路奖励、取消/重复/过期按钮和隔离存档。

4.15 是 4.13 后第 2 次专项发布，见 `docs/reviews/launcher-check-v4.15.md`。打包须核对 launcher-regression.txt 的六种隔离入口情况；下一次实际新发布须完整 game-ux-review。

4.16 完成八项完整 game-ux-review，见 `docs/reviews/ux-review-v4.16.md`。打包须核对 cards-upgraded-fit-regression.txt 及三档强化搭配总览、灰度图；覆盖全部 30 种牌在新旧规则下的真实加盾对照、取消/重复/过期控件、正常/旧存档。

## 完整 UX 复核计数

本轮 4.16 完整复核通过并打包后，发布登记表更新最近完整复核为 4.16；后续第三次实际新发布再次完整复核。每三次实际发布重新完整复核；核心主流程大改或严重回退时提前复核。

计数依据 `docs/releases.json`，不再根据磁盘上有多少 `release-v*` 目录推断。重打当前版本不会重复计数。完整报告仍应写明 Version、EvidenceRun、Status: COMPLETE、OpenBlockingIssues: 0，并匹配本次最终证据。

当前 4.10 完整复核见 `docs/reviews/ux-review-v4.10.md`，打包须核对 cards-status-regression.txt 与三档当前效果总览，覆盖触发、到期、敌人休息、旧规则和同时生效。

4.9 是 4.7 完整复核后的第 2 次专项发布，见 `docs/reviews/inspect-check-v4.9.md`；下一次新发布须完整 game-ux-review。打包须核对 cards-inspect-regression.txt 与三档查看总览，覆盖鼠标/键盘来源交替和过期事件。

4.8 是 4.7 完整复核后的第 1 次专项发布，见 `docs/reviews/receipt-check-v4.8.md`。打包须核对 cards-receipt-regression.txt 和三档实际收获总览，包含满血、抽牌为空/受限、多项效果和额外能量。

4.7 完整复核见 `docs/reviews/ux-review-v4.7.md`。打包须核对 cards-lesson-regression.txt、实际提示文本样本与三档引导总览，检查绕开推荐顺序、零费牌、旧提示、返回和重开的行为。

4.6 的专项验收为 `docs/reviews/browse-check-v4.6.md`，打包必须核对 cards-browse-regression.txt 和三档浏览总览。自动接续已恢复，每轮仍须读取最新登记表，不使用旧版目录。

4.5 的专项验收为 `docs/reviews/reward-fit-check-v4.5.md`。打包必须核对 cards-reward-fit-regression.txt；三档奖励搭配和灰度总览随包提供。

4.4 的完整复核为 `docs/reviews/ux-review-v4.4.md`，牌堆、键盘翻页和只读状态验证见 cards-piles-regression.txt。打包必须核对该报告，三档牌堆与完整流程总览随包提供。

4.3 的专项验收为 `docs/reviews/forecast-check-v4.3.md`，覆盖 225 组预测与实际动作对照、预览不变更存档及三档字号。打包必须核对 cards-forecast-rules.txt 和 cards-forecast-ui.txt。

4.2 的功能验收依据保留在 `docs/reviews/feedback-check-v4.2.md`。迁移后的新构建和验证以 `artifacts/latest-check.json` 与迁移验收文档为准。
