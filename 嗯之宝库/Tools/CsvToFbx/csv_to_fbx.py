"""
将 RenderDoc 导出的 mesh CSV 转为 FBX 7.4 ASCII 格式。
自动识别 CSV 表头中的所有 TEXCOORD 通道，全部写入 FBX 的多层 UV。
支持 Position / Normal / Tangent / 多套 UV (TEXCOORD0~N)。

用法: python csv_to_fbx.py <input.csv> [output.fbx]
"""

import csv
import sys
import os
import re
import time

def parse_header_columns(header):
    """
    解析 RenderDoc CSV 表头，返回各通道的列索引映射。
    RenderDoc 表头格式示例:
      VTX, IDX, SV_POSITION.x, SV_POSITION.y, SV_POSITION.z, SV_POSITION.w,
      NORMAL.x, NORMAL.y, NORMAL.z, TANGENT.x, TANGENT.y, TANGENT.z, TANGENT.w,
      TEXCOORD0.x, TEXCOORD0.y, TEXCOORD1.x, TEXCOORD1.y, ...

    返回 dict:
      'vtx': int, 'idx': int,
      'pos': (ix, iy, iz),
      'normal': (ix, iy, iz) or None,
      'tangent': (ix, iy, iz, iw) or None,
      'uvs': [(ix, iy), ...] 按 TEXCOORD 编号排序
    """
    col_map = {h.upper(): i for i, h in enumerate(header)}

    # VTX / IDX
    vtx_col = None
    idx_col = None
    for i, h in enumerate(header):
        hu = h.upper()
        if hu == 'VTX':
            vtx_col = i
        elif hu == 'IDX':
            idx_col = i

    # Position: SV_POSITION 或 POSITION
    pos_cols = None
    for prefix in ('SV_POSITION', 'POSITION'):
        px = col_map.get(f'{prefix}.X')
        py = col_map.get(f'{prefix}.Y')
        pz = col_map.get(f'{prefix}.Z')
        if px is not None and py is not None and pz is not None:
            pos_cols = (px, py, pz)
            break

    # Normal
    normal_cols = None
    nx = col_map.get('NORMAL.X')
    ny = col_map.get('NORMAL.Y')
    nz = col_map.get('NORMAL.Z')
    if nx is not None and ny is not None and nz is not None:
        normal_cols = (nx, ny, nz)

    # Tangent
    tangent_cols = None
    tx = col_map.get('TANGENT.X')
    ty = col_map.get('TANGENT.Y')
    tz = col_map.get('TANGENT.Z')
    tw = col_map.get('TANGENT.W')
    if tx is not None and ty is not None and tz is not None and tw is not None:
        tangent_cols = (tx, ty, tz, tw)

    # TEXCOORD: 自动检测所有 TEXCOORD{N}.X / .Y（可能有 .Z .W）
    uv_map = {}  # channel_index -> (col_x, col_y, [col_z, col_w])
    texcoord_re = re.compile(r'^TEXCOORD(\d+)\.([XYZW])$')
    for i, h in enumerate(header):
        m = texcoord_re.match(h.upper())
        if m:
            ch = int(m.group(1))
            comp = m.group(2)
            if ch not in uv_map:
                uv_map[ch] = {}
            uv_map[ch][comp] = i

    # 按通道号排序，组装成 list of tuples
    uv_channels = []
    for ch in sorted(uv_map.keys()):
        comps = uv_map[ch]
        if 'X' in comps and 'Y' in comps:
            entry = [comps['X'], comps['Y']]
            if 'Z' in comps:
                entry.append(comps['Z'])
            if 'W' in comps:
                entry.append(comps['W'])
            uv_channels.append(tuple(entry))

    return {
        'vtx': vtx_col if vtx_col is not None else 0,
        'idx': idx_col if idx_col is not None else 1,
        'pos': pos_cols,
        'normal': normal_cols,
        'tangent': tangent_cols,
        'uvs': uv_channels,
    }


