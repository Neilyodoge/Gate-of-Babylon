"""External Tk panel. Run 启动工具.cmd; no need to open the Max UI."""
import ctypes
import os
from pathlib import Path
import queue
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import batch_core as core
import ui_theme
import max_locator
from batch_runner import BatchRunner

ROOT = Path(__file__).resolve().parent
CONFIG_PATH = ROOT / 'last_config.json'
STATUS = {'success': '成功', 'failed': '失败', 'skipped': '已跳过', 'canceled': '未完成'}


def documents_dir():
    if os.name == 'nt':
        buf = ctypes.create_unicode_buffer(32768)
        if ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf) == 0:
            return Path(buf.value)
    return Path.home() / 'Documents'


def discover_presets():
    paths = set()
    base = documents_dir() / '3dsmax' / 'FBX'
    if base.exists():
        paths.update(base.rglob('*.fbxexportpreset'))
    return sorted(paths, key=lambda p: (p.stem, str(p)))


class BatchApp:
    def __init__(self, root):
        self.root, self.runner, self.thread = root, None, None
        self.events, self.jobs, self.results = queue.Queue(), [], {}
        self.rows, self.log_dir, self.closing = {}, '', False
        self.advanced_window = None
        self.resolving_max = False
        self.max_request = 0
        self.max_hint = tk.StringVar(value='请在高级设置中选择 Max 快捷方式')
        self.controls = []
        root.title('传政今天牙掉了没 (ಡωಡ)')
        root.geometry('1180x880')
        root.minsize(1040, 760)
        ui_theme.apply_theme(root)
        self.vars = {key: tk.StringVar() for key in ('source', 'output', 'max_launcher', 'model_preset', 'animation_preset', 'root_name')}
        self.vars['root_name'].set('Root')
        self.recursive = tk.BooleanVar(value=False)
        self.scene_range = tk.BooleanVar(value=True)
        self.auto_fbx = tk.BooleanVar(value=False)
        self._manual_output = ''
        self.model_frame = tk.StringVar(value='0')
        self.timeout = tk.StringVar(value='10')
        self.status = tk.StringVar(value='选择文件夹后即可开始全部导出，也可以先扫描查看分组。')
        self.phase = tk.StringVar(value='●  准备就绪')
        self.summary = tk.StringVar(value='尚未扫描')
        self.progress_text = tk.StringVar(value='0 / 0')
        presets = discover_presets()
        self.preset_labels = {}
        for path in presets:
            same_name = sum(p.stem == path.stem for p in presets) > 1
            label = '{}  —  {}'.format(path.stem, path.parent) if same_name else path.stem
            self.preset_labels[label] = str(path)
        for key, preferred in [('model_preset', '蒙皮'), ('animation_preset', '动作')]:
            choices = [label for label, path in self.preset_labels.items() if Path(path).stem == preferred]
            if choices:
                self.vars[key].set(choices[0])
        if CONFIG_PATH.is_file():
            try:
                saved = core.load_config(CONFIG_PATH)
                for key, var in self.vars.items():
                    if key in saved:
                        var.set(saved[key])
                self.vars['max_launcher'].set(saved.get('max_launcher', saved.get('max_exe', '')))
                self.recursive.set(saved.get('recursive', False))
                self.scene_range.set(saved.get('scene_range', True))
                self.auto_fbx.set(saved.get('auto_fbx', False))
                self._manual_output = saved.get('manual_output', saved.get('output', ''))
                self.model_frame.set(str(saved.get('model_frame', 0)))
                self.timeout.set(str(saved.get('timeout_minutes', 10)))
            except Exception:
                self.status.set('旧配置无法读取，请重新选择目录和预设。')
        for key in ('model_preset', 'animation_preset'):
            self.set_preset_label(key, self.vars[key].get())
        self.vars['max_launcher'].trace_add('write', self.launcher_changed)
        self.launcher_changed()

        frame = ttk.Frame(root, padding=(24, 20, 24, 16))
        frame.pack(fill='both', expand=True)
        header = ttk.Frame(frame)
        header.pack(fill='x', pady=(0, 7))
        ttk.Label(header, text='传政今天牙掉了没 (ಡωಡ)', style='Title.TLabel').pack(side='left')
        ttk.Label(header, textvariable=self.phase, style='Badge.TLabel').pack(side='right', anchor='center')
        ttk.Label(frame, text='角色本体与骨骼动画  /  FBX 批量导出', style='Muted.TLabel').pack(anchor='w', pady=(0, 10))

        rules = ttk.Frame(frame)
        rules.pack(fill='x', pady=(0, 10))
        for title, detail, side in [
            ('角色名.max', '角色本体  ·  模型 + 蒙皮 + 骨骼，无动画', 'left'),
            ('角色名@动作名.max', '动作文件  ·  Root + 骨骼，排除其他辅助对象', 'right')]:
            card = ttk.Frame(rules, style='Card.TFrame', padding=(16, 8))
            card.pack(side=side, fill='both', expand=True, padx=(0, 6) if side == 'left' else (6, 0))
            ttk.Label(card, text=title, style='CardTitle.TLabel').pack(anchor='w')
            ttk.Label(card, text=detail, style='CardMuted.TLabel').pack(anchor='w', pady=(4, 0))

        self.config_frame = ttk.Frame(frame, style='Card.TFrame', padding=(16, 12))
        self.config_frame.pack(fill='x', pady=(0, 16))
        for row, (key, title) in enumerate([('source', '源文件夹'), ('output', '导出到'),
                                          ('model_preset', '角色本体预设'), ('animation_preset', '骨骼动作预设')]):
            ttk.Label(self.config_frame, text=title, style='Card.TLabel').grid(row=row, column=0, sticky='w', padx=(0, 16), pady=3)
            if key == 'output':
                output_row = ttk.Frame(self.config_frame, style='Card.TFrame')
                output_row.grid(row=row, column=1, sticky='ew', pady=3)
                field = self.output_entry = ttk.Entry(output_row, textvariable=self.vars[key])
                field.pack(side='left', fill='x', expand=True)
                self.auto_fbx_check = ttk.Checkbutton(output_row, text='牙崩掉了fbx', variable=self.auto_fbx,
                                                     command=self.toggle_auto_fbx)
                self.auto_fbx_check.pack(side='right', padx=(12, 0))
                ui_theme.Tooltip(self.auto_fbx_check,
                    '使用源文件夹上一级的 fbx 文件夹作为导出路径。\n例如：D:\\角色\\MAX → D:\\角色\\fbx\n随源文件夹自动更新；开始导出时创建，已有文件夹则直接使用。')
                self.controls.append(self.auto_fbx_check)
            elif key.endswith('_preset'):
                field = ttk.Combobox(self.config_frame, textvariable=self.vars[key], values=list(self.preset_labels))
            else:
                field = ttk.Entry(self.config_frame, textvariable=self.vars[key])
            if key != 'output':
                field.grid(row=row, column=1, sticky='ew', pady=3)
            ui_theme.Tooltip(field, lambda k=key: self.preset_labels.get(self.vars[k].get(), self.vars[k].get()))
            button = ttk.Button(self.config_frame, text='选择预设…' if key.endswith('_preset') else '选择文件夹…',
                                command=lambda k=key: self.browse(k))
            button.grid(row=row, column=2, padx=(10, 0), pady=3, sticky='ew')
            self.controls.extend([field, button])
        self.config_frame.columnconfigure(1, weight=1)
        preset_help = ttk.Label(self.config_frame,
            text='两项均使用 3ds Max 内部的 FBX 导出预设（.fbxexportpreset）。先在 Max 的 FBX 导出窗口保存预设，再在这里选择。',
            style='CardMuted.TLabel', wraplength=980)
        preset_help.grid(row=4, column=0, columnspan=3, sticky='w', pady=(4, 2))
        self.config_frame.bind('<Configure>', lambda e: preset_help.configure(wraplength=max(200, e.width - 32)))
        options = ttk.Frame(self.config_frame, style='Card.TFrame')
        options.grid(row=5, column=0, columnspan=3, sticky='ew', pady=(6, 0))
        for label, var in [('包含子文件夹', self.recursive), ('动作范围跟随 MAX 时间轴', self.scene_range)]:
            check = ttk.Checkbutton(options, text=label, variable=var)
            check.pack(side='left', padx=(0, 22))
            self.controls.append(check)
        ui_theme.Tooltip(check, '预设启用动画烘焙时，自动使用每个 MAX 文件的起止帧。\n关闭后使用动作预设中保存的范围。')
        advanced = ttk.Button(options, text='高级设置…', command=self.show_advanced, style='Quiet.TButton')
        advanced.pack(side='right')
        self.controls.append(advanced)
        max_status = ttk.Label(options, textvariable=self.max_hint, style='CardMuted.TLabel', width=26)
        max_status.pack(side='right', padx=(8, 14))
        ui_theme.Tooltip(max_status, lambda: self.max_hint.get() + '\n' + self.vars['max_launcher'].get())

        heading = ttk.Frame(frame)
        heading.pack(fill='x', pady=(0, 10))
        ttk.Label(heading, text='导出列表', style='Section.TLabel').pack(side='left')
        ttk.Label(heading, textvariable=self.summary, style='Muted.TLabel').pack(side='left', padx=16)
        ttk.Label(heading, text='按文件名自动分类', style='Muted.TLabel').pack(side='right')
        buttons = ttk.Frame(frame)
        buttons.pack(fill='x', pady=(0, 12))
        for text, callback, style in [('扫描文件', self.scan, 'TButton'),
                                      ('开始全部导出', self.run_all, 'Accent.TButton'),
                                      ('导出选中', self.run_selected, 'TButton'),
                                      ('重试失败项', self.retry, 'TButton')]:
            button = ttk.Button(buttons, text=text, command=callback, style=style)
            button.pack(side='left', padx=(0, 8))
            self.controls.append(button)
        self.kill_button = ttk.Button(buttons, text='立即终止', command=lambda: self.stop(True),
                                      style='Danger.TButton', state='disabled')
        self.kill_button.pack(side='right')
        self.stop_button = ttk.Button(buttons, text='完成当前后停止', command=self.stop, state='disabled')
        self.stop_button.pack(side='right', padx=(8, 8))
        ui_theme.Tooltip(self.kill_button, '立即结束本工具启动的后台 Max。当前文件会标记为未完成，可稍后重试。')

        table_frame = ttk.Frame(frame, style='Card.TFrame')
        table_frame.rowconfigure(0, weight=1)
        table_frame.columnconfigure(0, weight=1)
        columns = ('character', 'kind', 'file', 'status', 'detail')
        self.table = ttk.Treeview(table_frame, columns=columns, show='headings', selectmode='extended', height=5)
        for column, title, width in zip(columns, ['角色', '用途', '源文件（MAX）', '状态', '结果 / 提示'], [175, 95, 300, 95, 310]):
            self.table.heading(column, text=title, anchor='w')
            self.table.column(column, width=width, minwidth=75, anchor='w', stretch=column in ('file', 'detail', 'character'))
        scroll = ttk.Scrollbar(table_frame, orient='vertical', command=self.table.yview)
        horizontal = ttk.Scrollbar(table_frame, orient='horizontal', command=self.table.xview)
        self.table.configure(yscrollcommand=scroll.set, xscrollcommand=horizontal.set)
        self.table.grid(row=0, column=0, sticky='nsew')
        scroll.grid(row=0, column=1, sticky='ns')
        horizontal.grid(row=1, column=0, sticky='ew')
        for tag, color in [('failed', '#ffa59e'), ('success', '#7fe0bd'), ('skipped', '#98a9bb'),
                           ('canceled', '#efca8b'), ('running', '#8dcaff')]:
            self.table.tag_configure(tag, foreground=color)
        self.table.tag_configure('odd', background='#1e2935')
        self.table.bind('<Double-1>', self.show_detail)
        self.empty = ttk.Frame(table_frame, style='Card.TFrame', padding=12)
        self.empty.place(relx=.5, rely=.55, anchor='center')
        ttk.Label(self.empty, text='等待添加角色与动作', style='CardTitle.TLabel').pack()
        ttk.Label(self.empty, text='选择源文件夹，点击“扫描文件”查看自动分组', style='CardMuted.TLabel').pack(pady=(6, 0))
        footer = ttk.Frame(frame)
        footer.pack(side='bottom', fill='x')
        self.progress = ttk.Progressbar(footer, mode='determinate')
        self.progress.pack(fill='x', pady=(14, 10))
        status_line = ttk.Frame(footer)
        status_line.pack(fill='x')
        self.status_label = ttk.Label(status_line, textvariable=self.status, wraplength=960)
        self.status_label.pack(side='left', fill='x', expand=True)
        ttk.Label(status_line, textvariable=self.progress_text, style='Muted.TLabel').pack(side='right', padx=(12, 0))
        status_line.bind('<Configure>', lambda e: self.status_label.configure(wraplength=max(100, e.width - 120)))
        links = ttk.Frame(footer)
        links.pack(fill='x', pady=(12, 0))
        ttk.Label(links, text='已有 FBX 自动跳过  ·  双击查看详情  ·  源 MAX 保持不变', style='Muted.TLabel').pack(side='left')
        ttk.Button(links, text='打开日志', command=lambda: self.open_folder(self.log_dir), style='Quiet.TButton').pack(side='right')
        ttk.Button(links, text='打开输出文件夹', command=lambda: self.open_folder(self.vars['output'].get()),
                   style='Quiet.TButton').pack(side='right', padx=(0, 8))
        table_frame.pack(fill='both', expand=True)
        self.vars['source'].trace_add('write', self.sync_output_folder)
        self.sync_output_folder()
        if self.auto_fbx.get():
            self.output_entry.configure(state='readonly')
        root.protocol('WM_DELETE_WINDOW', self.close)
        root.after(150, self.poll)

    def sync_output_folder(self, *_args):
        if self.auto_fbx.get():
            source = self.vars['source'].get().strip()
            # abspath normalizes typed paths without resolving filesystem links.
            output = str(Path(os.path.abspath(source)).parent / 'fbx') if source else ''
            self.vars['output'].set(output)

    def toggle_auto_fbx(self):
        if self.auto_fbx.get():
            self._manual_output = self.vars['output'].get()
            self.sync_output_folder()
        else:
            self.vars['output'].set(self._manual_output)
        self.output_entry.configure(state='readonly' if self.auto_fbx.get() else 'normal')

    def set_preset_label(self, key, value):
        label = next((label for label, path in self.preset_labels.items() if path == value), value)
        self.vars[key].set(label)

    def launcher_changed(self, *_args):
        # Invalidate pending resolution on any edit; never reuse a cached EXE.
        self.max_request += 1
        if self.resolving_max:
            self.resolving_max = False
            self.set_running(False)
            self.phase.set('●  准备就绪')
            self.status.set('Max 入口已更改，请重新开始导出。')
        value = max_locator.clean_path(self.vars['max_launcher'].get())
        path = Path(value)
        label = path.stem if path.suffix.lower() == '.lnk' else path.parent.name
        self.max_hint.set('指定 Max：' + label if value else '请在高级设置中选择 Max 快捷方式')

    def settings_data(self):
        self.sync_output_folder()
        data = {key: self.preset_labels.get(var.get(), var.get()).strip() for key, var in self.vars.items()}
        data.update(recursive=self.recursive.get(), scene_range=self.scene_range.get(),
                    model_frame=self.model_frame.get(), timeout_minutes=self.timeout.get(),
                    auto_fbx=self.auto_fbx.get(),
                    manual_output=self._manual_output if self.auto_fbx.get() else data['output'])
        return data

    def remember_settings(self):
        try:
            core.save_config(CONFIG_PATH, self.settings_data())
        except OSError as exc:
            messagebox.showwarning('设置未保存', '无法保存设置，下次打开需要重新选择：\n' + str(exc), parent=self.root)

    def show_advanced(self):
        if self.advanced_window and self.advanced_window.winfo_exists():
            self.advanced_window.lift()
            return
        window = self.advanced_window = tk.Toplevel(self.root)
        window.title('高级设置 · 传政今天牙掉了没 (ಡωಡ)')
        window.configure(bg=ui_theme.BG)
        window.transient(self.root)
        window.resizable(True, False)
        window.geometry('790x620')
        window.minsize(760, 620)
        window.geometry('+{}+{}'.format(self.root.winfo_rootx() + 80, self.root.winfo_rooty() + 50))
        body = ttk.Frame(window, padding=24)
        body.pack(fill='both', expand=True)
        ttk.Label(body, text='高级设置', style='Section.TLabel').pack(anchor='w')
        ttk.Label(body, text='一般保留默认值即可。修改后用于下一次导出。', style='Muted.TLabel').pack(anchor='w', pady=(6, 18))
        settings_controls = []
        program = ttk.Frame(body, style='Card.TFrame', padding=14)
        program.pack(fill='x', pady=(0, 12))
        ttk.Label(program, text='使用哪个 Max（快捷方式）', style='Card.TLabel').pack(anchor='w', pady=(0, 8))
        row = ttk.Frame(program, style='Card.TFrame')
        row.pack(fill='x')
        field = ttk.Entry(row, textvariable=self.vars['max_launcher'])
        field.pack(side='left', fill='x', expand=True)
        button = ttk.Button(row, text='选择快捷方式…', command=lambda: self.browse('max_launcher'))
        button.pack(side='right', padx=(8, 0))
        settings_controls.extend([field, button])
        ui_theme.Tooltip(field, lambda: self.vars['max_launcher'].get())
        ttk.Label(program, textvariable=self.max_hint, style='CardMuted.TLabel', wraplength=680).pack(anchor='w', pady=(8, 0))
        ttk.Label(program, text='按快捷方式的版本、参数和起始位置启动普通 Max，整批完成后退出。也可粘贴完整路径或选择 Max 程序。',
                  style='CardMuted.TLabel', wraplength=680).pack(anchor='w', pady=(5, 0))

        settings = ttk.Frame(body, style='Card.TFrame', padding=14)
        settings.pack(fill='x')
        settings.columnconfigure(1, weight=1)
        descriptions = [
            ('骨骼根节点名称', self.vars['root_name'], None,
             '保留这个节点及其下的骨骼；两种导出都排除其余辅助对象。名称不区分大小写，Root / root / ROOT 均可。'),
            ('角色默认姿势帧', self.model_frame, (-100000, 100000),
             '0 = 用第 0 帧的姿势导出角色本体。只影响本体，不影响动作范围；不会自动生成 T 姿势。'),
            ('最长等待时间（分钟）', self.timeout, (1, 240),
             '分别用于 Max 启动、每个文件的处理，默认各等 10 分钟。文件超时后跳过并继续；启动超时则结束本批。大文件可调大。')]
        for index, (label, var, bounds, detail) in enumerate(descriptions):
            row = index * 2
            ttk.Label(settings, text=label, style='Card.TLabel').grid(row=row, column=0, sticky='w', padx=(0, 14))
            field = (ttk.Spinbox(settings, from_=bounds[0], to=bounds[1], textvariable=var, width=12)
                     if bounds else ttk.Entry(settings, textvariable=var, width=14))
            field.grid(row=row, column=1, sticky='e')
            settings_controls.append(field)
            help_label = ttk.Label(settings, text=detail, style='CardMuted.TLabel', wraplength=680)
            help_label.grid(row=row + 1, column=0, columnspan=2, sticky='w', pady=(5, 15 if index < 2 else 0))
            ui_theme.Tooltip(field, detail)
        self.controls.extend(settings_controls)
        if self.thread and self.thread.is_alive():
            for control in settings_controls:
                control.configure(state='disabled')

        def dismiss():
            self.controls = [control for control in self.controls if control not in settings_controls]
            self.remember_settings()
            window.destroy()
            self.advanced_window = None

        ttk.Button(body, text='返回主界面', style='Accent.TButton', command=dismiss).pack(side='bottom', anchor='e', pady=(14, 0))
        window.protocol('WM_DELETE_WINDOW', dismiss)
        window.bind('<Escape>', lambda _event: dismiss())
        window.after(50, lambda: ui_theme.dark_titlebar(window))


    def browse(self, key):
        current = self.preset_labels.get(self.vars[key].get(), self.vars[key].get())
        if key == 'max_launcher':
            try:
                owner = self.advanced_window or self.root
                value = max_locator.pick_max_launcher(owner.winfo_id(), current)
                if value:
                    self.vars[key].set(value)
            except Exception as exc:
                messagebox.showerror('无法选择快捷方式', str(exc), parent=self.advanced_window or self.root)
            return
        if key in ('source', 'output'):
            value = filedialog.askdirectory(title='选择源 MAX 文件夹' if key == 'source' else '选择 FBX 输出文件夹',
                                            initialdir=current or None, parent=self.root)
        else:
            value = filedialog.askopenfilename(title='选择 Max 内部 FBX 导出预设',
                                               initialdir=str(Path(current).parent) if current else None,
                                               filetypes=[('Max 内部 FBX 导出预设', '*.fbxexportpreset')], parent=self.root)
        if value:
            if key == 'output':
                self.auto_fbx.set(False)
                self._manual_output = value
                self.output_entry.configure(state='normal')
            self.set_preset_label(key, value) if key.endswith('_preset') else self.vars[key].set(value)
            if key == 'source' and not self.vars['output'].get():
                self.vars['output'].set(str(Path(value) / 'FBX_Export'))

    def config(self):
        data = self.settings_data()
        if not data['source'] or not data['output']:
            raise ValueError('请选择 MAX 文件夹和 FBX 输出文件夹。')
        if not data['root_name']:
            raise ValueError('骨骼根节点名称不能为空。')
        try:
            model_frame = int(self.model_frame.get())
        except ValueError:
            raise ValueError('高级设置中的“角色默认姿势帧”必须是整数，例如 0。') from None
        try:
            timeout_minutes = float(self.timeout.get())
        except ValueError:
            raise ValueError('高级设置中的“最长等待时间”必须是数字，例如 10。') from None
        data.update(recursive=self.recursive.get(), scene_range=self.scene_range.get(),
                    model_frame=model_frame, timeout_minutes=timeout_minutes,
                    auto_fbx=self.auto_fbx.get(),
                    manual_output=self._manual_output if self.auto_fbx.get() else data['output'])
        if not 0 < data['timeout_minutes'] <= 240:
            raise ValueError('高级设置中的“最长等待时间”必须大于 0，且不超过 240 分钟。')
        return data

    def display_jobs(self, jobs):
        self.table.delete(*self.table.get_children())
        self.jobs, self.rows = jobs, {}
        self.empty.place_forget() if jobs else self.empty.place(relx=.5, rely=.55, anchor='center')
        models = sum(job['kind'] == 'model' for job in jobs)
        issues = sum(bool(job['issue']) for job in jobs)
        self.summary.set('{} 个角色本体   /   {} 个动作   /   {} 个需检查'.format(models, len(jobs) - models, issues))
        self.progress.configure(maximum=max(1, len(jobs)), value=0)
        self.progress_text.set('0 / {}'.format(len(jobs)))
        for i, job in enumerate(jobs):
            source = job['source']
            status = '需检查' if job['issue'] else ('已有 / 跳过' if Path(job['target']).exists() else '待处理')
            iid = str(i)
            self.rows[source] = iid
            self.table.insert('', 'end', iid=iid, values=(job['character'], '角色本体' if job['kind'] == 'model' else '动作',
                                                       Path(source).name, status, job['issue'] or ''),
                              tags=(('failed',) if job['issue'] else ()) + (('odd',) if i % 2 else ()))

    def scan(self):
        try:
            config = self.config()
            self.display_jobs(core.make_jobs(config['source'], config['output'], config['recursive']))
            models = sum(j['kind'] == 'model' for j in self.jobs)
            issues = sum(bool(j['issue']) for j in self.jobs)
            self.status.set('找到 {} 个角色本体、{} 个动作，{} 个文件需要检查。'.format(models, len(self.jobs) - models, issues))
            self.phase.set('●  待导出' if self.jobs else '●  未找到文件')
        except Exception as exc:
            messagebox.showerror('无法扫描', str(exc))

    def run_all(self):
        self.start()

    def run_selected(self):
        selection = self.table.selection()
        if not selection:
            return messagebox.showinfo('导出选中', '先扫描并选中要导出的文件；可按 Ctrl 多选。\n要处理整个文件夹，直接点击“开始全部导出”。')
        self.start({self.jobs[int(i)]['source'] for i in selection})

    def retry(self):
        sources = {source for source, value in self.results.items() if value['status'] in ('failed', 'canceled')}
        if not sources:
            return messagebox.showinfo('重试', '本次没有失败或未完成的文件。')
        self.start(sources)

    def start(self, only_sources=None):
        if self.resolving_max or (self.thread and self.thread.is_alive()):
            return
        try:
            config = self.config()
            if not max_locator.clean_path(config['max_launcher']):
                raise ValueError('请在“高级设置”中选择要使用的 Max 快捷方式。')
            jobs = core.make_jobs(config['source'], config['output'], config['recursive'])
            if only_sources is not None:
                jobs = [j for j in jobs if j['source'] in only_sources]
            if not jobs:
                raise ValueError('没有可处理的 MAX 文件。')
            for kind in {j['kind'] for j in jobs if not j['issue']}:
                core.validate_preset(config[kind + '_preset'])
            self.max_request += 1
            request = self.max_request
            self.resolving_max = True
            self.set_running(True)
            self.phase.set('●  正在检查 Max')
            self.status.set('正在检查指定的 Max 快捷方式…')

            def resolve():
                event = {'event': 'max_resolved', 'request': request, 'config': config, 'jobs': jobs}
                try:
                    event['launch'] = max_locator.resolve_launch(config['max_launcher'])
                except Exception as exc:
                    event['error'] = str(exc)
                self.events.put(event)

            threading.Thread(target=resolve, daemon=True).start()
        except Exception as exc:
            if self.resolving_max:
                self.resolving_max = False
                self.set_running(False)
                self.phase.set('●  准备就绪')
            messagebox.showerror('无法开始', str(exc))

    def finish_max_resolution(self, event):
        if event['request'] != self.max_request:
            return
        self.resolving_max = False
        try:
            if event.get('error'):
                raise ValueError(event['error'])
            config, jobs = event['config'], event['jobs']
            config.update(event['launch'])
            core.save_config(CONFIG_PATH, config)
            self.max_hint.set('使用 ' + Path(config['max_exe']).parent.name)
            self.display_jobs(jobs)
            self.results = {}
            self.log_dir = ''
            self.runner = BatchRunner(config, jobs, self.events.put)
            self.thread = threading.Thread(target=self.runner.run, daemon=True)
            self.set_running(True)
            self.progress.configure(maximum=len(jobs), value=0)
            self.status.set('正在启动指定版本的后台 Max，首次启动可能需要一段时间…')
            self.thread.start()
        except Exception as exc:
            self.set_running(False)
            self.phase.set('●  准备就绪')
            self.status.set('无法启动指定的 Max，请检查高级设置。')
            messagebox.showerror('无法开始', str(exc), parent=self.advanced_window or self.root)

    def set_running(self, running):
        for control in self.controls:
            control.configure(state='disabled' if running else 'normal')
        self.stop_button.configure(state='normal' if running and not self.resolving_max else 'disabled')
        self.kill_button.configure(state='normal' if running and not self.resolving_max else 'disabled')
        if not running and self.auto_fbx.get():
            self.output_entry.configure(state='readonly')
        if running:
            self.phase.set('●  正在导出')

    def stop(self, immediate=False):
        if self.runner:
            self.runner.stop(immediate)
            self.phase.set('●  正在停止')
            self.status.set('正在终止本工具启动的后台 Max…' if immediate else '已请求停止；等待当前文件完成。卡住时可使用“立即终止”。')

    def poll(self):
        try:
            while True:
                event = self.events.get_nowait()
                name = event['event']
                if name == 'max_resolved':
                    self.finish_max_resolution(event)
                elif name == 'run_start':
                    self.log_dir = event['log_dir']
                elif name == 'launching':
                    self.status.set('正在启动后台 Max；剩余 {} 个文件…'.format(event['remaining']))
                elif name == 'file_start':
                    self.status.set('正在导出：' + Path(event['source']).name)
                    row = self.rows[event['source']]
                    self.table.set(row, 'status', '处理中')
                    self.table.item(row, tags=('running',) + (('odd',) if int(row) % 2 else ()))
                    self.table.see(row)
                elif name == 'file_end':
                    self.results[event['source']] = {'status': event['status'], 'detail': event['detail']}
                    row = self.rows[event['source']]
                    self.table.set(row, 'status', STATUS.get(event['status'], event['status']))
                    self.table.set(row, 'detail', event['detail'])
                    self.table.item(row, tags=(event['status'],) + (('odd',) if int(row) % 2 else ()))
                    self.progress.configure(value=len(self.results))
                    self.progress_text.set('{} / {}'.format(len(self.results), len(self.jobs)))
                elif name in ('error', 'restarting'):
                    self.status.set(event['detail'])
                elif name == 'run_done':
                    self.set_running(False)
                    values = [v['status'] for v in event['results'].values()]
                    self.phase.set('●  已停止' if event['stopped'] else ('●  已结束，有失败项' if values.count('failed') else '●  已结束'))
                    self.status.set('{}：成功 {}，失败 {}，跳过 {}，未完成 {}。'.format('已停止' if event['stopped'] else '已结束',
                                    values.count('success'), values.count('failed'), values.count('skipped'), values.count('canceled')))
                    if self.closing:
                        self.root.destroy()
                        return
        except queue.Empty:
            pass
        self.root.after(150, self.poll)

    def show_detail(self, _event=None):
        selection = self.table.selection()
        if selection:
            job = self.jobs[int(selection[0])]
            detail = self.results.get(job['source'], {}).get('detail', job['issue'])
            messagebox.showinfo('文件信息', '源文件：\n{}\n\n输出文件：\n{}\n\n{}'.format(job['source'], job['target'], detail))

    def open_folder(self, path):
        if path and Path(path).is_dir():
            os.startfile(str(Path(path).resolve()))

    def close(self):
        if self.thread and self.thread.is_alive():
            self.closing = True
            self.stop()
        else:
            self.max_request += 1
            self.remember_settings()
            self.root.destroy()


def main():
    root = tk.Tk()
    BatchApp(root)
    root.mainloop()


if __name__ == '__main__':
    main()
