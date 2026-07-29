# RenderDoc → FBX 导出插件（改进版）

基于 `spamrakuen/renderdoc2fbx`（原作者 timmyliang，MIT）修改，用于从 RenderDoc 的 Mesh Viewer 直接把网格导出为 FBX（ASCII）。

原地址：
- https://github.com/spamrakuen/renderdoc2fbx
- https://github.com/FXTD-ODYSSEY/renderdoc2fbx

## 本版改了什么

1. **属性映射改成下拉枚举**：Position / Normal / Tangent / BiNormal / Color / UV / UV2 每个通道都用下拉框，列出 VS Input 表里检测到的**所有属性**，下拉项标注分量数（如 `_input0 (3 分量)`），方便判断谁是 UV / 法线。第一项固定是 `none`。
2. **打开对话框前自动识别并预选**：
   - Position = 第一个属性（优先 ≥3 分量）
   - UV / UV2 = 分量为 2（vector2）的属性，值域接近 0~1 的优先
   - Normal = ≥3 分量且长度≈1 的属性
   - Tangent = 剩下的 4 分量属性
   - 识别不出来 → 默认 `none`，可手动改。
   - 解决了没有语义名（列名是 `_input0`/`_sig50` 这种）无法映射的问题。
3. **修复导出空文件**：原插件强依赖 VS Input 的 `IDX` 列，非索引绘制时读不到导致导出空网格。现改为：没有 IDX 就用 VTX，再没有就用顺序索引 `0,1,2...`。
4. 找不到数据表时给出明确中文报错。

## 安装

方式一（推荐）：双击 `install.bat`，会复制到 `%APPDATA%\qrenderdoc\extensions\timmyliang`。

方式二（手动）：把本目录下的 `timmyliang` 文件夹整个复制到：
```
%APPDATA%\qrenderdoc\extensions
```

安装后**重启 RenderDoc**，在 `Tools -> Manage Extensions` 中启用 **FBX Mesh Exporter**。

> 要求 RenderDoc ≥ 1.17。

## 使用

1. 打开 Mesh Viewer，选好要导出的 draw call。
2. 面板菜单点 **Export FBX Mesh**。
3. 弹出对话框，Position/UV 等已自动预选；核对（对着分量数看），不对就下拉改，没有的通道选 `none`。
4. OK → 选保存路径 → 导出。

## 注意

- 插件只读 **VS Input** 表（本地坐标），这是正确的模型数据；不要用 VS Output（裁剪空间）。
- UV 判断：只有 x/y 两分量且值在 0~1 附近的才是 UV。

## 目录结构

```
csv2fbxFix/
├── install.bat                 一键安装脚本
├── README.md                   本说明
└── timmyliang/
    └── exporter/
        └── fbx/
            ├── extension.json  扩展清单
            ├── __init__.py     主逻辑（含自动识别调用 + IDX 兜底）
            ├── query_dialog.py 下拉映射对话框（自动识别）
            └── progress_dialog.py
```
