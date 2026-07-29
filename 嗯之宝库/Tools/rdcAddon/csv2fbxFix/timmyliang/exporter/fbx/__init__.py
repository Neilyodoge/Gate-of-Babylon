# -*- coding: utf-8 -*-
"""
FBX Exporter
"""

from __future__ import division
from __future__ import print_function
from __future__ import absolute_import

__author__ = "timmyliang"
__email__ = "820472580@qq.com"
__date__ = "2021-01-26 20:44:17"


import os
import time
import json
import struct
import inspect
from textwrap import dedent
from functools import partial
from collections import defaultdict

from PySide2 import QtWidgets, QtCore

import qrenderdoc

from .query_dialog import QueryDialog
from .progress_dialog import MProgressDialog

FBX_ASCII_TEMPLETE = """
    ; FBX 7.3.0 project file
    ; ----------------------------------------------------

    ; Object definitions
    ;------------------------------------------------------------------

    Definitions:  {

        ObjectType: "Geometry" {
            Count: 1
            PropertyTemplate: "FbxMesh" {
                Properties70:  {
                    P: "Primary Visibility", "bool", "", "",1
                }
            }
        }

        ObjectType: "Model" {
            Count: 1
            PropertyTemplate: "FbxNode" {
                Properties70:  {
                    P: "Visibility", "Visibility", "", "A",1
                }
            }
        }
    }

    ; Object properties
    ;------------------------------------------------------------------

    Objects:  {
        Geometry: 2035541511296, "Geometry::", "Mesh" {
            Vertices: *%(vertices_num)s {
                a: %(vertices)s
            } 
            PolygonVertexIndex: *%(polygons_num)s {
                a: %(polygons)s
            } 
            GeometryVersion: 124
            %(LayerElementNormal)s
            %(LayerElementBiNormal)s
            %(LayerElementTangent)s
            %(LayerElementColor)s
            %(LayerElementUV)s
            %(LayerElementUV2)s
            Layer: 0 {
                Version: 100
                %(LayerElementNormalInsert)s
                %(LayerElementBiNormalInsert)s
                %(LayerElementTangentInsert)s
                %(LayerElementColorInsert)s
                %(LayerElementUVInsert)s
                
            }
            Layer: 1 {
                Version: 100
                %(LayerElementUV2Insert)s
            }
        }
        Model: 2035615390896, "Model::%(model_name)s", "Mesh" {
            Properties70:  {
                P: "DefaultAttributeIndex", "int", "Integer", "",0
            }
        }
    }

    ; Object connections
    ;------------------------------------------------------------------

    Connections:  {
        
        ;Model::pCube1, Model::RootNode
        C: "OO",2035615390896,0
        
        ;Geometry::, Model::pCube1
        C: "OO",2035541511296,2035615390896

    }

    """


