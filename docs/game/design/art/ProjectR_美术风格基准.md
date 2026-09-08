# ProjectR 美术风格基准

> 状态：场景背景与三宠方向已确认；主角全部旧设定与候选已撤回，等待重新设计  
> 基准日期：2026-09-04  
> UI追加：2026-09-08  
> 用途：后续场景概念图、角色立绘、灵宠图与UI插画生成时作为共同提示词前缀
> 世界依据：[ProjectR 世界设定](../设定_ProjectR世界与主角.md)

![场景背景基准](ProjectR_场景美术风格基准.png)

## 三宠美术基准

![三宠美术基准](ProjectR_三宠美术基准.png)

- 共用粗黑手绘轮廓、块面平涂、轻微不对称与“憨怪但友善”的表情语言。
- 火花狸采用干燥焦毛、尖锐耳尾和暖色火焰，动作倾向前冲。
- 回声鸮采用哑光羽壳、圆眼与延迟轮廓／破环，动作倾向悬停。
- 弹弹胶采用透明叠色、湿润高光、内部弹簧和气泡，必须展示压扁与拉伸。
- 三宠保持同一世界观，但不得复用相同眼型、躯干结构和材质表现。

## 核心方向

- 二维动画截图感，不做写实3D、厚涂绘本或高密度奇幻概念图。
- 角色使用清晰深色线稿、平涂色块和少量赛璐璐阴影。
- 背景使用更松的手绘笔触、大块色面和清晰明暗分组，细节弱于角色。
- 自然环境优先；建筑只作为被植物覆盖的简化木石遗迹，不使用固定地域的传统建筑符号。
- 主色为苔藓绿、青绿、木褐和暖灰，以黄绿色斜向阳光及少量金色灵光建立焦点。
- 画面保留安静留白与明确行动路径，不堆满瓶罐、装饰、碎叶和微小纹理。

## 主角（待重新设计）

主角的种族、背景、身份、性格、能力来源、比例、服装和视觉钩子当前全部未定。V1～V8生成图仅作为被否决的探索草稿保留，不在本文件引用，也不能作为后续生图、建模或叙事依据。

主角重新设计前，不从“见习寻路者”“路生民”“三孔护腕”“折叠路径标片”或任何既有候选继续细化。后续应先确认人物背景，再单独建立角色提示词与美术基准。

## 天赋树UI画风

- **当前唯一美术权威**：`Assets/1Game/ArtRes/UI/ProjectR/References/SpiritTalentTree_ApprovedArtV1.png`。后续天赋树资源、布局和实机验收均直接与此图对照。
- `SpiritTalentTree_BaseV1.png`、`SpiritTalentTree_RoundCuteV2.png`及此前实机截图只保留为过程稿，不再覆盖权威图。
- 必须保留权威图的大幅左下灵宠插画、左侧三宠纵向页签、`3→6→6→3`疏密节奏、右侧独立详情卡、顶部成长信息和底部图例；不得只提取配色后套回旧12节点网格。
- 普通节点使用饱满双圆环，关键节点使用短尖、圆角的四向十字星，终极节点使用同源的放大环星，不为每个效果发明一种外轮廓。
- 灵宠可提高头脸与尾巴的圆润比例，但不得变成幼儿化吉祥物；保持动作倾向和物种剪影。
- 暖杏橙、柔和鼠尾草绿与灰青色区分路线，深可可色承担文字和轮廓；减少脏污、尖锐笔触和深黑压迫感。
- 《暗喻幻想》只参考非对称排版、选中态动势和插画破框原则，不复制其高噪声拼贴、字体或具体资产。

## 通用生成提示词

```text
An original ProjectR fantasy scene rendered like a clean high-quality 2D animated film frame.
Characters use confident dark line art, simple rounded silhouettes, flat cel-shaded color blocks,
minimal facial features and only one restrained shadow tone. Do not infer or generate the protagonist
from this shared environment prompt; use a separate approved character brief once one exists.

The background is a loose hand-painted 2D animation background with broad visible brush blocks,
simplified planar moss, timber and stone shapes, restrained detail, slightly rough painted edges,
clear value grouping and a strong diagonal yellow-green sunlight shape. Nature dominates and
reclaims a few simple wooden or stone ruins. Use moss green, turquoise, muted brown, warm gray and
small golden spirit-light accents. Keep open negative space and a readable path through the scene.

Friendly spirits use tiny rounded animation shapes with stable silhouettes and simple expressions.
Original world and character designs, gentle natural fantasy, calm wonder with a small mystery.
```

## 通用负向提示词

```text
no photorealism, no realistic 3D render, no painterly storybook micro-detail, no dense concept-art
clutter, no ornate historical architecture, no pagodas, no talismans, no calligraphy, no fixed
real-world cultural costume, no realistic adult anatomy, no long limbs, no detailed muscles,
no complex belts or layered armor, no heavy cape, no thick realistic hair strands, no soft
oil-painted face, no grim darkness, no oversized mascot body, no UI, no text, no logo
```

## 场景追加模板

在通用提示词后追加具体内容，例如：

```text
Beginner prologue: a reclaimed woodland ruin with broad slate steps and weathered timber platforms.
A root-grown bonding mark sits in the foreground, three simple training dolls stand along the path,
and an ordinary overgrown path leads toward the first secret-realm region between distant trees.
```

后续生成应同时参考本页图片与提示词。若角色和背景发生冲突，以“角色清晰线稿／背景概括笔触”的层级关系为准。
