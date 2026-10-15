"""纯 Python 角度加权算法；不依赖 Max，便于验证。"""
import math


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def mul(a, scale):
    return tuple(x * scale for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def unit(a):
    length = math.sqrt(dot(a, a))
    return mul(a, 1.0 / length) if length > 1e-20 else (0.0, 0.0, 0.0)


def oct_encode(normal):
    """单位方向编码成两个分量；U+2 标记新版 Max 切线空间格式；旧 Unity SmoothN 使用 UV1.xyz。"""
    x, y, z = unit(normal)
    scale = abs(x) + abs(y) + abs(z)
    if scale < 1e-20:
        raise ValueError('不能编码零法线。')
    x, y, z = x / scale, y / scale, z / scale
    if z < 0:
        x, y = ((1 - abs(y)) * (1 if x >= 0 else -1),
                (1 - abs(x)) * (1 if y >= 0 else -1))
    return (x * .5 + 2.5, y * .5 + .5)


def oct_decode(encoded):
    x, y = (encoded[0] - 2.5) * 2, encoded[1] * 2 - 1
    z = 1 - abs(x) - abs(y)
    if z < 0:
        x, y = ((1 - abs(y)) * (1 if x >= 0 else -1),
                (1 - abs(x)) * (1 if y >= 0 else -1))
    return unit((x, y, z))


def is_degenerate_triangle(vertices, face):
    """Same scale-independent geometric criterion as the smooth-normal baker."""
    a, b, c = (vertices[i] for i in face)
    raw = cross(unit(sub(b, a)), unit(sub(c, a)))
    return dot(raw, raw) < 1e-20


def smooth_normals(vertices, faces, skip_unused=False):
    """与项目相同：精确同位置分组，忽略硬边、UV 缝和平滑组。"""
    sums = {}
    active = set()
    skipped = 0
    for face in faces:
        a, b, c = (vertices[i] for i in face)
        e1, e2 = unit(sub(b, a)), unit(sub(c, a))
        raw = cross(e1, e2)
        if is_degenerate_triangle(vertices, face):
            skipped += 1
            continue
        active.update(face)
        normal = unit(raw)
        for corner, index in enumerate(face):
            position = vertices[index]
            u = unit(sub(vertices[face[(corner + 1) % 3]], position))
            v = unit(sub(vertices[face[(corner + 2) % 3]], position))
            angle = math.acos(max(-1.0, min(1.0, dot(u, v))))
            sums[position] = add(sums.get(position, (0.0, 0.0, 0.0)), mul(normal, angle))
    normals = [unit(sums.get(position, (0.0, 0.0, 0.0))) for position in vertices]
    invalid = [i for i, n in enumerate(normals) if (not skip_unused or i in active) and dot(n, n) < .5]
    if invalid:
        details = ['几何顶点 #%s，位置 %s，关联三角面 %s' % (i + 1, vertices[i], [fi + 1 for fi, face in enumerate(faces) if i in face][:6]) for i in invalid[:5]]
        raise ValueError(('有效面的平滑法线相互抵消，无法烘焙；请检查重叠反向面和原始法线。' if skip_unused else
                          '存在孤立点或相互抵消的法线；请清理网格中的孤立点、重叠反向面。') + '\n' + '\n'.join(details))
    return normals, skipped


def transport_frame(vertices, faces):
    """选一个稳定三角形作为公共坐标基，避免硬编码 Max/FBX/Unity 轴转换。"""
    best = None
    best_area = 0.0
    for index, face in enumerate(faces):
        a, b, c = (vertices[i] for i in face)
        ab, ac = sub(b, a), sub(c, a)
        area = dot(cross(ab, ac), cross(ab, ac))
        if area > best_area:
            best, best_area = index, area
    if best is None:
        raise ValueError('没有有效三角面。')
    a, b, c = (vertices[i] for i in faces[best])
    t = unit(sub(b, a))
    ac = sub(c, a)
    b = unit(sub(ac, mul(t, dot(ac, t))))
    n = unit(cross(t, b))
    return best, (t, b, n)


def pack_normals(vertices, faces):
    normals, skipped = smooth_normals(vertices, faces)
    reference, frame = transport_frame(vertices, faces)
    packed = [tuple(dot(n, axis) for axis in frame) for n in normals]
    return packed, reference, skipped