def export_fbx(save_path, mapper, data, attr_list, controller):

    if not data:
        # manager.ErrorDialog("Current Draw Call lack of Vertex. ", "Error")
        return

    save_name = os.path.basename(os.path.splitext(save_path)[0])

    # We'll decode the first three indices making up a triangle
    idx_dict = data["IDX"]
    # NOTE 索引兜底：VS Input 表没有 IDX 列（非索引绘制）时，退化用 VTX 或顺序索引，
    #      否则下面的顶点收集循环不会执行，导出会是空网格。
    if not idx_dict:
        idx_dict = data.get("VTX")
    if not idx_dict:
        _row_count = 0
        for _a in attr_list:
            _row_count = len(data[_a])
            break
        idx_dict = list(range(_row_count))
    value_dict = defaultdict(list)
    vertex_data = defaultdict(dict)

    for i, idx in enumerate(idx_dict):
        for attr in attr_list:
            value = data[attr][i]
            value_dict[attr].append(value)
            if idx not in vertex_data[attr]:
                vertex_data[attr][idx] = value

    ARGS = {
        "model_name": save_name,
        "LayerElementNormal": "",
        "LayerElementNormalInsert": "",
        "LayerElementBiNormal": "",
        "LayerElementBiNormalInsert": "",
        "LayerElementTangent": "",
        "LayerElementTangentInsert": "",
        "LayerElementColor": "",
        "LayerElementColorInsert": "",
        "LayerElementUV": "",
        "LayerElementUVInsert": "",
        "LayerElementUV2": "",
        "LayerElementUV2Insert": "",
    }

    POSITION = mapper.get("POSITION")
    NORMAL = mapper.get("NORMAL")
    BINORMAL = mapper.get("BINORMAL")
    TANGENT = mapper.get("TANGENT")
    COLOR = mapper.get("COLOR")
    UV = mapper.get("UV")
    UV2 = mapper.get("UV2")
    ENGINE = mapper.get("ENGINE")

    min_poly = min(idx_dict) if idx_dict else 0
    idx_list = [idx - min_poly for idx in idx_dict]
    # idx_data = ",".join([str(idx) for idx in idx_list])
    idx_len = len(idx_list)

    class ProcessHandler(object):
        def run(self):
            curr = time.time()
            for name, func in inspect.getmembers(self, inspect.isroutine):
                if name.startswith("run_"):
                    func()
            print("elapsed time template: %s" % (time.time() - curr))

        def run_vertices(self):
            vertices = [str(v) for idx, values in sorted(vertex_data[POSITION].items()) for v in values[:3]]
            ARGS["vertices"] = ",".join(vertices)
            ARGS["vertices_num"] = len(vertices)

        def run_polygons(self):
            polygons = [str(idx ^ -1 if i % 3 == 2 else idx) for i, idx in enumerate(idx_list)]
            ARGS["polygons"] = ",".join(polygons)
            ARGS["polygons_num"] = len(polygons)

        def run_normals(self):
            if not vertex_data.get(NORMAL):
                return

            # NOTE FBX_ASCII only support 3 dimension
            normals = [str(v) for values in value_dict[NORMAL] for v in values[:3]]

            ARGS[
                "LayerElementNormal"
            ] = """
                LayerElementNormal: 0 {
                    Version: 101
                    Name: ""
                    MappingInformationType: "ByPolygonVertex"
                    ReferenceInformationType: "Direct"
                    Normals: *%(normals_num)s {
                        a: %(normals)s
                    } 
                }
            """ % {
                "normals": ",".join(normals),
                "normals_num": len(normals),
            }
            ARGS[
                "LayerElementNormalInsert"
            ] = """
                LayerElement:  {
                        Type: "LayerElementNormal"
                    TypedIndex: 0
                }
            """

        def run_binormals(self):
            if not vertex_data.get(BINORMAL):
                return
            # NOTE FBX_ASCII only support 3 dimension
            binormals = [str(-v) for values in value_dict[BINORMAL] for v in values[:3]]

            ARGS[
                "LayerElementBiNormal"
            ] = """
                LayerElementBinormal: 0 {
                    Version: 101
                    Name: "map1"
                    MappingInformationType: "ByVertice"
                    ReferenceInformationType: "Direct"
                    Binormals: *%(binormals_num)s {
                        a: %(binormals)s
                    } 
                    BinormalsW: *%(binormalsW_num)s {
                        a: %(binormalsW)s
                    } 
                }
            """ % {
                "binormals": ",".join(binormals),
                "binormals_num": len(binormals),
                "binormalsW": ",".join(["1" for i in range(idx_len)]),
                "binormalsW_num": idx_len,
            }
            ARGS[
                "LayerElementBiNormalInsert"
            ] = """
                LayerElement:  {
                        Type: "LayerElementBinormal"
                    TypedIndex: 0
                }
            """

        def run_tangents(self):
            if not vertex_data.get(TANGENT):
                return

            tangents = [str(v) for values in value_dict[TANGENT] for v in values[:3]]

            ARGS[
                "LayerElementTangent"
            ] = """
                LayerElementTangent: 0 {
                    Version: 101
                    Name: "map1"
                    MappingInformationType: "ByPolygonVertex"
                    ReferenceInformationType: "Direct"
                    Tangents: *%(tangents_num)s {
                        a: %(tangents)s
                    } 
                }
            """ % {
                "tangents": ",".join(tangents),
                "tangents_num": len(tangents),
            }

            ARGS[
                "LayerElementTangentInsert"
            ] = """
                    LayerElement:  {
                        Type: "LayerElementTangent"
                        TypedIndex: 0
                    }
            """

        def run_color(self):
            if not vertex_data.get(COLOR):
                return

            colors = [
                # str(v) if i % 4 else "1"
                str(v)
                for values in value_dict[COLOR]
                for i, v in enumerate(values, 1)
            ]

            ARGS[
                "LayerElementColor"
            ] = """
                LayerElementColor: 0 {
                    Version: 101
                    Name: "colorSet1"
                    MappingInformationType: "ByPolygonVertex"
                    ReferenceInformationType: "IndexToDirect"
                    Colors: *%(colors_num)s {
                        a: %(colors)s
                    } 
                    ColorIndex: *%(colors_indices_num)s {
                        a: %(colors_indices)s
                    } 
                }
            """ % {
                "colors": ",".join(colors),
                "colors_num": len(colors),
                "colors_indices": ",".join([str(i) for i in range(idx_len)]),
                "colors_indices_num": idx_len,
            }
            ARGS[
                "LayerElementColorInsert"
            ] = """
                LayerElement:  {
                    Type: "LayerElementColor"
                    TypedIndex: 0
                }
            """

        def run_uv(self):
            if not vertex_data.get(UV):
                return

            uvs_indices = ",".join([str(idx) for idx in idx_list])
            uvs = [
                # NOTE flip y axis
                str(1 - v if i else v)
                for idx, values in sorted(vertex_data[UV].items())
                for i, v in enumerate(values)
            ]

            ARGS[
                "LayerElementUV"
            ] = """
                LayerElementUV: 0 {
                    Version: 101
                    Name: "map1"
                    MappingInformationType: "ByPolygonVertex"
                    ReferenceInformationType: "IndexToDirect"
                    UV: *%(uvs_num)s {
                        a: %(uvs)s
                    } 
                    UVIndex: *%(uvs_indices_num)s {
                        a: %(uvs_indices)s
                    } 
                }
            """ % {
                "uvs": ",".join(uvs),
                "uvs_num": len(uvs),
                "uvs_indices": uvs_indices,
                "uvs_indices_num": idx_len,
            }

            ARGS[
                "LayerElementUVInsert"
            ] = """
                LayerElement:  {
                    Type: "LayerElementUV"
                    TypedIndex: 0
                }
            """

        def run_uv2(self):
            if not vertex_data.get(UV2):
                return

            uvs_indices = ",".join([str(idx) for idx in idx_list])
            uvs = [
                # NOTE flip y axis
                str(1 - v if i else v)
                for idx, values in sorted(vertex_data[UV2].items())
                for i, v in enumerate(values)
            ]

            ARGS[
                "LayerElementUV2"
            ] = """
                LayerElementUV: 1 {
                    Version: 101
                    Name: "map2"
                    MappingInformationType: "ByPolygonVertex"
                    ReferenceInformationType: "IndexToDirect"
                    UV: *%(uvs_num)s {
                        a: %(uvs)s
                    } 
                    UVIndex: *%(uvs_indices_num)s {
                        a: %(uvs_indices)s
                    } 
                }
            """ % {
                "uvs": ",".join(uvs),
                "uvs_num": len(uvs),
                "uvs_indices": uvs_indices,
                "uvs_indices_num": idx_len,
            }

            ARGS[
                "LayerElementUV2Insert"
            ] = """
                LayerElement:  {
                    Type: "LayerElementUV"
                    TypedIndex: 1
                }
            """

    handler = ProcessHandler()
    handler.run()

    fbx = FBX_ASCII_TEMPLETE % ARGS

    with open(save_path, "w") as f:
        f.write(dedent(fbx).strip())


