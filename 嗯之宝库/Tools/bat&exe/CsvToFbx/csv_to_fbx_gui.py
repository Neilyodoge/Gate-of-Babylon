"""
RenderDoc CSV → FBX 转换工具（GUI 版）

双击运行即可弹出可视化界面，支持：
  · 拖拽或浏览选择 CSV 文件（支持多选批量转换）
  · 自定义输出目录（默认同 CSV 目录）
  · 自定义 Mesh 名称（默认取文件名）
  · 自动识别多套 TEXCOORD，全部写入 FBX 的多层 UV
  · 实时显示转换日志

依赖：Python 3.6+ (tkinter 为标准库自带)
"""

import os
import sys
import time
import csv
import re
import threading
import tkinter as tk
from tkinter import ttk, filedialog, messagebox

# ================================================================
#  核心转换逻辑（复用 csv_to_fbx.py 的算法，支持多套 UV）
# ================================================================

# 导入同目录的核心模块
_script_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _script_dir)
from csv_to_fbx import read_csv, write_fbx_ascii




# ================================================================
#  GUI 应用
# ================================================================

class CsvToFbxApp:
    def __init__(self, root):
        self.root = root
        self.root.title("RenderDoc CSV → FBX 转换工具")
        self.root.geometry("680x640")
        self.root.minsize(580, 480)
        self.root.configure(bg="#2b2b2b")

        self.files = []
        self.output_dir = tk.StringVar(value="")
        self.mesh_name = tk.StringVar(value="")
        self.is_converting = False

        self._build_ui()

    def _build_ui(self):
        style = ttk.Style()
        style.theme_use('clam')
        style.configure("TFrame", background="#2b2b2b")
        style.configure("TLabel", background="#2b2b2b", foreground="#e0e0e0", font=("Segoe UI", 9))
        style.configure("Header.TLabel", background="#2b2b2b", foreground="#70c0e8", font=("Segoe UI", 12, "bold"))
        style.configure("TButton", font=("Segoe UI", 9))
        style.configure("Convert.TButton", font=("Segoe UI", 10, "bold"))
        style.configure("TEntry", fieldbackground="#3c3c3c", foreground="#e0e0e0")
        style.configure("TLabelframe", background="#2b2b2b", foreground="#a0a0a0")
        style.configure("TLabelframe.Label", background="#2b2b2b", foreground="#90c8e8", font=("Segoe UI", 9, "bold"))

        main_frame = ttk.Frame(self.root, padding=12)
        main_frame.pack(fill=tk.BOTH, expand=True)

        # 标题
        ttk.Label(main_frame, text="RenderDoc CSV → FBX 转换", style="Header.TLabel").pack(anchor=tk.W)
        ttk.Label(main_frame, text="将 RenderDoc 导出的 Mesh CSV 转为 FBX 7.4 ASCII 格式",
                  foreground="#808080").pack(anchor=tk.W, pady=(0, 8))

        # 输入文件区
        input_frame = ttk.LabelFrame(main_frame, text=" 输入文件 ", padding=8)
        input_frame.pack(fill=tk.X, pady=(0, 8))

        btn_row = ttk.Frame(input_frame)
        btn_row.pack(fill=tk.X)
        ttk.Button(btn_row, text="选择 CSV 文件...", command=self._browse_files).pack(side=tk.LEFT)
        ttk.Button(btn_row, text="清空列表", command=self._clear_files).pack(side=tk.LEFT, padx=(8, 0))
        self.file_count_label = ttk.Label(btn_row, text="已选 0 个文件", foreground="#a0a0a0")
        self.file_count_label.pack(side=tk.RIGHT)

        self.file_listbox = tk.Listbox(input_frame, height=4, bg="#1e1e1e", fg="#c8c8c8",
                                        selectbackground="#3a6a8a", font=("Consolas", 9),
                                        borderwidth=1, relief=tk.FLAT)
        self.file_listbox.pack(fill=tk.X, pady=(6, 0))

        # 输出设置区
        output_frame = ttk.LabelFrame(main_frame, text=" 输出设置 ", padding=8)
        output_frame.pack(fill=tk.X, pady=(0, 8))

        dir_row = ttk.Frame(output_frame)
        dir_row.pack(fill=tk.X)
        ttk.Label(dir_row, text="输出目录:").pack(side=tk.LEFT)
        ttk.Entry(dir_row, textvariable=self.output_dir).pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(6, 6))
        ttk.Button(dir_row, text="...", width=3, command=self._browse_output).pack(side=tk.LEFT)

        ttk.Label(output_frame, text="(留空 = 输出到 CSV 同目录)", foreground="#707070").pack(anchor=tk.W, pady=(2, 4))

        name_row = ttk.Frame(output_frame)
        name_row.pack(fill=tk.X)
        ttk.Label(name_row, text="Mesh 名称:").pack(side=tk.LEFT)
        ttk.Entry(name_row, textvariable=self.mesh_name).pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(6, 0))

        ttk.Label(output_frame, text="(留空 = 使用 CSV 文件名)", foreground="#707070").pack(anchor=tk.W, pady=(2, 0))

        # 转换按钮
        self.convert_btn = ttk.Button(main_frame, text="▶  开始转换", style="Convert.TButton",
                                       command=self._start_convert)
        self.convert_btn.pack(fill=tk.X, pady=(4, 8), ipady=6)

        # 日志区
        log_frame = ttk.LabelFrame(main_frame, text=" 转换日志 ", padding=4)
        log_frame.pack(fill=tk.BOTH, expand=True)

        self.log_text = tk.Text(log_frame, height=14, bg="#1a1a1a", fg="#b8d8b8",
                                 font=("Consolas", 9), borderwidth=0, wrap=tk.WORD)
        scrollbar = ttk.Scrollbar(log_frame, orient=tk.VERTICAL, command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.log_text.pack(fill=tk.BOTH, expand=True)

        self._log("就绪。请选择 RenderDoc 导出的 CSV 文件。")

    def _browse_files(self):
        paths = filedialog.askopenfilenames(
            title="选择 RenderDoc CSV 文件",
            filetypes=[("CSV 文件", "*.csv"), ("所有文件", "*.*")]
        )
        if paths:
            self.files = list(paths)
            self._refresh_file_list()

    def _clear_files(self):
        self.files = []
        self._refresh_file_list()

    def _refresh_file_list(self):
        self.file_listbox.delete(0, tk.END)
        for f in self.files:
            self.file_listbox.insert(tk.END, os.path.basename(f))
        self.file_count_label.config(text=f"已选 {len(self.files)} 个文件")

    def _browse_output(self):
        d = filedialog.askdirectory(title="选择输出目录")
        if d:
            self.output_dir.set(d)

    def _log(self, msg):
        self.log_text.insert(tk.END, msg + "\n")
        self.log_text.see(tk.END)

    def _start_convert(self):
        if self.is_converting:
            return

        if not self.files:
            messagebox.showwarning("提示", "请先选择 CSV 文件")
            return

        self.is_converting = True
        self.convert_btn.config(state=tk.DISABLED)

        thread = threading.Thread(target=self._do_convert, daemon=True)
        thread.start()

    def _do_convert(self):
        total = len(self.files)
        success = 0
        failed = 0
        fail_details = []  # 记录失败文件及具体原因，转换结束后弹窗提示

        for i, csv_path in enumerate(self.files):
            filename = os.path.basename(csv_path)
            self.root.after(0, self._log, f"\n[{i+1}/{total}] 处理: {filename}")

            try:
                # 确定输出路径
                out_dir = self.output_dir.get().strip()
                if not out_dir:
                    out_dir = os.path.dirname(csv_path)

                name = self.mesh_name.get().strip()
                if not name:
                    name = os.path.splitext(os.path.basename(csv_path))[0]

                fbx_path = os.path.join(out_dir, name + ".fbx")

                # 如果批量且没指定名称，用各自文件名
                if total > 1 and not self.mesh_name.get().strip():
                    name = os.path.splitext(os.path.basename(csv_path))[0]
                    fbx_path = os.path.join(out_dir, name + ".fbx")

                self.root.after(0, self._log, f"  读取 CSV...")
                verts, tris, uv_count = read_csv(csv_path)
                self.root.after(0, self._log, f"  顶点: {len(verts)}, 三角形: {len(tris)}, UV通道: {uv_count}")

                self.root.after(0, self._log, f"  写入 FBX: {fbx_path}")
                write_fbx_ascii(verts, tris, fbx_path, name)
                self.root.after(0, self._log, f"  ✓ 完成! ({len(verts)} 顶点, {len(tris)} 三角形, {uv_count} 套UV)")
                success += 1

            except Exception as e:
                # 记录完整堆栈到日志，方便定位具体原因
                import traceback
                tb = traceback.format_exc()
                reason = str(e).strip() or e.__class__.__name__
                self.root.after(0, self._log, f"  ✗ 失败: {reason}")
                self.root.after(0, self._log, tb.rstrip())
                fail_details.append((filename, reason))
                failed += 1

        summary = f"\n{'='*40}\n转换完成: 成功 {success} 个, 失败 {failed} 个"
        self.root.after(0, self._log, summary)
        self.root.after(0, self._finish_convert, fail_details)

    def _finish_convert(self, fail_details=None):
        self.is_converting = False
        self.convert_btn.config(state=tk.NORMAL)

        # 有失败时弹窗提示具体原因
        if fail_details:
            lines = [f"• {name}\n    原因: {reason}" for name, reason in fail_details]
            detail = "\n".join(lines)
            messagebox.showerror(
                "转换失败",
                f"以下 {len(fail_details)} 个文件转换失败：\n\n{detail}\n\n"
                f"（详细堆栈见下方“转换日志”）"
            )


def _show_startup_error(msg):
    """在没有控制台（pythonw/vbs 静默启动）时，用弹窗提示启动失败。"""
    # 优先尝试用 tkinter 弹窗
    try:
        import tkinter as _tk
        from tkinter import messagebox as _mb
        _r = _tk.Tk()
        _r.withdraw()
        _mb.showerror("CSV → FBX 启动失败", msg)
        _r.destroy()
        return
    except Exception:
        pass
    # 退化方案：直接调用 Windows MessageBox（tkinter 都不可用时）
    try:
        import ctypes
        ctypes.windll.user32.MessageBoxW(0, msg, "CSV → FBX 启动失败", 0x10)
    except Exception:
        pass


def main():
    root = tk.Tk()
    app = CsvToFbxApp(root)
    root.mainloop()


if __name__ == '__main__':
    try:
        main()
    except Exception:
        import traceback
        _show_startup_error("程序启动失败：\n\n" + traceback.format_exc())
