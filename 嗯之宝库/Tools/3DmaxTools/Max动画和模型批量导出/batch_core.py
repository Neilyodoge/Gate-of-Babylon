"""Character.max -> model; Character@clip.max -> animation. No Max dependency."""
import json
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET

SCHEMA_VERSION = 2


def normalized(path):
    return os.path.normcase(str(Path(path).resolve()))


def make_jobs(source, output, recursive=False):
    source, output = Path(source).resolve(), Path(output).resolve()
    if not source.is_dir():
        raise ValueError('请选择有效的 MAX 文件夹。')
    if output.exists() and not output.is_dir():
        raise ValueError('输出路径不是文件夹。')
    candidates = source.rglob('*') if recursive else source.iterdir()
    files = sorted((p for p in candidates if p.is_file() and p.suffix.lower() == '.max'
                    and not (output != source and output in p.parents)),
                   key=lambda p: str(p.relative_to(source)).casefold())
    jobs, destinations = [], set()
    for scene in files:
        character, separator, clip = scene.stem.partition('@')
        kind = 'animation' if separator else 'model'
        issue = ''
        if not character or (separator and not clip):
            issue = '文件名应为角色名.max 或 角色名@动作名.max'
        relative = scene.relative_to(source)
        target = output / relative.parent / character / ('Animations' if separator else 'Model') / (scene.stem + '.fbx')
        if normalized(target) in destinations:
            raise ValueError('多个 MAX 会导出到同一路径：' + str(target))
        destinations.add(normalized(target))
        jobs.append({'source': str(scene), 'target': str(target), 'kind': kind,
                     'character': character, 'clip': clip, 'issue': issue})
    return sorted(jobs, key=lambda j: (str(Path(j['source']).parent).casefold(),
                                       j['character'].casefold(), j['kind'] != 'model', j['clip'].casefold()))


def validate_preset(path):
    path = Path(path)
    if not path.is_file() or path.suffix.lower() != '.fbxexportpreset':
        raise ValueError('请选择已保存的 FBX 导出预设（.fbxexportpreset）：' + str(path))
    root = ET.parse(path).getroot()
    if root.tag != 'Export':
        raise ValueError('这不是 FBX 导出预设：' + str(path))
    return str(path.resolve())


def save_config(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent,
                                     prefix='.mt-config-', delete=False) as stream:
        staging = Path(stream.name)
        json.dump(dict(data, schema_version=SCHEMA_VERSION), stream, ensure_ascii=False, indent=2)
    try:
        os.replace(staging, path)
    finally:
        staging.unlink(missing_ok=True)


def load_config(path):
    data = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if not isinstance(data, dict) or data.get('schema_version') != SCHEMA_VERSION:
        raise ValueError('配置版本不匹配；旧版记录模板不适用于角色 / 动作导出。')
    return data


def publish_export(staging_dir, target):
    """FBX is committed last; Windows rename refuses to replace existing files."""
    staging_dir, target = Path(staging_dir).resolve(), Path(target).resolve()
    fbx = staging_dir / target.name
    if not fbx.is_file() or fbx.stat().st_size < 32:
        raise RuntimeError('导出器没有生成有效的 FBX 文件。')
    staged = sorted(staging_dir.iterdir(), key=lambda p: p == fbx)
    pairs = [(p, target.parent / p.name) for p in staged]
    for _, dst in pairs:
        if dst.exists():
            raise FileExistsError('目标已存在，未覆盖：' + str(dst))
    moved = []
    try:
        for src, dst in pairs:
            os.rename(src, dst)
            moved.append((src, dst))
    except Exception:
        for src, dst in reversed(moved):
            os.rename(dst, src)
        raise