def read_csv(csv_path):
    """
    读取 RenderDoc CSV，自动识别表头中所有通道。
    返回 (ordered_verts, remapped_tris, uv_count)
      - ordered_verts: list of dict {'pos', 'normal', 'tangent', 'uvs': [tuple, ...]}
      - remapped_tris: list of (i0, i1, i2)
      - uv_count: int，UV 通道数
    """
    vertices = {}
    draw_indices = []

    with open(csv_path, 'r') as f:
        reader = csv.reader(f)
        header = [h.strip() for h in next(reader)]
        col_info = parse_header_columns(header)

        idx_col = col_info['idx']
        pos_cols = col_info['pos']
        normal_cols = col_info['normal']
        tangent_cols = col_info['tangent']
        uv_channels = col_info['uvs']

        if pos_cols is None:
            raise ValueError("CSV 中未找到 Position 列 (SV_POSITION 或 POSITION)")

        for row in reader:
            vals = [v.strip() for v in row]
            if len(vals) <= idx_col:
                continue
            idx = int(vals[idx_col])
            draw_indices.append(idx)

            if idx not in vertices:
                pos = (float(vals[pos_cols[0]]), float(vals[pos_cols[1]]), float(vals[pos_cols[2]]))

                normal = (0.0, 1.0, 0.0)
                if normal_cols:
                    normal = (float(vals[normal_cols[0]]), float(vals[normal_cols[1]]), float(vals[normal_cols[2]]))

                tangent = (1.0, 0.0, 0.0, 1.0)
                if tangent_cols:
                    tangent = (float(vals[tangent_cols[0]]), float(vals[tangent_cols[1]]),
                               float(vals[tangent_cols[2]]), float(vals[tangent_cols[3]]))

                uvs = []
                for uv_cols in uv_channels:
                    uv_data = tuple(float(vals[c]) for c in uv_cols)
                    uvs.append(uv_data)

                vertices[idx] = {
                    'pos': pos,
                    'normal': normal,
                    'tangent': tangent,
                    'uvs': uvs,
                }

    # 构建三角形
    triangles = []
    for i in range(0, len(draw_indices), 3):
        if i + 2 < len(draw_indices):
            triangles.append((draw_indices[i], draw_indices[i+1], draw_indices[i+2]))

    # 重建连续索引映射
    sorted_idxs = sorted(vertices.keys())
    idx_remap = {old: new for new, old in enumerate(sorted_idxs)}
    ordered_verts = [vertices[old_idx] for old_idx in sorted_idxs]
    remapped_tris = [(idx_remap[t[0]], idx_remap[t[1]], idx_remap[t[2]]) for t in triangles]

    return ordered_verts, remapped_tris, len(uv_channels)


