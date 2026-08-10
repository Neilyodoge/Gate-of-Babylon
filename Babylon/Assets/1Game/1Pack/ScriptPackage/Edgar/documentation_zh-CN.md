# Edgar 中文离线文档

> 对应版本：2.0.0  
> 原文：`documentation.pdf`  
> 说明：本译文保留了原文的类名、字段名、菜单名和代码，以便在 Unity 中对照操作。原 PDF 中仅用于说明界面的插图未嵌入本 Markdown，请配合原文相应页面查看。

## 目录

- [离线文档说明](#离线文档说明)
- [快速入门](#快速入门)
- [房间模板](#房间模板)
- [关卡图](#关卡图)
- [生成器设置](#生成器设置)
- [关卡结构与房间数据](#关卡结构与房间数据)
- [性能建议](#性能建议)
- [地牢生成器](#地牢生成器)
- [后处理](#后处理)
- [(PRO) 平台跳跃关卡生成器](#pro-平台跳跃关卡生成器)
- [(PRO) 自定义输入](#pro-自定义输入)
- [常见问题](#常见问题)
- [在编辑器中保留预制体关联](#在编辑器中保留预制体关联)

## 离线文档说明

欢迎阅读 Edgar 的离线文档。

若想获得最佳阅读体验，请使用[在线文档](https://ondrejnepozitek.github.io/Edgar-Unity/docs/introduction/)。为了缩短本文篇幅，所有示例配置和操作指南仅收录于在线版本。

如有任何问题，可通过 `ondra@nepozitek.cz` 联系作者。

## 快速入门

本节介绍生成第一个关卡所需的基础知识，并非完整教程。这里提到的每个主题都有各自的专门页面，其中包含本节未涉及的详细信息。

> 如果你更喜欢视频教程，可以在 YouTube 上观看相关视频。

### 房间模板

房间模板是生成器的核心概念之一。它描述地牢中各个房间的外观，以及房间之间可以如何连接。

一个完整的房间模板中，房间轮廓以黄色高亮显示，可用的门位置以红色显示。

#### 创建房间模板

1. 进入准备保存房间模板预制体的文件夹。
2. 在 Project 窗口中单击右键，选择 `Create → Edgar → Dungeon room template`。
3. 可选：按需重命名预制体文件。

#### 设计房间模板

打开房间模板预制体后，可以看到一个 `Tilemap` 游戏对象，其中包含 `Walls`、`Floor` 等多个瓦片地图层。你可以使用画笔、规则瓦片等所有可用的 Tilemap 工具来设计房间。

> **非常重要：**如果要改变房间模板的结构，例如添加瓦片地图层或碰撞体，请阅读“房间模板自定义”指南。第一次使用 Edgar 时，建议沿用默认结构。

生成器底层需要计算每个房间模板的轮廓，因此房间模板的形状存在一些限制。例如，房间模板不能由两组互不相连的瓦片构成。

#### 门

完成房间模板的视觉设计后，可以添加门。门的位置用于告诉算法不同房间模板之间能够如何连接。最重要的规则是：两扇门的长度必须相同，才能彼此兼容。

门位置通过挂载在房间模板根游戏对象上的 `Doors (Grid2D)` 组件添加。添加方式包括：

- **简单门模式（Simple door mode）**：适用于不太关心门具体位置的情况。只需指定门的长度和边距，算法便会在轮廓上自动加入所有可用的门。
- **手动门模式（Manual door mode）**：需要手动标记每一个门的位置。

> 刚开始使用 Edgar 时，建议尽可能使用简单门模式。如果手动指定的门位置太少，生成器可能无法生成关卡；简单门模式通常能提供足够多的门位置，使生成速度保持在合理水平。

在手动门模式中，先单击门的第一格瓦片，再拖动光标至最后一格瓦片，即可添加一扇门。

#### 走廊

为了连接关卡中的两个普通房间模板，生成器会使用所谓的“走廊房间模板”。它的创建方式与普通房间模板相同，区别在于走廊通常恰好只有两个门位置。

生成器实际上也支持门不在相对两侧或拥有两个以上门位置的走廊，但出于性能考虑，不建议这样设计。推荐使用狭窄的直线走廊。

### 关卡图

关卡图是生成器的另一个重要概念，用来描述：

- 每次生成的关卡中应有多少个房间；
- 哪些房间应该相连；
- 关卡中的不同房间可以使用哪些房间模板。

例如，一张包含 5 个房间和 4 条连接的简单关卡图，会让每次生成的关卡都恰好包含 5 个房间。

#### 使用关卡图

通过 `Create → Edgar → Level graph` 创建关卡图，然后双击创建出的 ScriptableObject 打开编辑器。

基本操作：

- **创建房间**：双击网格中的空白位置。
- **配置房间**：双击已有房间。
- **删除房间**：按 `Ctrl + Del`，或右键单击房间并选择 `Delete room`。
- **移动房间**：按住鼠标左键拖动。
- **添加连接**：按住 `Ctrl`，左键单击一个房间，然后将光标移动到另一个房间。
- **删除连接**：右键单击连接控制点，选择 `Delete connection`。

> 创建第一张关卡图时应从简单结构开始：只添加少量房间（少于 6 个），不要添加太多连接（保证图连通，但最多只包含一个环）。先掌握生成器的核心概念，再逐步增加复杂度。这个原则也适用于房间模板设计。

#### 分配房间模板

关卡结构准备好后，需要选择生成器可用的房间模板。在关卡图 Inspector 中可以看到 `Default room templates` 和 `Corridor room templates` 两个区域：

- 将普通房间模板拖入 `Default room templates → Room Templates`；
- 将走廊房间模板拖入 `Corridor room templates → Room Templates`。

也可以让某些房间使用不同模板。双击关卡图中的房间，并将模板加入 `Individual Room Templates`。只要给该房间分配了至少一个模板，它就会覆盖默认房间模板。

### 生成器设置

最后一步是在场景中创建地牢生成器实例：

1. 给场景中的任意游戏对象添加 `Dungeon Generator (Grid2D)` 组件。
2. 将关卡图分配给 `Level Graph` 字段。
3. 单击 `Generate dungeon`，或者启用 `Generate on start` 后进入播放模式。

### 故障排查

生成器有时会长时间无响应，随后在 Console 中输出超时错误。这通常表示生成器配置不正确，或当前输入对生成器而言过于困难。

遇到这种情况时，应先查看 Console 输出。那里通常会包含有助于修复问题的诊断信息，例如诊断算法可能提示关卡中的环过多，需要修改关卡图。

如果无法自行解决，可以加入作者的 Discord 服务器寻求帮助。

## 房间模板

房间模板是生成器的主要概念之一，用于描述地牢中各房间的外观以及房间之间的连接方式。

### 创建房间模板

1. 进入准备保存房间模板预制体的文件夹。
2. 在 Project 窗口中单击右键，选择 `Create → Edgar → Dungeon room template`。
3. 可选：按需重命名预制体文件。

### 房间模板结构

新建的房间模板包含：

- `Tilemaps` 游戏对象，其子对象是若干瓦片地图；
- 挂载在根游戏对象上的 `Room Template` 脚本；
- 挂载在根游戏对象上的 `Doors` 脚本。

### 设计房间模板

房间模板使用 Unity Tilemap 设计，因此需要先熟悉 Tilemap。默认房间模板在 `Tilemap` 游戏对象下包含以下层：

- `Floor`：渲染顺序 0；
- `Walls`：渲染顺序 1，带碰撞体；
- `Collideable`：渲染顺序 2，带碰撞体；
- `Other 1`：渲染顺序 3；
- `Other 2`：渲染顺序 4；
- `Other 3`：渲染顺序 5。

> **非常重要：**所有房间模板必须拥有完全相同的瓦片地图结构，因为生成器会把各房间模板中的瓦片复制到共享瓦片地图中。如果需要不同结构，可以覆盖默认行为，详见“房间模板自定义”指南。

可以使用画笔、规则瓦片等所有可用工具绘制房间模板。

### 限制

底层算法处理的是多边形，而不是瓦片地图、瓦片或精灵。算法关心的是各房间模板的轮廓。为了正确计算轮廓，房间模板应遵守以下规则。

如果轮廓无效，`Room Template` 脚本中会显示警告。

> 底层算法不会分别处理每个瓦片地图层，而是先合并所有层，再查找全部非空瓦片。因此，无论使用哪个瓦片地图层，得到的房间轮廓都相同。

#### 只能有一个连通分量

房间中的全部瓦片必须连成一体，不能存在彼此分离的瓦片组。

目前实现会在找到任意一个轮廓后停止，并不会检查所有瓦片是否都包含在该轮廓中，因此错误模板有时也会显示一个不完整轮廓。该行为计划在未来改进。

#### 每格瓦片至少有两个相邻瓦片

每格瓦片必须与至少两个相邻瓦片相连。如果某格只连接到一个相邻瓦片，则房间形状无效。确实需要这种形状时，可以使用下一节介绍的 `Outline override`。

#### 可以包含孔洞

房间模板内部允许存在孔洞。此特性在 1.x.x 版本中不受支持。

### 轮廓覆盖（Outline override）

如果确实需要使用轮廓无效的房间模板，可以使用 `Outline override`。在 `Room Template` 脚本中单击 `Add outline override` 后，会创建名为 `Outline override` 的瓦片地图层。

启用该功能后，算法计算轮廓时会忽略其他所有层。此外，此层上绘制的内容不会出现在最终关卡中，因此可以使用任意瓦片来绘制轮廓。

> 轮廓绘制完成后，可以停用 `Outline override` 游戏对象，以便查看房间模板的实际外观，但不能删除该游戏对象。

### 包围盒轮廓处理器

有些情况下，使用覆盖房间模板中全部瓦片的包围盒作为轮廓会更方便，例如处理某些平台跳跃关卡。还可以给轮廓顶部添加内边距，以便在高于原轮廓的位置添加门。

在 `Room Template` Inspector 中单击 `Add bounding box outline handler`，即可添加包围盒轮廓处理器。它可自动把无效轮廓修正为包围盒，也支持通过 `Padding top` 增加顶部空间。

### 添加门

完成房间外观后，可以添加门。门位置用于告诉算法不同房间模板之间可以如何连接。

满足以下条件时，算法可以连接两个房间模板：

- 两个模板存在长度相同的门位置；
- 连接后两个房间模板不会重叠；
- 但两个房间的轮廓瓦片可以重合。

> 在某些关卡生成器中，定义 N 扇门意味着房间必须连接 N 个邻居；Edgar 并非如此。门位置仅表示“这里可以放门”。拥有 20 个可用门位置的模板仍可用于只有一个邻居的房间。通常，可用门位置越多，生成性能越好。

### 如何理解门的 Gizmo

编辑器中用红色矩形表示可用门位置：

- 红色虚线矩形表示“门线”，即该矩形范围内所有可能门位置的集合；
- 红色实线矩形表示门的长度；
- 实线矩形还会显示该门线上包含多少个门位置。

门位置可以互相重叠，而且重叠通常有利于性能：可选位置越多，生成器越容易快速找到有效布局。

### 门模式

要编辑门，房间模板预制体根节点上必须挂载 `Doors` 组件。当前有三种定义门位置的方式。无论使用哪种模式，所有门位置都必须位于房间模板轮廓上。

#### 简单模式

简单模式中，需要指定所有门的宽度和边距（门距离房间拐角的最小距离）。当你不关心门的精确位置时，这种模式很合适。由于可选门位置很多，它通常也有最佳性能。

横版游戏对水平门与垂直门经常有不同要求。例如，玩家可能高 3 格、宽 1 格，因此垂直方向的门需要更宽。可以把 `Mode` 下拉框改为 `Different Horizontal And Vertical`，分别配置水平门与垂直门，也可以禁用其中一种门。

#### 手动模式

手动模式需要逐一指定房间模板的门位置，适合仅有少数几个门且位置非常明确的情况。

单击 `Doors` 脚本中的 `Manual mode`，再单击 `Add door positions` 进入编辑模式。单击门的第一格瓦片，然后拖到最后一格瓦片，即可绘制门位置。

手动模式允许不同长度的门，例如垂直门高 3 格、水平门宽 1 格。

删除门有两种方法：

1. 单击 `Delete all door positions` 删除所有门位置；
2. 单击 `Delete door positions`，再单击要删除的门位置。

> 多扇门重叠时，GUI 会变得杂乱，这种情况通常应使用混合模式。  
> 当前 Inspector 允许添加不在房间轮廓上的门，但生成地牢时会报错，未来版本可能会改进这一点。

#### 混合模式

混合模式介于简单模式和手动模式之间。它不逐个绘制门，而是一次绘制整条门线（即多个门）。

进入相应编辑模式后，需要先在下方字段配置门的长度。与手动模式的区别是：

- 手动模式通过鼠标拖动距离决定门长；
- 混合模式在编辑器中预先配置门长，鼠标拖动距离决定相邻门的数量。

无法使用简单模式、但手动配置又过于耗时时，混合模式非常合适。门线定义天然支持门位置重叠；同时，它鼓励提供大量门，并以生成器易于处理的格式保存，因此性能通常优于手动模式。

### (PRO) 门插槽

默认情况下，生成器通过比较门的长度来判断两个房间模板能否连接。如果需要更精细地控制匹配过程，可以使用 `Door sockets`。

### (PRO) 门方向

默认情况下，所有门都没有方向，既可作为入口也可作为出口。在手动门模式中，可以把门配置为仅入口或仅出口。配合有向关卡图，可以更精确地控制生成结果。详见“有向关卡图”指南。

### 重复模式

每个 `Room Template` 脚本都有一个默认设为 `Allow Repeat` 的 `Repeat Mode` 字段，用于控制同一模板能否在生成关卡中重复使用：

- `Allow repeat`：允许模板重复出现；
- `No immediate`：使用该模板的房间与其邻居必须使用不同模板；
- `No repeat`：该模板最多只能使用一次。

也可以直接在地牢生成器上配置全局覆盖，而不是逐个模板设置。

> 如果可选模板太少，即使选择 `No immediate` 或 `No repeat`，生成结果中也可能出现重复。请提供足够多的模板，才能确保满足重复模式约束。

### 走廊

算法区分普通房间模板和走廊房间模板。理论上，任何至少有两扇门的模板都可以充当走廊，但为了提高算法速度，应遵守以下建议：

1. 恰好只有两个门位置；
2. 两扇门位于房间模板相对的两侧；
3. 走廊不要太长或太宽。

推荐使用狭窄的直线走廊。较宽的走廊尚可使用，但不建议让门位于非相对侧，也不建议设置两个以上门位置。

### 最后步骤

创建好房间模板游戏对象后，将其保存为预制体，即可在关卡图中使用。

## 关卡图

关卡图是一层抽象，用于控制生成关卡的结构。

> 本插件中的“图”指由节点和边构成的数学结构，并非用于显示函数曲线的图表。

### 基础

关卡图由房间和房间连接组成。每个房间对应生成关卡中的一个房间；每条连接表示两个房间必须直接相连，或通过走廊相连。

例如，一张包含 5 个房间和 4 条连接的简单关卡图，会使每个生成的地牢恰好包含 5 个房间。若房间 1 与其他所有房间相连，那么生成结果也会保持这种连接关系。

关卡图在编辑器中的绘制位置并不重要，真正重要的只有房间数量以及房间之间的连接关系。

### 限制

#### 平面图

关卡图必须是平面图。若一张图能绘制在平面上且任意两条边都不相交，就称其为平面图。对本生成器而言，这意味着不能存在无法消除的连接线交叉，否则就无法找到房间和走廊互不重叠的地牢布局。

一张关卡图即使当前画法中有边相交，也仍可能是平面图，因为可以换一种无交叉的方式绘制。判断依据不是某一种具体画法，而是是否存在无交叉画法。

#### 连通图

关卡图必须连通。如果任意两个顶点之间都存在路径，就称图是连通的。左右两组顶点之间完全没有路径的图不是连通图。

### 创建关卡图

`LevelGraph` 是一个 ScriptableObject，可通过 `Create → Edgar → Level graph` 创建。

### 图编辑器

双击关卡图 ScriptableObject 即可打开 Graph editor。

窗口控制项：

- `Selected graph`：当前选中的关卡图名称；
- `Select in inspector`：在 Inspector 中选中当前关卡图；
- `Select level graph`：选择另一张关卡图。

关卡图操作：

- **创建房间**：双击网格空白处；
- **配置房间**：双击已有房间；
- **删除房间**：按 `Ctrl + Del`，或右键房间并选择 `Delete room`；
- **移动房间**：按住鼠标左键拖动；
- **添加连接**：按住 `Ctrl`，左键单击一个房间，再将光标移动到另一个房间；
- **删除连接**：右键单击连接控制点，选择 `Delete connection`。

### 房间模板

创建房间和连接后，需要设置房间模板。关卡图 Inspector 中的 `Default room templates` 和 `Corridor room templates` 用于指定各类房间可用的模板。

#### 房间模板集合

有时需要把模板分成商店房、Boss 房等组。通过 `Create → Edgar → Room templates set` 可以创建“房间模板集合”。它是一个保存房间模板数组的简单 ScriptableObject，可以代替逐一分配模板。

其主要优点是：以后新增商店房模板时，无需修改关卡图中的每个商店房，只要把新模板加入对应集合，所有使用该集合的房间都会自动获得更新。

#### 默认房间模板

- `Room templates`：供未单独指定房间形状的房间使用的模板数组。通常在这里加入普通房间，再给出生点、Boss 房等特殊房间单独配置模板。
- `Room templates sets`：供未单独指定房间形状的房间使用的模板集合数组。集合中的模板会与单独列出的模板共同使用。

#### 走廊房间模板

- `Room templates`：走廊房间使用的模板数组。仅在算法使用走廊连接相邻房间时需要；不使用走廊时可以留空。
- `Room templates sets`：走廊使用的模板集合数组。集合中的模板会与单独列出的走廊模板共同使用。

### 配置单个房间

在 Graph editor 中双击房间即可选中它，并在 Inspector 中进行配置。可以设置显示在图编辑器中的房间名称，也可以分配仅供该房间使用的模板或模板集合。

只要给房间分配了任意模板或模板集合，就会覆盖关卡图自身设置的默认房间模板。

### (PRO) 自定义房间和连接

经常需要给单个房间或连接附加额外信息，例如给每个房间指定类型，再根据类型执行不同逻辑。可以通过自行实现 `RoomBase` 和 `ConnectionBase` 来完成。

该功能在示例中的用途包括：在 Dead Cells 示例中，用自定义类型扩展房间定义。

#### 继承 Room

第一种方式是创建继承默认 `Room` 类的自定义类。若只想附加信息、不想改变房间本身的工作方式，这种方式最合适。还可以覆盖 `GetDisplayName()`，改变房间在关卡图编辑器中的显示文本。

这是面向大多数用户的推荐方式。

#### 继承 RoomBase

第二种方式是直接继承 `RoomBase`。此时必须实现所有抽象方法，目前包括 `GetDisplayName()` 和 `GetRoomTemplates()`。

这种方式的优点是：某些场景中不需要任何模板相关逻辑，可以直接从方法返回 `null`，这样房间 Inspector 就不会显示模板相关内容。例如，可以根据房间类型完全自行解析模板。

继承 `Connection` 或 `ConnectionBase` 时同样遵循上述逻辑。

#### 配置关卡图

自定义房间或连接类型准备好后，需要让关卡图使用它们。打开关卡图 Inspector，即可从下拉列表中选择自定义类型。

> 很难把已经创建的关卡图从一种房间／连接类型转换成另一种。因此，应在创建关卡图之前决定是否使用自定义类型，否则之后可能需要按正确类型重新创建关卡图。

#### 访问房间信息

如果给房间或连接添加了额外信息，通常需要在生成后读取它。首先获取 `RoomInstance`，然后访问 `RoomInstance.Room` 属性。该属性类型为 `RoomBase`，因此需要将它转换为自定义房间类型。

#### 在关卡图编辑器中使用自定义颜色

覆盖 `GetEditorStyle()` 并返回 `RoomEditorStyle` 或 `ConnectionEditorStyle`，即可改变自定义房间和连接在关卡图编辑器中的外观：

```csharp
public class GungeonRoom : RoomBase
{
    public GungeonRoomType Type;

    /* ... */

    public override RoomEditorStyle GetEditorStyle(bool isFocused)
    {
        var style = base.GetEditorStyle(isFocused);
        var backgroundColor = style.BackgroundColor;

        // 按房间类型使用不同颜色
        switch (Type)
        {
            case GungeonRoomType.Entrance:
                backgroundColor = new Color(38 / 256f, 115 / 256f, 38 / 256f);
                break;
            /* ... */
        }

        style.BackgroundColor = backgroundColor;

        // 获得焦点时让颜色变暗
        if (isFocused)
        {
            style.BackgroundColor = Color.Lerp(style.BackgroundColor, Color.black, 0.7f);
        }

        return style;
    }
}
```

### (PRO) 有向关卡图

默认情况下，关卡图是无向图：从房间 1 连接到房间 2，和从房间 2 连接到房间 1 没有区别。若需要更精确地控制生成结果，可以使用有向关卡图，并与仅入口、仅出口的门配合。详见“有向关卡图”指南。

## 生成器设置

准备好关卡图后，即可设置程序化地牢生成器：

1. 在场景中创建空游戏对象；
2. 给该对象添加 `Dungeon Generator` 组件；
3. 把关卡图分配给 `Level Graph`；
4. 单击 `Generate dungeon`，或启用 `Generate on start` 后进入播放模式。

本节的目标是用合理的默认配置创建生成器实例，而不是解释生成器的每一个选项。各项配置详见“地牢生成器”章节。

## 关卡结构与房间数据

### 关卡结构

承载关卡的游戏对象有两个子对象：

- `Tilemaps`：保存所有瓦片地图层；
- `Rooms`：保存关卡中使用的所有房间模板实例。

`Rooms` 下每个子对象的名称格式为 `"{roomName} - {roomTemplate}"`，方便调试时定位具体房间。

> 如果需要通过脚本获取这些游戏对象，最佳实践是使用 `GeneratorConstants` 的静态字段，而不是写死名称。

### 房间信息

生成器还会产生每个房间的位置、所用模板、相邻房间等信息，并通过 `RoomInstance` 类公开。

至少有两种方法可以获取 `RoomInstance`：

1. **通过房间游戏对象获取**：前述房间模板实例上都挂载了 `RoomInfo` 组件，该组件引用对应的 `RoomInstance`。
2. **通过后处理任务获取**：每个自定义后处理任务都会收到 `GeneratedLevel` 实例，可调用其 `GetRoomInstances()` 获取关卡中的全部房间实例。

## 性能建议

正确使用 Edgar 可以生成非常复杂的关卡，但也很容易提供对生成器而言过于困难的输入，最终触发 `TimeoutException`。

总体原则是：如果在某一方面增加生成难度（例如房间很多），就应在另一方面降低难度（例如不在关卡图中使用环）。建议先从简单结构开始，熟悉生成器行为后再逐步增加复杂度。

### 房间模板

**尽可能提供更多门位置。**这一点极其重要。应尽量使用简单或混合门模式，仅在绝对必要时使用手动模式。唯一的例外是生成无环关卡，此时门位置较少通常也可以接受。

**确保默认房间模板适用于大多数房间。**把模板加入 `Default room templates` 虽然最方便，但不应加入只能用于非常特殊场景的模板。例如，只有一个门位置的密室模板不应加入默认列表。否则生成器可能尝试把它用于拥有多个邻居的房间，浪费大量时间。应只把此类特殊模板分配给真正适合的房间。

### 关卡图

**限制房间数量。**关卡图中的房间数量会显著影响性能。经验上应少于 20 个；如果遵守其他性能建议，也可以生成最多约 40 个房间的关卡。

**限制环的数量。**生成带环关卡非常困难，因此环数会显著影响性能。通常应从 0～1 个环开始，熟悉 Edgar 核心概念后再增加。Enter the Gungeon 示例中最多使用 3 个环，生成器仍相对较快。

**避免互相连接的环。**环本身就很困难，共享房间的多个环更难。如果需要多个环，应确保不同环之间没有共同房间。通常不难设计出这种关卡图；Enter the Gungeon 中的所有关卡图都符合这一特性，并未损害游戏体验。

## 地牢生成器

### 最小设置

1. 给场景中的任意游戏对象添加 `Dungeon Generator` 组件；
2. 将关卡图分配给 `Level Graph`；
3. 单击 `Generate dungeon`，或启用 `Generate on start` 后进入播放模式。

### 配置

#### 输入配置（FixedLevelGraphConfigGrid2D）

- `Level Graph`：要使用的关卡图，不能为 `null`。
- `Use corridors`：是否在相邻房间间使用走廊。启用后，必须在关卡图中提供走廊房间模板。

#### 生成器配置（DungeonGeneratorConfigGrid2D）

- `Root Game Object`：生成关卡将挂载到的游戏对象。为 `null` 时会新建游戏对象。
- `Timeout`：等待算法生成关卡的最长时间，单位为毫秒。某些输入对算法而言可能过于困难，因此达到限定时间后应报错停止。
- `Repeat Mode Override`：是否覆盖各房间模板自身的重复模式。
  - `No override`：不覆盖，沿用模板设置；
  - `Allow repeat`：所有模板都可重复；
  - `No immediate`：相邻房间必须使用不同模板；
  - `No repeat`：所有房间必须使用不同模板。

> 如果模板数量太少，即使选择 `No immediate` 或 `No repeat`，生成结果仍可能重复。请提供足够多的可选模板。

- `Minimum Room Distance`：非相邻房间之间的最小距离。
  - `0`：不同房间的墙可以占用同一格瓦片；
  - `1`（默认）：不同房间的墙可以相邻，但不能重叠；
  - `2`：不同房间的墙之间至少要留一格空瓦片。使用规则瓦片出现异常时，这通常很有用。

> 较大的 `Minimum Room Distance` 会降低生成性能。走廊很短时，参数过大甚至可能导致关卡无法生成。

#### 后处理配置（PostProcessingConfigGrid2D）

详细信息请参阅“后处理”章节。

- `Initialize Shared Tilemaps`：是否初始化用于容纳生成关卡的共享瓦片地图。
- `Tilemap Layers Handler`：初始化共享瓦片地图时使用的瓦片地图层处理器。未设置时使用 `DungeonTilemapLayersHandler`。
- `Tilemap Material`：共享瓦片地图的 Tilemap Renderer 所用材质，可用于灯光等效果。留空时使用默认材质。
- `Copy Tiles To Shared Tilemaps`：是否把各房间模板的瓦片复制到共享瓦片地图。
- `Center Grid`：是否移动关卡，使其中心大致位于 `(0, 0)`；方便在编辑器 Scene 视图中调试。
- `Disable Room Template Renderers`：是否禁用各房间模板的瓦片地图渲染器。仅在启用 `Copy Tiles To Shared Tilemaps` 时有用。
- `Disable Room Template Colliders`：是否禁用各房间模板的瓦片地图碰撞体。仅在启用 `Copy Tiles To Shared Tilemaps` 时有用。

#### 其他配置（直接位于生成器类上）

- `Use Random Seed`：每次生成新关卡时是否使用随机种子。
- `Random Generator Seed`：关闭 `Use Random Seed` 时使用的随机数种子，便于调试。
- `Generate On Start`：进入播放模式时是否生成新关卡。

### 通过脚本修改配置

```csharp
// 获取生成器组件
var generator = GameObject.Find("Dungeon Generator").GetComponent<DungeonGeneratorGrid2D>();

// 访问输入配置
generator.FixedLevelGraphConfig.UseCorridors = false;

// 访问生成器配置
generator.GeneratorConfig.Timeout = 5000;

// 访问后处理配置
generator.PostProcessConfig.CenterGrid = false;

// 访问其他属性
generator.UseRandomSeed = false;
generator.RandomGeneratorSeed = 1000;
generator.GenerateOnStart = false;
```

### 通过脚本调用生成器

调用生成器非常简单：

1. 从承载生成器的游戏对象上获取 `DungeonGenerator` 组件；
2. 调用 `Generate()`。

```csharp
var generator = GameObject.Find("Dungeon Generator").GetComponent<DungeonGeneratorGrid2D>();
generator.Generate();
```

> `Generate()` 会阻塞 Unity 主线程，因此生成地牢期间游戏可能会卡住。PRO 版本提供基于协程的实现，可避免游戏冻结。

### (PRO) 使用协程

如果不想在生成关卡时阻塞 Unity 主线程，可以使用协程，有两种方式。

简单方式只使用 Unity 内置协程：

```csharp
var generator = GameObject.Find("Dungeon Generator").GetComponent<DungeonGeneratorGrid2D>();
StartCoroutine(generator.GenerateCoroutine());
```

简单方式的问题是协程无法妥善处理异常。如果生成器或自定义后处理逻辑出错，协程会直接终止，无法执行清理。因此 Edgar 还提供了可以处理错误的智能协程：

```csharp
public class CoroutineWithDataExampleAdvanced : MonoBehaviour
{
    public void Start()
    {
        var generator = GameObject.Find("Dungeon Generator")
            .GetComponent<DungeonGeneratorGrid2D>();
        StartCoroutine(GeneratorCoroutine(generator));
    }

    private IEnumerator GeneratorCoroutine(DungeonGeneratorGrid2D generator)
    {
        // 启动智能协程。
        // StartSmartCoroutine 是 MonoBehaviour 的自定义扩展方法，
        // 请确保引用 Edgar.Unity 命名空间。
        var generatorCoroutine = this.StartSmartCoroutine(generator.GenerateCoroutine());

        // 等待智能协程结束。
        // 务必 yield return Coroutine 属性，而不是 generatorCoroutine 本身。
        yield return generatorCoroutine.Coroutine;

        // 检查协程是否成功。
        if (generatorCoroutine.IsSuccessful)
        {
            Debug.Log("Level generated!");
        }
        // 发生错误时可以访问 Exception，
        // 也可调用 generatorCoroutine.ThrowIfNotSuccessful() 重新抛出异常。
        else
        {
            Debug.LogError("There was an error when generating the level!");
            Debug.LogError(generatorCoroutine.Exception.Message);
        }
    }
}
```

## 后处理

关卡生成后，通常还需要生成敌人等附加逻辑。可以提供自己的后处理逻辑；它会在关卡生成后被调用，并收到关卡相关信息。

为便于理解生成器的工作方式，下面先介绍内置后处理步骤，再说明如何扩展。如果只关心自定义逻辑，可直接跳到“自定义后处理”。

### 内置后处理步骤

#### 0. 在正确位置实例化房间模板

严格来说，这并非后处理，因为它发生在生成阶段且无法禁用。此时生成器已经知道每个房间的最终位置和模板，会遍历房间、实例化对应模板并移动到正确位置。

如果禁用其他所有后处理步骤，会得到一批位置正确的房间，但房间之间经常会出现奇怪的重叠。

#### 1. 初始化共享瓦片地图

生成器会初始化共享瓦片地图结构，下一步将把各房间复制到其中。共享瓦片地图最终包含关卡中的全部瓦片。若提供了自定义 `Tilemap Layers Handler`，会在此时调用。

#### 2. 把房间复制到共享瓦片地图

生成器把各房间复制到共享瓦片地图。使用走廊时，必须先复制普通房间，再复制走廊。这样走廊会在其他房间的墙上开口，使玩家能够在房间之间通行。

#### 3. 居中网格

移动整个关卡，使其中心位于 `(0, 0)` 附近。这样在编辑器 Scene 视图中查看多个生成关卡时，无需反复移动摄像机。

#### 4. 禁用房间模板渲染器

此时共享瓦片地图和步骤 0 中实例化的房间对象都会显示瓦片，因此必须禁用各房间模板的所有 Tilemap Renderer。

不能简单地停用整个房间模板，因为模板中还可能有灯光、敌人等其他游戏对象，需要保留。

#### 5. 禁用房间模板碰撞体

最后一步与上一步类似。各房间模板中的碰撞体此时会阻止玩家从一个房间进入另一个房间，因此需要禁用。设为 Trigger 的碰撞体会保留，因为它们可能用于“当前房间检测”等功能。

### 自定义后处理

目前有两种实现方式：自定义组件或 ScriptableObject。建议先使用自定义组件，因为它更简单；只有需要 ScriptableObject 的特性时再使用后者。

> 在 `v2.0.0-beta.0` 之前，只能用 ScriptableObject 实现自定义后处理，但流程较繁琐：需要添加容易忘记的 `CreateAssetMenu` 特性，再创建 ScriptableObject 实例。因此新版也支持直接把 MonoBehaviour 组件挂到生成器游戏对象上。

#### 使用 MonoBehaviour 组件

创建继承 `DungeonGeneratorPostProcessingComponentGrid2D`（其自身继承 `MonoBehaviour`）的类，并覆盖 `void Run(DungeonGeneratorLevelGrid2D level)`：

```csharp
public class MyCustomPostProcessingComponent
    : DungeonGeneratorPostProcessingComponentGrid2D
{
    public override void Run(DungeonGeneratorLevelGrid2D level)
    {
        // 在这里实现逻辑
    }
}
```

实现完成后，进入包含生成器的场景，把该组件挂载到生成器游戏对象上。再次运行生成器时，自定义后处理代码就会被调用。

#### 使用 ScriptableObject

创建继承 `DungeonGeneratorPostProcessingGrid2D`（其自身继承 `ScriptableObject`）的类。因为基类是 ScriptableObject，还需要添加 `CreateAssetMenu`，以便创建实例。然后覆盖 `void Run(DungeonGeneratorLevelGrid2D level)`：

```csharp
[CreateAssetMenu(
    menuName = "Edgar/Examples/Docs/My custom post-processing",
    fileName = "MyCustomPostProcessing")]
public class MyCustomPostProcessing : DungeonGeneratorPostProcessingGrid2D
{
    public override void Run(DungeonGeneratorLevelGrid2D level)
    {
        // 在这里实现逻辑
    }
}
```

实现后，在 Project 视图中右键选择：

`Create → Edgar → Examples → Docs → My custom post-processing`

创建 ScriptableObject 实例，最后把它拖入地牢生成器的 `Custom post process tasks` 数组。

#### 此功能在哪里使用

- Example 1 / Dead Cells：关卡生成后通过自定义后处理任务生成敌人；
- Dead Cells：把玩家移动到关卡出生位置。

### (PRO) 优先级回调

PRO 版本支持带优先级的回调，相关配置和示例请参阅在线文档。

## (PRO) 平台跳跃关卡生成器

### 最小设置

1. 给场景中的任意游戏对象添加 `Platformer Generator` 组件；
2. 把关卡图分配给 `Level Graph`；
3. 单击 `Generate platformer`，或启用 `Generate on start` 后进入播放模式。

### 配置与用法

配置目前与 `Dungeon Generator` 相同。唯一的区别是，通过代码使用生成器时要使用 `PlatformerGenerator` 类。

### 默认瓦片地图结构

可通过 `Create → Edgar → Platformer room template` 创建平台跳跃房间模板。默认瓦片地图结构为：

- `Background`；
- `Walls`：带碰撞体；
- `Platforms`：带碰撞体和 Platform Effector；
- `Collideable`：带碰撞体；
- `Other 1`；
- `Other 2`。

### 限制

#### 无环关卡图

应只使用无环图。平台跳跃房间模板通常限制较多，难以支持环。生成器目前允许输入带环图，但经常无法生成任何有效关卡。

#### 生成关卡的可通关性

生成器无法保证所有关卡都可通关。例如，玩家可能因无法完成某次跳跃而困在死路。最简单的处理方式通常是合理设计房间模板和关卡图，从结构上杜绝两个房间以无法从前者到达后者的方式连接。

## (PRO) 自定义输入

免费版的生成器输入是固定的：在 GUI 中创建关卡图，再把它直接交给生成器。但有时需要修改关卡图，例如把一个密室连接到随机房间。自定义输入可提供更强的输入控制能力。

### LevelGraph 与 LevelDescription

首先要理解 `LevelGraph` 与 `LevelDescription` 的区别。

`LevelGraph` 是房间和连接的集合，用于描述生成关卡的高层结构。每张关卡图都关联一个 `LevelGraph` ScriptableObject。

但生成器不会直接使用 `LevelGraph`。必须先将它转换成 `LevelDescription`，因为 `LevelGraph` 主要服务于 GUI 编辑器，而生成器需要真正的图数据结构。

两者都围绕房间和连接工作。以下代码展示基本 API 及转换方法：

```csharp
// [CreateAssetMenu(
//     menuName = "Dungeon generator/Examples/Docs/My custom input task",
//     fileName = "MyCustomInputSetup")]
public class CustomInputExample : DungeonGeneratorInputBaseGrid2D
{
    public LevelGraph LevelGraph;
    public bool UseCorridors;

    protected override LevelDescriptionGrid2D GetLevelDescription()
    {
        var levelDescription = new LevelDescriptionGrid2D();

        // 遍历关卡图中的房间，并加入关卡描述。
        foreach (var room in LevelGraph.Rooms)
        {
            levelDescription.AddRoom(room, GetRoomTemplates(room));
        }

        // 遍历关卡图中的连接。
        foreach (var connection in LevelGraph.Connections)
        {
            if (UseCorridors)
            {
                // 为走廊创建房间。
                var corridorRoom = ScriptableObject.CreateInstance<Room>();
                corridorRoom.Name = "Corridor";
                levelDescription.AddCorridorConnection(
                    connection,
                    corridorRoom,
                    GetCorridorRoomTemplates());
            }
            else
            {
                levelDescription.AddConnection(connection);
            }
        }

        return levelDescription;
    }

    /// <summary>
    /// 获取指定房间可用的房间模板。
    /// </summary>
    private List<GameObject> GetRoomTemplates(RoomBase room)
    {
        var roomTemplates = room.GetRoomTemplates();

        // 列表为空时，使用关卡图中的默认模板。
        if (roomTemplates == null || roomTemplates.Count == 0)
        {
            var defaultRoomTemplates = LevelGraph.DefaultIndividualRoomTemplates;
            var defaultRoomTemplatesFromSets =
                LevelGraph.DefaultRoomTemplateSets.SelectMany(x => x.RoomTemplates);

            // 合并单独配置的模板与模板集合中的模板。
            return defaultRoomTemplates
                .Union(defaultRoomTemplatesFromSets)
                .ToList();
        }

        return roomTemplates;
    }

    /// <summary>
    /// 获取走廊房间模板。
    /// </summary>
    private List<GameObject> GetCorridorRoomTemplates()
    {
        var defaultRoomTemplates = LevelGraph.CorridorIndividualRoomTemplates;
        var defaultRoomTemplatesFromSets =
            LevelGraph.CorridorRoomTemplateSets.SelectMany(x => x.RoomTemplates);

        return defaultRoomTemplates
            .Union(defaultRoomTemplatesFromSets)
            .ToList();
    }
}
```

### 自定义输入实现

自定义输入与自定义后处理类似。创建继承 `DungeonGeneratorInputBase` 的类。由于基类是 ScriptableObject，需要添加 `CreateAssetMenu`，并实现抽象方法 `LevelDescription GetLevelDescription()`。

实现逻辑后，在 Project 视图中右键选择：

`Create → Edgar → Examples → Docs → My custom input`

创建 ScriptableObject 实例。最后在生成器 Inspector 中把 `Input Type` 改为 `Custom Input`，并把实例拖入 `Custom Input Task`。

### 典型用例

#### 给关卡图添加房间

常见用法是在现有关卡图中添加额外房间，例如随机密室。通常流程如下：

1. 在 GUI 中创建关卡图的静态部分；
2. 创建自定义输入任务，公开一个用于接收关卡图的字段；
3. 将 `LevelGraph` 转换为 `LevelDescription`；
4. 创建额外房间，并在关卡描述中把它们连接到已有房间。

为了方便处理房间和连接构成的图，`LevelDescription` 提供 `IGraph<RoomBase> GetGraph()`，可获得当前房间图。该图包含获取全部房间、检查两个房间是否相邻等常用方法。

```csharp
// [CreateAssetMenu(
//     menuName = "Dungeon generator/Examples/Docs/My custom input task",
//     fileName = "MyCustomInputSetup")]
public class CustomInputExample2 : DungeonGeneratorInputBaseGrid2D
{
    protected override LevelDescriptionGrid2D GetLevelDescription()
    {
        /* 在这里创建关卡描述 */
        /* ... */
    }
}
```

具体实现可参阅 Enter the Gungeon 示例，其中会把密室连接到图中的随机房间。

> `GetGraph()` 当前返回的图不会在修改关卡描述后自动更新。修改后需要再次调用该方法，获取新的图。

#### 自动分配房间模板

另一种常见用途是实现自定义模板分配逻辑。例如使用自定义房间时，可以按房间类型自动分配模板，而不必逐个手动配置。Enter the Gungeon 和 Dead Cells 示例都使用了这种方式。

#### 程序化图

也可以完全不使用静态部分，在运行时即时创建整个关卡描述，从而得到完全程序化的关卡结构。

## 常见问题

本节汇总 Discord 等渠道中经常出现的问题。

### 如何在指定房间中生成玩家

最简单的方式是设计专用出生房模板，并把玩家预制体放进该模板。然后将它设为关卡图中 `Spawn` 房间唯一可用的模板。Example 1 展示了这种方法。

另一种方式是在后处理逻辑中移动玩家。不要把玩家预制体直接放进出生房，而是用空游戏对象标记出生点。关卡生成后运行后处理脚本，把玩家移动到标记位置。Dead Cells 示例展示了这种方法。

### 遇到 TimeoutException 怎么办

生成关卡时，Console 有时会出现 `TimeoutException`。这表示生成器未能在限定时间内生成关卡，默认时限为 10 秒。它可能表示：

- 关卡图对生成器而言过于困难，例如房间太多、环太多、房间模板限制太强；
- 配置存在问题，例如两个相邻房间模板的门不兼容。

通常是第二种情况。为了帮助定位问题，生成器会在异常上方输出诊断信息，例如门长度可疑，或关卡图中的房间可能过多。

如果无法自行解决，可以前往作者的 Discord 寻求帮助，也可阅读“性能建议”章节。

### 如何处理更宽的墙

某些图块集的墙宽度超过一格。如果仍按单格墙处理，走廊只会穿过多格墙的第一格，无法完全贯通。

解决方法是使用 `Outline Override` 修改走廊轮廓。Example 2 教程展示了相关配置，其中使用的图块集在水平墙上方还有一层额外墙瓦片；重点可查看其中的 `Vertical corridors` 部分。

### 生成关卡后，房间模板的改动丢失

你可能会修改房间模板的默认结构，例如添加碰撞体、添加瓦片地图层，或改变 Grid 的 Cell Size，但单击 `Generate` 后这些改动没有生效，关卡看起来和之前一样。

原因是关卡生成后，所有房间模板会被合并到一组共享瓦片地图中。因此，除了修改房间模板，还必须告诉生成器把相同改动应用到共享瓦片地图。在线文档中有专门的“房间模板自定义”指南。

### 房间生成得过于接近

生成器经常让一个房间的墙紧贴另一个房间的墙。通常没有问题，但有时会产生异常。例如墙使用规则瓦片时，过近的房间可能影响另一房间中的瓦片规则。

在地牢生成器 Inspector 中找到 `Minimum Room Distance`，把值提高到 `2`。需要更大间距时还可以继续提高，但每次增大都会让生成更困难，可能导致超时。详细说明见 `Minimum Room Distance` 配置。

### 在多人游戏中向多个玩家发送同一关卡

多人游戏中，让所有玩家获得同一关卡的最简单方法是：把生成器种子发送给每位玩家，然后让所有客户端用该种子运行生成器。在线文档中可查看种子配置及通过代码读取种子的方法。

### 在编辑器中生成关卡时保留预制体引用

请参阅下一章“在编辑器中保留预制体关联”。

## 在编辑器中保留预制体关联

关卡生成后，关卡中使用的每个房间模板预制体都会经过 `Object.Instantiate()`。该方法会移除实例与原始预制体之间的连接，相当于完全解包预制体。

但有时保留预制体引用很有用，例如在编辑器中使用关卡生成器，之后还要手动修改生成的关卡。

### 解决方案

目前没有只勾选一个复选框即可实现的方案，但可以直接修改资源源码中的一两行来改变默认行为。

找到 `GeneratorUtilsGrid2D` 类及其中的 `InstantiateRoomTemplate()` 方法。该方法有两个常量，可用于在编辑器内生成关卡时保留预制体引用：

```csharp
private static GameObject InstantiateRoomTemplate(GameObject roomTemplatePrefab)
{
    // 若要在编辑器中生成关卡时保留预制体关联，设为 true。
    const bool keepPrefabsInEditor = false;

    // 若要解包预制体的根游戏对象，设为 true。
    // 只有 keepPrefabsInEditor 为 true 时，该常量才有效。
    const bool unpackRootObject = false;

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    if (!Application.isPlaying && keepPrefabsInEditor)
    {
#if UNITY_EDITOR
        var roomTemplateInstance =
            (GameObject)PrefabUtility.InstantiatePrefab(roomTemplatePrefab);

        if (unpackRootObject)
        {
#pragma warning disable CS0162 // 检测到无法访问的代码
            PrefabUtility.UnpackPrefabInstance(
                roomTemplateInstance,
                PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction
            );
#pragma warning restore CS0162 // 检测到无法访问的代码
        }

        return roomTemplateInstance;
#endif
    }

    return Object.Instantiate(roomTemplatePrefab);
}
```