def error_log(func):
    def wrapper(pyrenderdoc, data):
        manager = pyrenderdoc.Extensions()
        try:
            func(pyrenderdoc, data)
        except:
            import traceback

            manager.MessageDialog("FBX Ouput Fail\n%s" % traceback.format_exc(), "Error!~")

    return wrapper


def collect_attr_info(model, column_count, row_count):
    """从 VS Input 表收集每个属性的分量列表与首行采样值，供对话框自动识别。

    返回 OrderedDict: base_name -> {"comps": ["X","Y",...], "sample": [float,...]}
    """
    from collections import OrderedDict

    info = OrderedDict()
    sample_row = 0 if row_count > 0 else -1
    for c in range(column_count):
        head = model.headerData(c, QtCore.Qt.Horizontal)
        if head is None:
            continue
        head = str(head)
        if "." not in head:
            continue  # 跳过 VTX / IDX 等无分量后缀的列
        base, comp = head.rsplit(".", 1)
        if base not in info:
            info[base] = {"comps": [], "sample": []}
        info[base]["comps"].append(comp)
        val = 0.0
        if sample_row >= 0:
            try:
                val = float(model.data(model.index(sample_row, c)))
            except Exception:
                val = 0.0
        info[base]["sample"].append(val)
    return info


@error_log
def prepare_export(pyrenderdoc, data):
    manager = pyrenderdoc.Extensions()
    if not pyrenderdoc.HasMeshPreview():
        manager.ErrorDialog("No preview mesh!", "Error")
        return

    # NOTE 先拿到 VS Input 表，收集属性信息用于下拉框自动识别
    main_window = pyrenderdoc.GetMainWindow().Widget()
    table = main_window.findChild(QtWidgets.QTableView, "inTable")
    if table is None:
        manager.ErrorDialog(
            "找不到 VS Input 数据表 (inTable)，请确认已打开 Mesh Viewer 并选中有效 draw。", "Error"
        )
        return

    model = table.model()
    row_count = model.rowCount()
    column_count = model.columnCount()
    rows = range(row_count)
    columns = range(column_count)

    attr_info = collect_attr_info(model, column_count, row_count)

    mqt = manager.GetMiniQtHelper()
    dialog = QueryDialog(mqt, attr_info)
    # NOTE 弹出下拉映射对话框（已按属性自动预选 Position/UV/Normal 等）
    if not mqt.ShowWidgetAsDialog(dialog.init_ui()):
        return

    save_path = manager.SaveFileName("Save FBX File", "", "*.fbx")
    if not save_path:
        return

    current = time.time()

    data = defaultdict(list)
    attr_list = set()

    for _, c in MProgressDialog.loop(columns, status="Collect Mesh Data"):
        head = model.headerData(c, QtCore.Qt.Horizontal)
        values = [model.data(model.index(r, c)) for r in rows]
        if "." not in head:
            data[head] = values
        else:
            attr = head.split(".")[0]
            attr_list.add(attr)
            data[attr].append(values)

    for _, attr in MProgressDialog.loop(attr_list, status="Rearrange Mesh Data"):
        values_list = data[attr]
        data[attr] = [[float(values[r]) for values in values_list] for r in rows]

    print("elapsed time unpack: %s" % (time.time() - current))
    pyrenderdoc.Replay().BlockInvoke(partial(export_fbx, save_path, dialog.mapper, data, attr_list))

    if os.path.exists(save_path):
        os.startfile(os.path.dirname(save_path))
        manager.MessageDialog("FBX Ouput Sucessfully", "Congradualtion!~")


def register(version, pyrenderdoc):
    # version is the RenderDoc Major.Minor version as a string, such as "1.2"
    # pyrenderdoc is the CaptureContext handle, the same as the global available in the python shell
    print("Registering FBX Mesh Exporter extension for RenderDoc {}".format(version))
    pyrenderdoc.Extensions().RegisterPanelMenu(qrenderdoc.PanelMenu.MeshPreview, ["Export FBX Mesh"], prepare_export)


def unregister():
    print("Unregistrating FBX Mesh Exporter extension")


# # NOTE for reload plugin
# import subprocess
# import qrenderdoc
# location = r"E:\repo\renderdoc2fbx\install.bat"
# subprocess.call(["cmd","/c",location],shell=True)
# extension = pyrenderdoc.Extensions()
# extension.LoadExtension("exporter.fbx")