def write_fbx_ascii(verts, tris, fbx_path, mesh_name="hair"):
    """写入 FBX 7.4 ASCII 文件，支持多套 UV"""
    num_verts = len(verts)
    num_tris = len(tris)
    num_indices = num_tris * 3

    # 检测 UV 通道数（从第一个顶点获取）
    uv_count = len(verts[0]['uvs']) if num_verts > 0 and 'uvs' in verts[0] else 0

    geom_id = 1000000000
    model_id = 2000000000

    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")

    with open(fbx_path, 'w') as f:
        f.write('; FBX 7.4.0 project file\n')
        f.write('; Converted from RenderDoc CSV\n')
        f.write(f'; {timestamp}\n')
        f.write(f'; UV channels: {uv_count}\n')
        f.write('; ---\n\n')

        f.write('FBXHeaderExtension:  {\n')
        f.write('\tFBXHeaderVersion: 1003\n')
        f.write('\tFBXVersion: 7400\n')
        f.write('\tCreationTimeStamp:  {\n')
        f.write('\t\tVersion: 1000\n')
        t = time.localtime()
        f.write(f'\t\tYear: {t.tm_year}\n')
        f.write(f'\t\tMonth: {t.tm_mon}\n')
        f.write(f'\t\tDay: {t.tm_mday}\n')
        f.write(f'\t\tHour: {t.tm_hour}\n')
        f.write(f'\t\tMinute: {t.tm_min}\n')
        f.write(f'\t\tSecond: {t.tm_sec}\n')
        f.write(f'\t\tMillisecond: 0\n')
        f.write('\t}\n')
        f.write('\tCreator: "csv_to_fbx converter"\n')
        f.write('}\n\n')

        f.write('GlobalSettings:  {\n')
        f.write('\tVersion: 1000\n')
        f.write('\tProperties70:  {\n')
        f.write('\t\tP: "UpAxis", "int", "Integer", "",1\n')
        f.write('\t\tP: "UpAxisSign", "int", "Integer", "",1\n')
        f.write('\t\tP: "FrontAxis", "int", "Integer", "",2\n')
        f.write('\t\tP: "FrontAxisSign", "int", "Integer", "",1\n')
        f.write('\t\tP: "CoordAxis", "int", "Integer", "",0\n')
        f.write('\t\tP: "CoordAxisSign", "int", "Integer", "",1\n')
        f.write('\t\tP: "OriginalUpAxis", "int", "Integer", "",1\n')
        f.write('\t\tP: "OriginalUpAxisSign", "int", "Integer", "",1\n')
        f.write('\t\tP: "UnitScaleFactor", "double", "Number", "",1\n')
        f.write('\t\tP: "OriginalUnitScaleFactor", "double", "Number", "",1\n')
        f.write('\t}\n')
        f.write('}\n\n')

        f.write('Definitions:  {\n')
        f.write('\tVersion: 100\n')
        f.write('\tCount: 3\n')
        f.write('\tObjectType: "GlobalSettings" {\n')
        f.write('\t\tCount: 1\n')
        f.write('\t}\n')
        f.write('\tObjectType: "Geometry" {\n')
        f.write('\t\tCount: 1\n')
        f.write('\t\tPropertyTemplate: "FbxMesh" {\n')
        f.write('\t\t\tProperties70:  {\n')
        f.write('\t\t\t}\n')
        f.write('\t\t}\n')
        f.write('\t}\n')
        f.write('\tObjectType: "Model" {\n')
        f.write('\t\tCount: 1\n')
        f.write('\t\tPropertyTemplate: "FbxNode" {\n')
        f.write('\t\t\tProperties70:  {\n')
        f.write('\t\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",0,0,0\n')
        f.write('\t\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",0,0,0\n')
        f.write('\t\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1\n')
        f.write('\t\t\t}\n')
        f.write('\t\t}\n')
        f.write('\t}\n')
        f.write('}\n\n')

        f.write('Objects:  {\n')

        f.write(f'\tGeometry: {geom_id}, "Geometry::{mesh_name}", "Mesh" {{\n')

        # Vertices
        f.write(f'\t\tVertices: *{num_verts * 3} {{\n')
        f.write('\t\t\ta: ')
        pos_strs = []
        for v in verts:
            p = v['pos']
            pos_strs.extend([f'{p[0]:.6f}', f'{p[1]:.6f}', f'{p[2]:.6f}'])
        f.write(','.join(pos_strs))
        f.write('\n\t\t}\n')

        # PolygonVertexIndex
        f.write(f'\t\tPolygonVertexIndex: *{num_indices} {{\n')
        f.write('\t\t\ta: ')
        idx_strs = []
        for tri in tris:
            idx_strs.append(str(tri[0]))
            idx_strs.append(str(tri[1]))
            idx_strs.append(str(-(tri[2]) - 1))
        f.write(','.join(idx_strs))
        f.write('\n\t\t}\n')

        f.write('\t\tGeometryVersion: 124\n')

        # LayerElementNormal
        f.write('\t\tLayerElementNormal: 0 {\n')
        f.write('\t\t\tVersion: 102\n')
        f.write('\t\t\tName: "Normals"\n')
        f.write('\t\t\tMappingInformationType: "ByPolygonVertex"\n')
        f.write('\t\t\tReferenceInformationType: "Direct"\n')
        f.write(f'\t\t\tNormals: *{num_indices * 3} {{\n')
        f.write('\t\t\t\ta: ')
        nrm_strs = []
        for tri in tris:
            for vi in tri:
                n = verts[vi]['normal']
                nrm_strs.extend([f'{n[0]:.6f}', f'{n[1]:.6f}', f'{n[2]:.6f}'])
        f.write(','.join(nrm_strs))
        f.write('\n\t\t\t}\n')
        f.write('\t\t}\n')

        # LayerElementTangent
        f.write('\t\tLayerElementTangent: 0 {\n')
        f.write('\t\t\tVersion: 102\n')
        f.write('\t\t\tName: "Tangents"\n')
        f.write('\t\t\tMappingInformationType: "ByPolygonVertex"\n')
        f.write('\t\t\tReferenceInformationType: "Direct"\n')
        f.write(f'\t\t\tTangents: *{num_indices * 3} {{\n')
        f.write('\t\t\t\ta: ')
        tan_strs = []
        for tri in tris:
            for vi in tri:
                tg = verts[vi]['tangent']
                tan_strs.extend([f'{tg[0]:.6f}', f'{tg[1]:.6f}', f'{tg[2]:.6f}'])
        f.write(','.join(tan_strs))
        f.write('\n\t\t\t}\n')

        f.write(f'\t\t\tTangentsW: *{num_indices} {{\n')
        f.write('\t\t\t\ta: ')
        tanw_strs = []
        for tri in tris:
            for vi in tri:
                tg = verts[vi]['tangent']
                tanw_strs.append(f'{tg[3]:.6f}')
        f.write(','.join(tanw_strs))
        f.write('\n\t\t\t}\n')
        f.write('\t\t}\n')

        # 多套 LayerElementUV
        for uv_idx in range(uv_count):
            uv_name = f"UVMap" if uv_idx == 0 else f"UVMap{uv_idx}"
            # 每个 UV 通道取前 2 个分量写入 FBX（FBX UV 只支持 2D）
            f.write(f'\t\tLayerElementUV: {uv_idx} {{\n')
            f.write('\t\t\tVersion: 101\n')
            f.write(f'\t\t\tName: "{uv_name}"\n')
            f.write('\t\t\tMappingInformationType: "ByPolygonVertex"\n')
            f.write('\t\t\tReferenceInformationType: "Direct"\n')
            f.write(f'\t\t\tUV: *{num_indices * 2} {{\n')
            f.write('\t\t\t\ta: ')
            uv_strs = []
            for tri in tris:
                for vi in tri:
                    uv_data = verts[vi]['uvs'][uv_idx] if uv_idx < len(verts[vi]['uvs']) else (0.0, 0.0)
                    uv_strs.extend([f'{uv_data[0]:.6f}', f'{uv_data[1]:.6f}'])
            f.write(','.join(uv_strs))
            f.write('\n\t\t\t}\n')
            f.write('\t\t}\n')

        # Layer 0（包含 Normal + Tangent + UV0）
        f.write('\t\tLayer: 0 {\n')
        f.write('\t\t\tVersion: 100\n')
        f.write('\t\t\tLayerElement:  {\n')
        f.write('\t\t\t\tType: "LayerElementNormal"\n')
        f.write('\t\t\t\tTypedIndex: 0\n')
        f.write('\t\t\t}\n')
        f.write('\t\t\tLayerElement:  {\n')
        f.write('\t\t\t\tType: "LayerElementTangent"\n')
        f.write('\t\t\t\tTypedIndex: 0\n')
        f.write('\t\t\t}\n')
        if uv_count > 0:
            f.write('\t\t\tLayerElement:  {\n')
            f.write('\t\t\t\tType: "LayerElementUV"\n')
            f.write('\t\t\t\tTypedIndex: 0\n')
            f.write('\t\t\t}\n')
        f.write('\t\t}\n')

        # Layer 1, 2, ... 给额外的 UV 通道
        for uv_idx in range(1, uv_count):
            f.write(f'\t\tLayer: {uv_idx} {{\n')
            f.write('\t\t\tVersion: 100\n')
            f.write('\t\t\tLayerElement:  {\n')
            f.write('\t\t\t\tType: "LayerElementUV"\n')
            f.write(f'\t\t\t\tTypedIndex: {uv_idx}\n')
            f.write('\t\t\t}\n')
            f.write('\t\t}\n')

        f.write('\t}\n')  # end Geometry

        # Model
        f.write(f'\tModel: {model_id}, "Model::{mesh_name}", "Mesh" {{\n')
        f.write('\t\tVersion: 232\n')
        f.write('\t\tProperties70:  {\n')
        f.write('\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",0,0,0\n')
        f.write('\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",0,0,0\n')
        f.write('\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1\n')
        f.write('\t\t}\n')
        f.write('\t\tShading: T\n')
        f.write('\t\tCulling: "CullingOff"\n')
        f.write('\t}\n')

        f.write('}\n\n')  # end Objects

        # Connections
        f.write('Connections:  {\n')
        f.write(f'\tC: "OO",{model_id},0\n')
        f.write(f'\tC: "OO",{geom_id},{model_id}\n')
        f.write('}\n')

    print(f'[OK] FBX 已导出: {fbx_path}')
    print(f'     顶点数: {num_verts}')
    print(f'     三角形数: {num_tris}')
    print(f'     UV 通道数: {uv_count}')
    print(f'     索引数: {num_indices}')


def main():
    if len(sys.argv) < 2:
        print('用法: python csv_to_fbx.py <input.csv> [output.fbx] [mesh_name]')
        print('  input.csv  - RenderDoc 导出的顶点 CSV')
        print('  output.fbx - 输出 FBX 路径 (默认同名)')
        print('  mesh_name  - 网格名称 (默认文件名)')
        sys.exit(1)

    csv_path = sys.argv[1]

    if len(sys.argv) >= 3:
        fbx_path = sys.argv[2]
    else:
        fbx_path = os.path.splitext(csv_path)[0] + '.fbx'

    if len(sys.argv) >= 4:
        mesh_name = sys.argv[3]
    else:
        mesh_name = os.path.splitext(os.path.basename(csv_path))[0]

    print(f'[INFO] 读取 CSV: {csv_path}')
    verts, tris, uv_count = read_csv(csv_path)
    print(f'[INFO] 解析完成: {len(verts)} 个唯一顶点, {len(tris)} 个三角形, {uv_count} 套 UV')

    write_fbx_ascii(verts, tris, fbx_path, mesh_name)


if __name__ == '__main__':
    main()
