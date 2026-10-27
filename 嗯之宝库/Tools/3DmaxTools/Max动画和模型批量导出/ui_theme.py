"""Standard-library dark theme for the external export panel."""
import ctypes
import os
import tkinter as tk
from tkinter import ttk

BG = '#11161d'
PANEL = '#1a222c'
FIELD = '#111922'
TEXT = '#e7eef5'
MUTED = '#97a9bb'
LINE = '#303e4e'
ACCENT = '#68dfbf'


def dark_titlebar(window):
    """Use the native dark caption when Windows supports it."""
    if os.name != 'nt':
        return
    try:
        window.update_idletasks()
        user32 = ctypes.windll.user32
        user32.GetParent.argtypes = [ctypes.c_void_p]
        user32.GetParent.restype = ctypes.c_void_p
        hwnd = user32.GetParent(window.winfo_id())
        enabled = ctypes.c_int(1)
        for attribute in (20, 19):
            if ctypes.windll.dwmapi.DwmSetWindowAttribute(
                    ctypes.c_void_p(hwnd), attribute, ctypes.byref(enabled), ctypes.sizeof(enabled)) == 0:
                break
    except (AttributeError, OSError):
        pass


def apply_theme(root):
    root.configure(bg=BG)
    style = ttk.Style(root)
    # Native Vista controls ignore custom backgrounds; clam styles every state.
    style.theme_use('clam')
    style.configure('.', font=('Microsoft YaHei UI', 10), background=BG, foreground=TEXT,
                    bordercolor=LINE, lightcolor=LINE, darkcolor=LINE, focuscolor=ACCENT)
    style.configure('TFrame', background=BG)
    style.configure('Card.TFrame', background=PANEL)
    style.configure('TLabel', background=BG, foreground=TEXT)
    style.configure('Muted.TLabel', foreground=MUTED)
    style.configure('Title.TLabel', font=('Microsoft YaHei UI', 24, 'bold'))
    style.configure('Section.TLabel', font=('Microsoft YaHei UI', 11, 'bold'))
    style.configure('Card.TLabel', background=PANEL)
    style.configure('CardMuted.TLabel', background=PANEL, foreground=MUTED, font=('Microsoft YaHei UI', 9))
    style.configure('CardTitle.TLabel', background=PANEL, font=('Microsoft YaHei UI', 11, 'bold'))
    style.configure('Badge.TLabel', background='#1a3935', foreground=ACCENT, padding=(14, 7))
    style.configure('TButton', background='#293646', foreground=TEXT, borderwidth=0,
                    padding=(14, 7), focusthickness=1, focuscolor=ACCENT)
    style.map('TButton', background=[('disabled', '#1e2833'), ('pressed', '#43566a'), ('active', '#36495b')],
              foreground=[('disabled', '#677889')])
    style.configure('Accent.TButton', background=ACCENT, foreground='#102820', font=('Microsoft YaHei UI', 10, 'bold'))
    style.map('Accent.TButton', background=[('disabled', '#274a44'), ('pressed', '#48b99d'), ('active', '#8becd2')],
              foreground=[('disabled', '#6c938a'), ('!disabled', '#102820')])
    style.configure('Quiet.TButton', background=PANEL, foreground=MUTED, padding=(10, 8))
    style.configure('Danger.TButton', foreground='#ffaaa8')
    for name in ('TEntry', 'TCombobox', 'TSpinbox'):
        style.configure(name, fieldbackground=FIELD, background=FIELD, foreground=TEXT,
                        insertcolor=TEXT, arrowcolor=MUTED, borderwidth=1, padding=6,
                        selectbackground='#31574f', selectforeground=TEXT)
        style.map(name, fieldbackground=[('disabled', '#202934'), ('readonly', FIELD)],
                  foreground=[('disabled', '#718394')],
                  bordercolor=[('focus', ACCENT), ('!focus', LINE)],
                  arrowcolor=[('disabled', '#5b6d7d'), ('active', ACCENT)])
    style.configure('TCheckbutton', background=PANEL, foreground=MUTED, padding=(0, 4),
                    indicatorbackground=FIELD, indicatorforeground=ACCENT, indicatormargin=(0, 2, 8, 2))
    style.map('TCheckbutton', background=[('active', PANEL)], foreground=[('disabled', '#667888'), ('active', TEXT)],
              indicatorbackground=[('disabled', '#26323d'), ('selected', '#315b50'), ('!selected', FIELD)])
    style.configure('Treeview', background=PANEL, fieldbackground=PANEL, foreground=TEXT,
                    borderwidth=0, rowheight=34)
    style.map('Treeview', background=[('selected', '#2b514b')], foreground=[('selected', '#f1fffa')])
    style.configure('Treeview.Heading', background='#253140', foreground=MUTED,
                    font=('Microsoft YaHei UI', 9, 'bold'), padding=(10, 10), relief='flat')
    style.map('Treeview.Heading', background=[('active', '#2e3d4e')])
    style.configure('TScrollbar', background='#3a4b5c', troughcolor=PANEL, borderwidth=0,
                    arrowcolor=MUTED, arrowsize=13)
    style.map('TScrollbar', background=[('active', '#526b80')])
    style.configure('Horizontal.TProgressbar', background=ACCENT, troughcolor='#253140',
                    borderwidth=0, thickness=5)
    root.option_add('*TCombobox*Listbox.background', FIELD)
    root.option_add('*TCombobox*Listbox.foreground', TEXT)
    root.option_add('*TCombobox*Listbox.selectBackground', '#31574f')
    root.option_add('*TCombobox*Listbox.selectForeground', TEXT)
    root.option_add('*TCombobox*Listbox.font', ('Microsoft YaHei UI', 10))
    root.after(0, lambda: dark_titlebar(root))
    return style


class Tooltip:
    def __init__(self, widget, text):
        self.widget, self.text, self.pending, self.popup = widget, text, None, None
        widget.bind('<Enter>', self.schedule, add='+')
        widget.bind('<Leave>', self.hide, add='+')
        widget.bind('<ButtonPress>', self.hide, add='+')
        widget.bind('<Destroy>', self.hide, add='+')

    def schedule(self, _event=None):
        self.hide()
        self.pending = self.widget.after(650, self.show)

    def show(self):
        self.pending = None
        text = self.text() if callable(self.text) else self.text
        if not text:
            return
        self.popup = tip = tk.Toplevel(self.widget)
        tip.wm_overrideredirect(True)
        tip.wm_attributes('-topmost', True)
        tk.Label(tip, text=text, justify='left', wraplength=620, background='#2b3a49',
                 foreground=TEXT, font=('Microsoft YaHei UI', 9), padx=12, pady=9).pack()
        tip.update_idletasks()
        x = min(self.widget.winfo_rootx(), tip.winfo_screenwidth() - tip.winfo_reqwidth() - 16)
        y = min(self.widget.winfo_rooty() + self.widget.winfo_height() + 5,
                tip.winfo_screenheight() - tip.winfo_reqheight() - 50)
        tip.geometry('+{}+{}'.format(max(x, 0), max(y, 0)))

    def hide(self, _event=None):
        if self.pending:
            self.widget.after_cancel(self.pending)
            self.pending = None
        if self.popup:
            self.popup.destroy()
            self.popup = None
