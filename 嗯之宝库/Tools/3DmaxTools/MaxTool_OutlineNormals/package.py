"""Package only the files needed to run the 3ds Max tool."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parent
runtime_files = (
    'Launch.ms', 'tl_outline_max.py', 'outline_math.py',
    'outline_preview.py', 'tangent_space.py', 'tl_mikktspace.dll',
)
archive_path = root.parent / 'MaxTool_OutlineNormals.zip'
with ZipFile(archive_path, 'w', ZIP_DEFLATED) as archive:
    for name in runtime_files:
        archive.write(root / name, name)
print(archive_path)
