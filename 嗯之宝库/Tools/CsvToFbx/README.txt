═══════════════════════════════════════════════════
  RenderDoc CSV → FBX 转换工具
═══════════════════════════════════════════════════

【功能说明】
  将 RenderDoc 导出的顶点 CSV 文件转换为 FBX ASCII 格式，
  可直接导入 Unity / Blender / Maya 等 DCC 工具。

  支持特性：
  - 自动检测多套 UV（TEXCOORD0~N）
  - 自动去重顶点并重建三角形索引
  - 支持 Position / Normal / Tangent / 多 UV 通道

【环境要求】
  - Python 3.6 或更高版本
  - 无需安装任何第三方库（纯标准库实现）
  
  Python 下载: https://www.python.org/downloads/
  安装时勾选 "Add Python to PATH"

【使用方式】

  方式一：双击 启动GUI.bat
    打开图形界面，选择文件后点击转换。

  方式二：拖放文件到 命令行转换.bat
    将 .csv 文件直接拖到 bat 图标上即可转换。

  方式三：命令行
    python csv_to_fbx.py input.csv [output.fbx] [mesh_name]

【文件说明】
  csv_to_fbx.py      - 核心转换逻辑（命令行版）
  csv_to_fbx_gui.py  - tkinter 图形界面
  启动GUI.bat         - 一键启动 GUI
  命令行转换.bat      - 命令行/拖放转换
  README.txt          - 本说明文件

═══════════════════════════════════════════════════
