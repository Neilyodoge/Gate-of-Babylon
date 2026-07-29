# -*- coding: utf-8 -*-
"""
属性映射对话框（下拉枚举版）

改造点：
- 把原来的手动填列名，改成【下拉框枚举所有 VS Input 属性】，直接选。
- 打开对话框前先【自动识别】：
    · Position  = 第一个属性（优先分量 >= 3 的）
    · UV / UV2  = 分量为 2（vector2）的属性，优先值域在 0~1 的
    · Normal    = 分量 >= 3 且长度≈1 的属性
    · Tangent   = 剩下的 4 分量属性
- 识别不出来的通道默认选 "none"，可手动改。
"""

from __future__ import division
from __future__ import print_function
from __future__ import absolute_import

from collections import OrderedDict
from PySide2 import QtWidgets


class QueryDialog(object):

    title = "Attribute Query  (下拉选择每个通道对应哪个属性)"

    # (mapper 键, 界面显示名)
    channels = [
        ("POSITION", "Vertex Position"),
        ("NORMAL", "Vertex Normal"),
        ("TANGENT", "Vertex Tangent"),
        ("BINORMAL", "Vertex BiNormal"),
        ("COLOR", "Vertex Color"),
        ("UV", "UV"),
        ("UV2", "UV2"),
    ]

    NONE_TEXT = "none"

    def __init__(self, mqt, attr_info=None):
        """
        attr_info: OrderedDict, base_name -> {"comps": [..], "sample": [float,..]}
                   由 __init__.py 在打开对话框前从 VS Input 表收集。
        """
        self.mqt = mqt
        self.attr_info = attr_info if attr_info is not None else OrderedDict()
        self.mapper = {}
        self.combos = {}

    # ------------------------------------------------------------------
    #  自动识别
    # ------------------------------------------------------------------
    def _comp_count(self, name):
        return len(self.attr_info[name]["comps"])

    def _sample(self, name):
        return self.attr_info[name]["sample"]

    def _auto_detect(self):
        names = list(self.attr_info.keys())
        used = set()
        result = {key: "" for key, _ in self.channels}

        # Position：第一个属性（优先分量 >= 3），否则退化为第一个属性
        pos = None
        for n in names:
            if self._comp_count(n) >= 3:
                pos = n
                break
        if pos is None and names:
            pos = names[0]
        if pos:
            result["POSITION"] = pos
            used.add(pos)

        # UV / UV2：分量恰好为 2 的属性，值域接近 0~1 的排前面
        def is_uv_like(n):
            vals = self._sample(n)
            return all(-0.2 <= v <= 1.2 for v in vals) if vals else False

        vec2 = [n for n in names if n not in used and self._comp_count(n) == 2]
        vec2.sort(key=lambda n: (not is_uv_like(n)))  # uv-like 排前
        if len(vec2) >= 1:
            result["UV"] = vec2[0]
            used.add(vec2[0])
        if len(vec2) >= 2:
            result["UV2"] = vec2[1]
            used.add(vec2[1])

        # Normal：分量 >= 3 且长度≈1 的属性
        for n in names:
            if n in used or self._comp_count(n) < 3:
                continue
            vals = self._sample(n)[:3]
            length = sum(v * v for v in vals) ** 0.5
            if 0.8 <= length <= 1.2:
                result["NORMAL"] = n
                used.add(n)
                break

        # Tangent：剩下的 4 分量属性
        for n in names:
            if n in used:
                continue
            if self._comp_count(n) == 4:
                result["TANGENT"] = n
                used.add(n)
                break

        return result

    # ------------------------------------------------------------------
    #  UI
    # ------------------------------------------------------------------
    def _option_label(self, name):
        """下拉项显示：属性名 + 分量数（方便判断谁是 UV/法线）。"""
        return "%s  (%d 分量)" % (name, self._comp_count(name))

    def init_ui(self):
        self.widget = self.mqt.CreateToplevelWidget(self.title, None)

        names = list(self.attr_info.keys())
        auto = self._auto_detect()

        if not names:
            info = self.mqt.CreateLabel()
            self.mqt.SetWidgetText(
                info, "未在 VS Input 表中找到任何顶点属性，请确认已打开 Mesh Viewer 并选中有效 draw。"
            )
            self.mqt.AddWidget(self.widget, info)

        for key, label_text in self.channels:
            container = self.mqt.CreateHorizontalContainer()
            label = self.mqt.CreateLabel()
            self.mqt.SetWidgetText(label, "%-16s" % label_text)

            combo = QtWidgets.QComboBox()
            combo.addItem(self.NONE_TEXT)          # index 0 = none
            for n in names:
                combo.addItem(self._option_label(n))

            # 设置自动识别结果为默认选中
            sel = auto.get(key, "")
            if sel and sel in names:
                combo.setCurrentIndex(names.index(sel) + 1)
            else:
                combo.setCurrentIndex(0)

            self.mqt.AddWidget(container, label)
            self.mqt.AddWidget(container, combo)
            self.mqt.AddWidget(self.widget, container)
            self.combos[key] = combo

        # 按钮
        button_container = self.mqt.CreateHorizontalContainer()
        ok_button = self.mqt.CreateButton(self.accept)
        self.mqt.SetWidgetText(ok_button, "OK")
        cancel_cb = lambda *args: self.mqt.CloseCurrentDialog(False)
        cancel_button = self.mqt.CreateButton(cancel_cb)
        self.mqt.SetWidgetText(cancel_button, "Cancel")
        self.mqt.AddWidget(button_container, cancel_button)
        self.mqt.AddWidget(button_container, ok_button)
        self.mqt.AddWidget(self.widget, button_container)

        return self.widget

    def accept(self, context, widget, text):
        names = list(self.attr_info.keys())
        self.mapper = {}
        for key, combo in self.combos.items():
            idx = combo.currentIndex()
            # index 0 = none -> 空字符串
            self.mapper[key] = names[idx - 1] if idx > 0 and (idx - 1) < len(names) else ""
        self.mapper["ENGINE"] = "unity"
        self.mqt.CloseCurrentDialog(True)
