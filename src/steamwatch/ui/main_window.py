"""
主窗口模块
"""

import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from typing import Optional, List
from datetime import datetime, timedelta
import json
import csv

from steamwatch.core.steam_monitor import SteamMonitor
from steamwatch.core.cache_reader import CacheReader, GameInfo
from steamwatch.core.time_tracker import TimeTracker, GameTimeLimit, DailyRecord
from steamwatch.core.reminder_manager import ReminderManager

try:
    import matplotlib

    matplotlib.use("TkAgg")
    from matplotlib.figure import Figure
    from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
    import matplotlib.font_manager as fm

    CHINESE_FONT = None
    for font_name in ["SimHei", "Microsoft YaHei", "SimSun", "KaiTi"]:
        try:
            font_path = fm.findfont(fm.FontProperties(family=font_name))
            if "generic" not in font_path.lower():
                CHINESE_FONT = font_name
                break
        except:
            pass

    MATPLOTLIB_AVAILABLE = True
except ImportError:
    MATPLOTLIB_AVAILABLE = False
    CHINESE_FONT = None


class MainWindow:
    """
    主窗口

    显示游戏列表、时长统计和设置
    """

    def __init__(
        self,
        time_tracker: TimeTracker,
        cache_reader: CacheReader,
        monitor: Optional[SteamMonitor] = None,
    ):
        self._time_tracker = time_tracker
        self._cache_reader = cache_reader
        self._monitor = monitor

        self._root: Optional[tk.Tk] = None
        self._notebook: Optional[ttk.Notebook] = None
        self._games_tree: Optional[ttk.Treeview] = None
        self._games_data: dict = {}

    def show(self) -> None:
        """显示窗口"""
        if self._root is None:
            self._create_window()

        if self._root:
            self._root.deiconify()
            self._root.lift()
            self._root.focus_force()

    def hide(self) -> None:
        """隐藏窗口"""
        if self._root:
            self._root.withdraw()

    def show_settings_tab(self) -> None:
        """切换到设置标签页"""
        if self._notebook:
            self._notebook.select(2)

    def _create_window(self) -> None:
        """创建窗口"""
        self._root = tk.Tk()
        self._root.title("SteamWatch")
        self._root.geometry("900x650")
        self._root.minsize(700, 500)

        self._root.protocol("WM_DELETE_WINDOW", self.hide)

        self._create_menu()
        self._create_notebook()
        self._create_status_bar()

    def _create_menu(self) -> None:
        """创建菜单栏"""
        menubar = tk.Menu(self._root)

        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(label="刷新游戏列表", command=self._refresh_games)
        file_menu.add_separator()
        file_menu.add_command(label="导出数据...", command=self._export_data)
        file_menu.add_separator()
        file_menu.add_command(label="退出", command=self.hide)
        menubar.add_cascade(label="文件", menu=file_menu)

        help_menu = tk.Menu(menubar, tearoff=0)
        help_menu.add_command(label="关于", command=self._show_about)
        menubar.add_cascade(label="帮助", menu=help_menu)

        self._root.config(menu=menubar)

    def _create_notebook(self) -> None:
        """创建标签页"""
        self._notebook = ttk.Notebook(self._root)
        self._notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        games_frame = self._create_games_tab()
        self._notebook.add(games_frame, text="游戏列表")

        stats_frame = self._create_stats_tab()
        self._notebook.add(stats_frame, text="统计")

        settings_frame = self._create_settings_tab()
        self._notebook.add(settings_frame, text="设置")

    def _create_games_tab(self) -> ttk.Frame:
        """创建游戏列表标签页"""
        frame = ttk.Frame(self._notebook)

        toolbar = ttk.Frame(frame)
        toolbar.pack(fill=tk.X, padx=5, pady=5)

        ttk.Button(toolbar, text="🔄 刷新列表", command=self._refresh_games).pack(
            side=tk.LEFT, padx=5
        )

        ttk.Button(toolbar, text="➕ 设置限额", command=self._show_limit_dialog).pack(
            side=tk.LEFT, padx=5
        )

        ttk.Button(toolbar, text="➖ 取消限额", command=self._remove_game_limit).pack(
            side=tk.LEFT, padx=5
        )

        ttk.Separator(toolbar, orient=tk.VERTICAL).pack(
            side=tk.LEFT, fill=tk.Y, padx=10
        )

        self._game_count_label = ttk.Label(toolbar, text="共 0 个游戏")
        self._game_count_label.pack(side=tk.LEFT, padx=5)

        tree_frame = ttk.Frame(frame)
        tree_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        columns = ("name", "app_id", "today_time", "limit", "status")
        self._games_tree = ttk.Treeview(
            tree_frame, columns=columns, show="headings", height=20
        )

        self._games_tree.heading("name", text="游戏名称")
        self._games_tree.heading("app_id", text="AppID")
        self._games_tree.heading("today_time", text="今日时长")
        self._games_tree.heading("limit", text="限制")
        self._games_tree.heading("status", text="状态")

        self._games_tree.column("name", width=300)
        self._games_tree.column("app_id", width=100)
        self._games_tree.column("today_time", width=100)
        self._games_tree.column("limit", width=100)
        self._games_tree.column("status", width=100)

        scrollbar = ttk.Scrollbar(
            tree_frame, orient=tk.VERTICAL, command=self._games_tree.yview
        )
        self._games_tree.configure(yscrollcommand=scrollbar.set)

        self._games_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        self._populate_games_tree()

        self._games_tree.bind("<Double-1>", self._on_game_double_click)
        self._games_tree.bind("<Button-3>", self._show_game_context_menu)

        return frame

    def _populate_games_tree(self) -> None:
        """填充游戏列表"""
        for item in self._games_tree.get_children():
            self._games_tree.delete(item)
        self._games_data.clear()

        games = self._cache_reader.get_all_games()

        if hasattr(self, "_game_count_label"):
            self._game_count_label.config(text=f"共 {len(games)} 个游戏")

        for game in games:
            today_time = self._time_tracker.get_game_time(game.app_id)
            limit = self._time_tracker.get_game_limit(game.app_id)

            limit_str = (
                f"{limit.daily_limit}分钟"
                if limit and limit.daily_limit > 0
                else "无限制"
            )

            status = "正常"
            if limit and limit.daily_limit > 0:
                progress = today_time / limit.daily_limit
                if progress >= 1.0:
                    status = "已超限"
                elif progress >= 0.95:
                    status = "即将超限"
                elif progress >= 0.85:
                    status = "接近限制"

            today_str = f"{today_time}分钟"

            item_id = self._games_tree.insert(
                "",
                tk.END,
                values=(game.name, game.app_id, today_str, limit_str, status),
            )
            self._games_data[item_id] = game

    def _on_game_double_click(self, event) -> None:
        """双击游戏设置限额"""
        self._show_limit_dialog()

    def _show_game_context_menu(self, event) -> None:
        """显示右键菜单"""
        item = self._games_tree.identify_row(event.y)
        if item:
            self._games_tree.selection_set(item)
            menu = tk.Menu(self._root, tearoff=0)
            menu.add_command(label="设置限额", command=self._show_limit_dialog)
            menu.add_command(label="取消限额", command=self._remove_game_limit)
            menu.add_separator()
            menu.add_command(label="刷新", command=self._refresh_games)
            menu.post(event.x_root, event.y_root)

    def _show_limit_dialog(self) -> None:
        """显示限额设置对话框"""
        selection = self._games_tree.selection()
        if not selection:
            messagebox.showwarning("提示", "请先选择一个游戏")
            return

        item = selection[0]
        game = self._games_data.get(item)
        if not game:
            return

        dialog = tk.Toplevel(self._root)
        dialog.title(f"设置限额 - {game.name}")
        dialog.geometry("500x250")
        dialog.resizable(False, False)
        dialog.transient(self._root)
        dialog.grab_set()

        frame = ttk.Frame(dialog, padding=25)
        frame.pack(fill=tk.BOTH, expand=True)

        ttk.Label(frame, text=f"游戏：{game.name}", font=("Arial", 12, "bold")).pack(
            anchor=tk.W, pady=(0, 10)
        )

        ttk.Label(frame, text="").pack()

        limit_frame = ttk.Frame(frame)
        limit_frame.pack(fill=tk.X, pady=10)

        ttk.Label(limit_frame, text="每日限额（分钟）：", font=("Arial", 10)).pack(
            side=tk.LEFT
        )

        current_limit = self._time_tracker.get_game_limit(game.app_id)
        limit_value = (
            str(current_limit.daily_limit)
            if current_limit and current_limit.daily_limit > 0
            else ""
        )
        limit_var = tk.StringVar(value=limit_value)

        limit_entry = ttk.Entry(
            limit_frame, textvariable=limit_var, width=15, font=("Arial", 11)
        )
        limit_entry.pack(side=tk.LEFT, padx=10)
        limit_entry.focus()

        ttk.Label(
            frame, text="提示：输入0或不填表示不限制该游戏", foreground="gray"
        ).pack(anchor=tk.W, pady=(5, 0))

        def save_limit():
            try:
                value = limit_var.get().strip()
                print(f"[DEBUG] 输入值: '{value}'")

                limit = int(value) if value else 0
                print(f"[DEBUG] 转换后: {limit}")

                if limit < 0:
                    raise ValueError("限额不能为负数")

                self._time_tracker.set_game_limit(game.app_id, limit, game.name)

                saved_limit = self._time_tracker.get_game_limit(game.app_id)
                print(f"[DEBUG] 保存后读取: {saved_limit}")

                messagebox.showinfo(
                    "成功", f"已设置 {game.name} 的每日限额为 {limit} 分钟"
                )
                self._populate_games_tree()
                dialog.destroy()
            except ValueError as e:
                messagebox.showerror("错误", f"请输入有效的数字：{e}")

        def on_cancel():
            dialog.destroy()

        btn_frame = ttk.Frame(frame)
        btn_frame.pack(fill=tk.X, pady=25)

        ttk.Button(btn_frame, text="保存", command=save_limit, width=12).pack(
            side=tk.RIGHT, padx=5
        )
        ttk.Button(btn_frame, text="取消", command=on_cancel, width=12).pack(
            side=tk.RIGHT, padx=5
        )

        # 绑定回车键保存
        limit_entry.bind("<Return>", lambda e: save_limit())
        # 绑定ESC键取消
        dialog.bind("<Escape>", lambda e: on_cancel())

    def _remove_game_limit(self) -> None:
        """取消游戏限额"""
        selection = self._games_tree.selection()
        if not selection:
            return

        item = selection[0]
        game = self._games_data.get(item)
        if not game:
            return

        if messagebox.askyesno("确认", f"确定要取消 {game.name} 的限额设置吗？"):
            self._time_tracker.remove_game_limit(game.app_id)
            self._populate_games_tree()
            messagebox.showinfo("成功", f"已取消 {game.name} 的限额")

    def _create_stats_tab(self) -> ttk.Frame:
        """创建统计标签页"""
        frame = ttk.Frame(self._notebook)

        label = ttk.Label(frame, text="近7天游戏时长统计", font=("Arial", 14, "bold"))
        label.pack(pady=10)

        if MATPLOTLIB_AVAILABLE:
            chart_frame = ttk.Frame(frame)
            chart_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

            self._create_line_chart(chart_frame)
        else:
            ttk.Label(
                frame, text="图表功能需要安装 matplotlib", foreground="gray"
            ).pack(pady=20)

        stats_frame = ttk.Frame(frame)
        stats_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        columns = ("date", "total_time")
        tree = ttk.Treeview(stats_frame, columns=columns, show="headings", height=7)

        tree.heading("date", text="日期")
        tree.heading("total_time", text="总时长")

        tree.column("date", width=200)
        tree.column("total_time", width=200)

        records = self._time_tracker.get_recent_records(7)
        for record in records:
            tree.insert(
                "", tk.END, values=(record.date, f"{record.get_total_time()}分钟")
            )

        tree.pack(fill=tk.BOTH, expand=True)

        return frame

    def _create_line_chart(self, parent: ttk.Frame) -> None:
        """创建折线图"""
        records = self._time_tracker.get_recent_records(7)
        records.reverse()

        dates = [record.date[-5:] for record in records]
        times = [record.get_total_time() for record in records]

        fig = Figure(figsize=(8, 3), dpi=100)
        ax = fig.add_subplot(111)

        ax.plot(dates, times, marker="o", linewidth=2, markersize=6, color="#4285f4")
        ax.fill_between(dates, times, alpha=0.3, color="#4285f4")

        if CHINESE_FONT:
            ax.set_xlabel("日期", fontsize=10, fontfamily=CHINESE_FONT)
            ax.set_ylabel("时长（分钟）", fontsize=10, fontfamily=CHINESE_FONT)
            ax.set_title("每日游戏时长趋势", fontsize=12, fontfamily=CHINESE_FONT)
        else:
            ax.set_xlabel("Date", fontsize=10)
            ax.set_ylabel("Time (min)", fontsize=10)
            ax.set_title("Daily Game Time Trend", fontsize=12)

        ax.grid(True, linestyle="--", alpha=0.7)

        for i, (date, time_val) in enumerate(zip(dates, times)):
            ax.annotate(
                f"{time_val}",
                xy=(i, time_val),
                xytext=(0, 5),
                textcoords="offset points",
                ha="center",
                fontsize=8,
            )

        ax.set_xticks(range(len(dates)))
        ax.set_xticklabels(dates)

        fig.tight_layout()

        canvas = FigureCanvasTkAgg(fig, master=parent)
        canvas.draw()
        canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)

    def _create_settings_tab(self) -> ttk.Frame:
        """创建设置标签页"""
        frame = ttk.Frame(self._notebook)

        global_frame = ttk.LabelFrame(frame, text="全局设置")
        global_frame.pack(fill=tk.X, padx=10, pady=10)

        ttk.Label(global_frame, text="每日总时长限制（分钟，0表示无限制）:").grid(
            row=0, column=0, padx=5, pady=5, sticky=tk.W
        )

        global_limit_var = tk.StringVar(
            value=str(self._time_tracker.get_global_limit())
        )
        global_limit_entry = ttk.Entry(
            global_frame, textvariable=global_limit_var, width=10
        )
        global_limit_entry.grid(row=0, column=1, padx=5, pady=5)

        def save_global_limit():
            try:
                limit = int(global_limit_var.get())
                self._time_tracker.set_global_limit(limit)
                messagebox.showinfo("成功", "全局限制已保存")
            except ValueError:
                messagebox.showerror("错误", "请输入有效数字")

        ttk.Button(global_frame, text="保存", command=save_global_limit).grid(
            row=0, column=2, padx=5, pady=5
        )

        notify_frame = ttk.LabelFrame(frame, text="提醒设置")
        notify_frame.pack(fill=tk.X, padx=10, pady=10)

        ttk.Label(
            notify_frame, text="渐强提醒机制说明：", font=("Arial", 10, "bold")
        ).grid(row=0, column=0, columnspan=3, padx=5, pady=5, sticky=tk.W)

        reminder_info = """
当游戏时长接近限额时，系统会按以下进度发送渐强提醒：

  • 70% 时：首次提醒（"已游玩70%，请注意时间"）
  • 85% 时：二次提醒（"已游玩85%，即将达到限额"）
  • 95% 时：最终提醒（"已游玩95%，马上超限！"）
  • 100% 时：超限提醒（"已超过限额！请休息一下"）

提醒间隔会逐渐缩短，确保您注意到时长限制。
        """

        info_label = ttk.Label(
            notify_frame, text=reminder_info, justify=tk.LEFT, foreground="#333333"
        )
        info_label.grid(row=1, column=0, columnspan=3, padx=10, pady=5, sticky=tk.W)

        ttk.Separator(notify_frame, orient=tk.HORIZONTAL).grid(
            row=2, column=0, columnspan=3, sticky=tk.EW, padx=5, pady=10
        )

        game_limit_frame = ttk.LabelFrame(frame, text="游戏限额管理")
        game_limit_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        ttk.Label(
            game_limit_frame, text='在"游戏列表"标签页中：', font=("Arial", 10, "bold")
        ).pack(anchor=tk.W, padx=10, pady=5)

        tips = [
            "• 双击游戏行，可设置该游戏的每日限额",
            "• 右键点击游戏行，可设置或取消限额",
            "• 单个游戏限额与全局限额独立生效",
        ]

        for tip in tips:
            ttk.Label(game_limit_frame, text=tip, foreground="#555555").pack(
                anchor=tk.W, padx=20, pady=2
            )

        return frame

    def _create_status_bar(self) -> None:
        """创建状态栏"""
        status_frame = ttk.Frame(self._root)
        status_frame.pack(fill=tk.X, side=tk.BOTTOM)

        status_label = ttk.Label(
            status_frame, text="提示：双击游戏设置限额 | 右键查看更多选项"
        )
        status_label.pack(side=tk.LEFT, padx=5)

    def _refresh_games(self) -> None:
        """刷新游戏列表"""
        self._cache_reader.refresh()
        self._populate_games_tree()

    def _export_data(self) -> None:
        """导出数据"""
        export_window = tk.Toplevel(self._root)
        export_window.title("导出数据")
        export_window.geometry("400x200")
        export_window.resizable(False, False)

        frame = ttk.Frame(export_window, padding=20)
        frame.pack(fill=tk.BOTH, expand=True)

        ttk.Label(frame, text="选择导出格式：", font=("Arial", 11)).pack(pady=10)

        format_var = tk.StringVar(value="json")

        formats_frame = ttk.Frame(frame)
        formats_frame.pack(pady=10)

        ttk.Radiobutton(
            formats_frame, text="JSON", variable=format_var, value="json"
        ).pack(side=tk.LEFT, padx=10)
        ttk.Radiobutton(
            formats_frame, text="CSV", variable=format_var, value="csv"
        ).pack(side=tk.LEFT, padx=10)

        ttk.Label(
            frame, text="导出内容：游戏列表、时长记录、配置", foreground="gray"
        ).pack(pady=5)

        def do_export():
            file_format = format_var.get()

            if file_format == "json":
                file_path = filedialog.asksaveasfilename(
                    defaultextension=".json",
                    filetypes=[("JSON文件", "*.json")],
                    title="保存JSON文件",
                )
                if file_path:
                    self._export_to_json(file_path)
                    messagebox.showinfo("导出成功", f"数据已导出到：\n{file_path}")
            else:
                file_path = filedialog.asksaveasfilename(
                    defaultextension=".csv",
                    filetypes=[("CSV文件", "*.csv")],
                    title="保存CSV文件",
                )
                if file_path:
                    self._export_to_csv(file_path)
                    messagebox.showinfo("导出成功", f"数据已导出到：\n{file_path}")

            export_window.destroy()

        ttk.Button(frame, text="导出", command=do_export).pack(pady=10)

    def _export_to_json(self, file_path: str) -> None:
        """导出为JSON格式"""
        records = self._time_tracker.get_recent_records(30)

        data = {
            "export_time": datetime.now().isoformat(),
            "games": [
                {
                    "app_id": game.app_id,
                    "name": game.name,
                    "playtime_forever": game.playtime_forever,
                }
                for game in self._cache_reader.get_all_games()
            ],
            "records": [
                {
                    "date": record.date,
                    "total_time": record.get_total_time(),
                    "game_playtimes": record.game_playtimes,
                }
                for record in records
            ],
            "global_limit": self._time_tracker.get_global_limit(),
            "game_limits": [
                {
                    "app_id": limit.app_id,
                    "name": limit.name,
                    "daily_limit": limit.daily_limit,
                }
                for limit in [
                    self._time_tracker.get_game_limit(app_id)
                    for app_id in self._cache_reader._games.keys()
                ]
                if limit is not None
            ],
        }

        with open(file_path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)

    def _export_to_csv(self, file_path: str) -> None:
        """导出为CSV格式"""
        records = self._time_tracker.get_recent_records(30)

        with open(file_path, "w", encoding="utf-8-sig", newline="") as f:
            writer = csv.writer(f)

            writer.writerow(["日期", "总时长（分钟）", "游戏时长明细"])

            for record in records:
                detail = "; ".join(
                    [
                        f"{app_id}:{time}分钟"
                        for app_id, time in record.game_playtimes.items()
                    ]
                )
                writer.writerow([record.date, record.get_total_time(), detail])

    def _show_about(self) -> None:
        """显示关于对话框"""
        messagebox.showinfo(
            "关于 SteamWatch",
            "SteamWatch v0.1.0\n\n"
            "Steam游戏时长监控与限制工具\n\n"
            "https://github.com/your-username/SteamWatch",
        )
