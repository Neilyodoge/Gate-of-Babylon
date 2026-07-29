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

  方式一（推荐）：双击 启动GUI.vbs
    静默启动图形界面，不会弹出黑色 cmd 窗口。
    若未安装 Python 或启动失败，会自动弹窗提示。

  方式二：双击 run_gui.bat
    同样以无窗口方式启动 GUI（内部会调用 启动GUI.vbs / pythonw）。

  方式三：拖放文件到 run_cmd.bat
    将 .csv 文件直接拖到 bat 图标上即可转换（命令行版，会显示日志窗口）。

  方式四：命令行
    python csv_to_fbx.py input.csv [output.fbx] [mesh_name]

【文件说明】
  csv_to_fbx.py      - 核心转换逻辑（命令行版）
  csv_to_fbx_gui.py  - tkinter 图形界面（含启动失败弹窗提示）
  启动GUI.vbs         - 无窗口静默启动 GUI（推荐）
  run_gui.bat         - 一键启动 GUI（无 cmd 窗口）
  run_cmd.bat         - 命令行/拖放转换（显示日志窗口）
  README.txt          - 本说明文件

═══════════════════════════════════════════════════
