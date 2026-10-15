"""Read-only MikkTSpace calculation. Does not change Max normals, UV1 or tangents."""
import ctypes
import os
import math
from outline_math import cross, dot, unit, mul, sub

_library = None

class TangentFrameError(ValueError):
    def __init__(self, face_index, corner, message):
        super().__init__(message)
        self.face_index = face_index
        self.corner = corner


def corner_frames(positions, faces, normals, uv_faces, uv_vertices):
    global _library
    if _library is None:
        path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tl_mikktspace.dll')
        if not os.path.isfile(path):
            raise RuntimeError('缺少 tl_mikktspace.dll，请完整解压新版工具包。')
        _library = ctypes.CDLL(path)
        _library.tl_mikk_generate.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_float), ctypes.POINTER(ctypes.c_float)]
        _library.tl_mikk_generate.restype = ctypes.c_int
        _library.tl_mikk_version.restype = ctypes.c_int
        if _library.tl_mikk_version() != 1:
            raise RuntimeError('切线计算库版本不匹配，请完整更新工具包。')
    count = len(faces) * 3
    source = (ctypes.c_float * (count * 8))()
    target = (ctypes.c_float * (count * 4))()
    for fi, face in enumerate(faces):
        for corner, vi in enumerate(face):
            uv_index = uv_faces[fi][corner]
            if not 0 <= uv_index < len(uv_vertices):
                raise TangentFrameError(fi, corner, '第一套 UV 顶点索引无效：三角面 #%s / 面角 %s，UV 顶点 #%s。' % (fi + 1, corner + 1, uv_index + 1))
            values = positions[vi] + normals[fi][corner] + uv_vertices[uv_index][:2]
            if not all(math.isfinite(value) for value in values):
                raise TangentFrameError(fi, corner, '切线输入包含 NaN/Infinity：三角面 #%s / 面角 %s。' % (fi + 1, corner + 1))
            start = (fi * 3 + corner) * 8
            for j, value in enumerate(values):
                source[start + j] = value
    if not _library.tl_mikk_generate(len(faces), source, target):
        raise ValueError('第一套 UV 无法生成有效 MikkTSpace 切线，请检查网格。')
    frames = []
    for i in range(count):
        n = normals[i // 3][i % 3]
        tangent = tuple(target[i * 4 + j] for j in range(3))
        t = unit(sub(tangent, mul(n, dot(n, tangent))))
        if dot(t, t) < .5:
            raise TangentFrameError(i // 3, i % 3, '第一套 UV 存在无法生成切线的面：三角面 #%s / 面角 %s；法线 %s，计算切线 %s。' % (i // 3 + 1, i % 3 + 1, n, tangent))
        b = mul(cross(n, t), target[i * 4 + 3])
        frames.append((t, b, n))
    return frames

def to_tangent(direction, frame):
    return unit(tuple(dot(direction, axis) for axis in frame))

def from_tangent(direction, frame):
    return unit(tuple(sum(direction[j] * frame[j][i] for j in range(3)) for i in range(3)))


def unity_corner_frames(positions, faces, normals, uv_faces, uv_vertices, face_ids=None):
    """Predict Unity's default FBX import basis, returned in Max local space.

    Unity reflects local X and reverses winding. Ordinary UV derivatives are
    covariant, but Mikk's fixed-axis fallback on UV-degenerate triangles is not.
    Calculate after that conversion, then reflect the frame axes back. This
    also preserves Mikk's fallback handedness rather than guessing its sign.
    Requires Unity ModelImporter.bakeAxisConversion=False (the default).
    """
    flip = lambda p: (-p[0], p[1], p[2])
    reverse = lambda f: (f[0], f[2], f[1])
    try:
        imported = corner_frames(
            [flip(p) for p in positions], [reverse(f) for f in faces],
            [[flip(n) for n in reverse(face)] for face in normals],
            [reverse(f) for f in uv_faces], uv_vertices)
    except TangentFrameError as error:
        fi, ci = error.face_index, (0, 2, 1)[error.corner]
        original_fi = face_ids[fi] if face_ids is not None else fi
        ids = uv_faces[fi]
        uv_values = [uv_vertices[i] if 0 <= i < len(uv_vertices) else '索引无效' for i in ids]
        reason = '索引或数值无效' if '索引' in str(error) or 'NaN' in str(error) else '无法生成有效切线'
        raise ValueError('阶段：第一套 UV 切线计算，%s。\n三角面 #%s / 面角 %s / 几何顶点 #%s。\nUV 顶点编号 %s；UVW %s；原始法线 %s。' % (
            reason, original_fi + 1, ci + 1, faces[fi][ci] + 1, tuple(i + 1 for i in ids), uv_values, normals[fi][ci])) from error
    return [tuple(flip(axis) for axis in imported[fi * 3 + (0, 2, 1)[c]])
            for fi in range(len(faces)) for c in range(3)]
