-- 在 3ds Max 中运行本文件，打开平滑法线 UV 工具。
(
    local toolPath = (getFilenamePath (getSourceFileName())) + "tl_outline_max.py"
    toolPath = substituteString toolPath "\\" "/"
    toolPath = substituteString toolPath "'" "\\'"
    -- run_path 显式设置 __file__，避免 ExecuteFile 沿用上一个 Python 脚本目录。
    python.Execute ("import runpy; runpy.run_path('" + toolPath + "', run_name='__main__')")
)
