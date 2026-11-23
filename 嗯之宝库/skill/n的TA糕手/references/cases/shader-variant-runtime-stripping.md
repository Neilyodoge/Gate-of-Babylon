# Unity Player 丢 Shader 变体：以出错的 pass 和关键字为准

**要解决的事**：编辑器正常、Player 少了某个效果，打开 Keep All 或 Always Included 仍有缺变体日志。先取证是哪组关键字、哪个 pass/stage 请求失败；构建保留设置和变体总数都不足以证明具体组合进了 Player。

## 公开案例：运行时过程化实例化

[Unity Issue Tracker · Shader variants are stripped when they are not used during Player building](https://issuetracker.unity.com/issues/21036/shader-variants-are-stripped-when-they-are-not-used-during-player-building)，核对于 2026-11-23，当时状态为 Open。

- 报告的复现是在附带项目 Build and Run 后读 `Player.log`。日志点名 `Repro/ProceduralInstancingVariantRepro`、subshader 0、pass 0 的 vertex 和 pixel stage，均缺 `PROCEDURAL_INSTANCING_ON`。报告注明此变体在运行时请求、构建时未被要求。
- 报告注明在 `Instancing Variants = Keep All` 以及 Shader 被加入 `Always Included Shaders` 的配置中也可复现。这是该复现中的保留边界，不能推广成这些设置在所有 Unity 版本都无效。
- 报告列出的可复现环境是 Windows 11；Unity 版本包括 6000.0.0f1、6000.0.77f1、6000.3.18f1、6000.4.12f1、6000.5.0f1、6000.6.0a7。没有测试其他环境。本轮只读了报告和日志摘录，未运行附件；不能据此确认项目用的具体 Shader 声明、请求它的 API、变体被哪一阶段剔除，或有无修复版本。

## 怎样定位自己的构建

1. **先固定失败证据**。保留 Unity/管线包版本、图形 API、质量档、目标平台和触发渲染操作。用 [Unity 6000.0 · strictShaderVariantMatching](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/playersettings/strictshadervariantmatching#remarks) 做诊断 Player：开启后要求已启用的 local keyword 与 global override 匹配，找不到会使用 error shader 并报错；关闭时 Unity 可能静默选最接近的组合。关注日志中的 Shader、pass、stage 和完整关键字，不凭“没有粉色”判定成功。
2. **再定位构建阶段**。按照 [Unity 6000.0 · Check how many shader variants you have](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-variants/shader-how-many-variants)，在 `Editor.log` 里找对应 `Compiling shader` 的 pass 和 stage，抄 `After settings filtering`、`After built-in stripping` 和 `After scriptable stripping`。文档示例的计数是 8 → 8 → 4 → 4，说明这例数量在内建阶段下降；它不是上面实例化案例的实测数据，也不能仅靠数量推出缺的是哪一个组合。需追精确组合时，在自己的构建中针对这一 Shader/pass 加观察日志，检查实际加载的 Player 或内容包，确认不是在别的构建产物里找变体。
3. **最后按原因做小范围对照**。[Unity 6000.0 · Strip shader variants](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-variants/shader-variant-stripping) 提醒避免用 `shader_feature` 切换只在运行时才出现的分支，也提供自定义构建剔除入口。普通自定义关键字若只在运行时被启用，先查构建时材质是否确实需要它；可对那一组比较显式保留或窄范围 `multi_compile`，同时确认项目/管线剔除规则。有全局保留仍缺程序化实例化变体时，先用最小工程排查版本相关行为；不把换普通关键字声明当成这条公开问题的既定修法。

## 最小验收

同一项目做两份 Player：A 维持原构建，B 只调整指向该组合的一个保留条件。两份都执行同一运行时操作并开启严格匹配，比较该 pass/stage 的错误、目标画面和构建计数。只要目标效果仍回退，或 B 只增加了其他 pass 的变体，总数上升也不能算修复。验收后另跑正常发布配置，记录变体数和构建大小的代价。

**状态**：公开 Player 日志和 Unity 文档已核对；排查与 A/B 是候选流程，未本地 Build and Run。
