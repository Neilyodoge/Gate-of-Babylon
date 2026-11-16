# 游戏 UI 视觉

用于 HUD、菜单、图标、字体、提示、世界空间界面及其动效的**视觉语言**。若问题主要是任务流程、按钮位置或交互逻辑，结合 UX 证据处理；美术方向仍需说明信息怎样在游戏画面中被看见、辨认和理解。

## 接手资料

先看实际游戏截图或录屏、界面的全部状态、目标分辨率/设备、输入方式、语言和项目美术规范。缺少游戏背景时，至少把 UI 放到有代表性的明暗与复杂背景上；孤立的 Figma/PSD 画板不足以证明游戏内可读。

评作品集 UI 画板，先划清游戏画面与展示编排：板外标题、品牌字、样本底色及并列组件不算实机 HUD，也不证明这些组件同时出现。[Ed Streltsov《S.T.A.L.K.E.R. 2》](https://www.artstation.com/artwork/rlWbo2)的 PDA JOURNAL 外标题／元件板与 [Kade O’Casey《Borderlands 4》](https://www.artstation.com/artwork/dy5VZJ)的 UI ART／状态样本提供了静态对照；这类板只支持组件语法比较，运行位置、触发与读速仍需目标尺寸的发行版连续画面。

## 视觉系统

- **层级与焦点**：区分持续存在、按需出现和紧急打断的信息。用尺寸、明度、位置、对比、留白与动效建立读取顺序，不让装饰框或稀有度光效压过当前操作。
- **图标语法**：规定抽象层级、轮廓/填充、笔画重量、内部空隙、视角、光源与色彩用途。拿最容易混淆的一组图标在实际显示尺寸和相同背景上并排比较，检查形状差异能否独立于颜色成立。
- **字体与数字**：在目标语言检查字形、字重、行距、数字宽度、标点与本地化后长度；CJK 文本不要沿用只验证过英文的字号与排版。长文本、极短标签、倒计时和快速变化数值分别试。项目若允许放大字体、图标或 HUD，先核清各选项覆盖的组件和可同时启用的组合；文字、字幕、战斗 HUD 与准星可能受不同设置控制。若有覆盖范围明确的整体档位，分别检查默认档与最大档；若组件独立调节，记录目标画面实际会同屏出现的配置组合。复查文字重排、控件间距、按键提示、战斗信息及背景变化；不能只把默认稿等比放大，也不能用一个组件的放大图代替整屏验收。
- **状态与运动**：列出默认、聚焦、选中、禁用、冷却、警告、完成等实际需要的状态。动效要交代状态变化和响应时机；静态稿无法证明动态信息清楚。事件密集时，分别记录持续 HUD、对白／字幕、短时通知和需关闭的教学面板的触发、驻留与退出；用连续实机画面核它们是同屏、遮挡还是排队，别从单张截图推队列、延迟或玩家读速。若据公开视频判断某组设置在战斗中生效，先核设置值、构建与设置→战斗的连续性；剪辑教程只能分别证明所见菜单和战斗状态。创作者叠加的字幕、警示条不能计入游戏原生 UI。
- **方案与状态分开**：同页多幅颜色、明度、尺寸或外框变体，先辨同件造型探索、同族不同功能图标与已标注的运行状态；图序、相似版位或亮暗变化本身不证明选中／禁用状态或最终入选。Ryan Klaverweide 的[《Skull and Bones》早期 UI 稿](https://www.artstation.com/artwork/8bqWAm)中金／白字形与 FREE CURSOR 双稿属于探索，作者亦称发行版仅留少量影子；Daniel Gagne 的[《DOOM: The Dark Ages》线框稿](https://www.artstation.com/artwork/0aY4ew)展示多种弹药／士气条方案，[结果通知图](https://www.artstation.com/artwork/6vnNZ6)才明确标出 COMPLETE／FAILED。即使状态有文字，触发、驻留和玩家读速仍需目标尺寸与连续操作画面验证。
- **与世界的关系**：HUD 可借用场景的形、材质或时代感，但首先要清楚。世界空间 UI 还须查镜头距离、透视、遮挡、深度冲突和玩家移动时的跟随行为。

## 评审与交付

在目标设备上检查实尺寸、边缘安全区、最拥挤画面、不同亮度场景、控制器或触屏焦点，以及信息激增时的遮挡。颜色是辅助线索；关键状态应有形、文字、图案或运动的冗余表达。若声称无障碍合格，须按项目适用标准和实际玩家测试核实。

交付可是一页 UI 视觉规则与少量关键组件：颜色/字体/边框/图标的用途、必要状态、动效意图和反例；资产化时再给切图、九宫格、缩放与引擎实施条件。评审意见应指出具体屏幕、状态和冲突，而不是笼统要求“更有游戏感”。

上述缩放检查来自两款已发行游戏的不同证据：[Santa Monica Studio 的 Ragnarök 官方默认/XX-Large 与图标大小对照](https://blog.playstation.com/2022/05/19/god-of-war-ragnarok-accessibility-features-revealed/)可直接看到文本重排和图标放大；[Zach Bohn 的制作访谈](https://80.lv/articles/a-deep-dive-into-god-of-war-ragnar-k-s-user-interface)解释其动态布局；[Naughty Dog 的《最后生还者 第二部》官方说明](https://blog.playstation.com/2020/06/09/the-last-of-us-part-ii-accessibility-features-detailed/)列出独立的 HUD 尺寸、背景和颜色选项，未展示同机位布局前后图。另[Firewalk 的《Concord》官方说明](https://blog.playstation.com/2024/08/15/concord-accessibility-settings-detailed/)配有近同机位 HUD 缩放图：技能图标、生命条和弹药信息变大，原文明确准星不随 HUD Scale 缩放；图中未标具体档位。这些案例支持把缩放范围与实际画面都列为复核项，不证明某一档位已通过玩家可读性测试。

[Square Enix 的 FFXIV 官方 HUD 博客](https://na.finalfantasyxiv.com/blog/002170.html)以目标读条 200%、HP 条 100%、状态图标 140% 的同屏画面示例说明组件可混合倍率；其 [Pop-up Text Size 指南](https://na.finalfantasyxiv.com/uiguide/battle/battle-ftpt/battle_popuptext_size.html)另将 Flying Text Size 设为独立选项。[BioWare 的 Veilguard 官方说明](https://www.ea.com/games/dragon-age/dragon-age-the-veilguard/news/accessibility-spotlight)把 HUD 显隐、迷你地图位置、战斗文字大小及字幕设置分开。这些是配置与示例画面的证据，没有验证多项同时放大时的玩家读速。

这一时间轴检查参考 [Zach Bohn 的《Ragnarök》UI 访谈](https://80.lv/articles/a-deep-dive-into-god-of-war-ragnar-k-s-user-interface)：他只说区域、任务／日志和拾取等指定通知共用队列，可能积压；[《Ragnarök》公开游玩片段](https://www.youtube.com/watch?v=h8cLQJ2KYbs)中区域牌、对白字幕和后续地图提示呈不同出入场；[《FFXIV》Alexandria 公开视频](https://www.youtube.com/watch?v=Hm4olPMmleo)可见系统对白、倒计时与持续 HUD 同屏；[《Veilguard》公开游玩片段](https://www.youtube.com/watch?v=8GidSzhaWjo)可见区域牌、字幕、HUD 与需关闭的教学卡。公开视频的 UI 档位和构建未核，这些例子支持按消息类别分别检查，不能证明通用队列、暂停机制或玩家理解效果。

公开教程的证据边界也见 [Mama Cat Gamer 的《FFXIV》HUD 教程](https://www.youtube.com/watch?v=QU2BZfJjImQ)（2026-03-31）：设置面板的倍率在原尺寸画面不可辨，非战斗布局与团战间有剪辑，红色 Boss 提示条为创作者覆盖层；[IVIV 的《WoW 11.1》布局教程](https://www.youtube.com/watch?v=5jGmpXFF4hs)（2025-02-19）在编辑、战斗和城镇画面间剪辑；[Gaming with Griff Griffin 的《Veilguard》设置视频](https://www.youtube.com/watch?v=OLtxT0ihaT4)（2024-11-08）在约 5:14–5:23 的静音正常速度连续段可见 Combat Text 设置菜单退出、同一岩地木桩场景及随后的原生字幕与持续 HUD，5:37 抽帧另见战斗浮字。后一段未见明显换场，但构建号、完整通知时序和玩家读速未核。三例说明剪辑与创作者覆盖层会限制可用证据；短段连续画面只能验证局部状态，不能替代完整的受控同配置多事件检查。
