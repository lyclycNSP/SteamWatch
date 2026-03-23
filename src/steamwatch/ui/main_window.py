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

try:
    import matplotlib
    matplotlib.use('TkAgg')
    from matplotlib.figure import Figure
    from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
    MATPLOTLIB_AVAILABLE = True
except ImportError:
    MATPLOTLIB_AVAILABLE = False


class MainWindow:
    """
    主窗口
    
    显示游戏列表、时长统计和设置
    """
    
    def __init__(
        self,
        time_tracker: TimeTracker,
        cache_reader: CacheReader,
        monitor: Optional[SteamMonitor] = None
    ):
        """
        初始化主窗口
        
        Args:
            time_tracker: 时长追踪器
            cache_reader: 缓存读取器
            monitor: Steam监控器
        """
        self._time_tracker = time_tracker
        self._cache_reader = cache_reader
        self._monitor = monitor
        
        self._root: Optional[tk.Tk] = None
        self._notebook: Optional[ttk.Notebook] = None
    
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
            self._notebook.select(1)
    
    def _create_window(self) -> None:
        """创建窗口"""
        self._root = tk.Tk()
        self._root.title("SteamWatch")
        self._root.geometry("800x600")
        self._root.minsize(600, 400)
        
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
        
        columns = ("name", "app_id", "today_time", "limit", "status")
        tree = ttk.Treeview(frame, columns=columns, show="headings", height=20)
        
        tree.heading("name", text="游戏名称")
        tree.heading("app_id", text="AppID")
        tree.heading("today_time", text="今日时长")
        tree.heading("limit", text="限制")
        tree.heading("status", text="状态")
        
        tree.column("name", width=300)
        tree.column("app_id", width=100)
        tree.column("today_time", width=100)
        tree.column("limit", width=100)
        tree.column("status", width=100)
        
        scrollbar = ttk.Scrollbar(frame, orient=tk.VERTICAL, command=tree.yview)
        tree.configure(yscrollcommand=scrollbar.set)
        
        tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        self._populate_games_tree(tree)
        
        return frame
    
    def _populate_games_tree(self, tree: ttk.Treeview) -> None:
        """填充游戏列表"""
        games = self._cache_reader.get_all_games()
        
        for game in games:
            today_time = self._time_tracker.get_game_time(game.app_id)
            limit = self._time_tracker.get_game_limit(game.app_id)
            
            limit_str = f"{limit.daily_limit}分钟" if limit and limit.daily_limit > 0 else "无限制"
            
            status = "正常"
            if limit and limit.daily_limit > 0:
                if today_time >= limit.daily_limit:
                    status = "已超限"
                elif today_time >= limit.daily_limit * 0.8:
                    status = "即将超限"
            
            today_str = f"{today_time}分钟"
            
            tree.insert("", tk.END, values=(
                game.name,
                game.app_id,
                today_str,
                limit_str,
                status
            ))
    
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
                frame,
                text="图表功能需要安装 matplotlib",
                foreground="gray"
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
            tree.insert("", tk.END, values=(
                record.date,
                f"{record.get_total_time()}分钟"
            ))
        
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
        
        ax.plot(dates, times, marker='o', linewidth=2, markersize=6, color='#4285f4')
        ax.fill_between(dates, times, alpha=0.3, color='#4285f4')
        
        ax.set_xlabel('日期', fontsize=10)
        ax.set_ylabel('时长（分钟）', fontsize=10)
        ax.set_title('每日游戏时长趋势', fontsize=12)
        ax.grid(True, linestyle='--', alpha=0.7)
        
        for i, (date, time) in enumerate(zip(dates, times)):
            ax.annotate(
                f'{time}',
                xy=(date, time),
                xytext=(0, 5),
                textcoords='offset points',
                ha='center',
                fontsize=8
            )
        
        fig.tight_layout()
        
        canvas = FigureCanvasTkAgg(fig, master=parent)
        canvas.draw()
        canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)
    
    def _create_settings_tab(self) -> ttk.Frame:
        """创建设置标签页"""
        frame = ttk.Frame(self._notebook)
        
        global_frame = ttk.LabelFrame(frame, text="全局设置")
        global_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(global_frame, text="每日总时长限制（分钟，0表示无限制）:").grid(row=0, column=0, padx=5, pady=5, sticky=tk.W)
        
        global_limit_var = tk.StringVar(value=str(self._time_tracker.get_global_limit()))
        global_limit_entry = ttk.Entry(global_frame, textvariable=global_limit_var, width=10)
        global_limit_entry.grid(row=0, column=1, padx=5, pady=5)
        
        def save_global_limit():
            try:
                limit = int(global_limit_var.get())
                self._time_tracker.set_global_limit(limit)
                messagebox.showinfo("成功", "全局限制已保存")
            except ValueError:
                messagebox.showerror("错误", "请输入有效数字")
        
        ttk.Button(global_frame, text="保存", command=save_global_limit).grid(row=0, column=2, padx=5, pady=5)
        
        notify_frame = ttk.LabelFrame(frame, text="提醒设置")
        notify_frame.pack(fill=tk.X, padx=10, pady=10)
        
        ttk.Label(notify_frame, text="提醒阈值（%）：").grid(row=0, column=0, padx=5, pady=5, sticky=tk.W)
        threshold_var = tk.StringVar(value="80")
        ttk.Entry(notify_frame, textvariable=threshold_var, width=10).grid(row=0, column=1, padx=5, pady=5)
        
        ttk.Label(notify_frame, text="渐强提醒：启用后会在接近限制时逐步加强提醒").grid(row=1, column=0, columnspan=3, padx=5, pady=5, sticky=tk.W)
        
        return frame
    
    def _create_status_bar(self) -> None:
        """创建状态栏"""
        status_frame = ttk.Frame(self._root)
        status_frame.pack(fill=tk.X, side=tk.BOTTOM)
        
        status_label = ttk.Label(status_frame, text="就绪")
        status_label.pack(side=tk.LEFT, padx=5)
    
    def _refresh_games(self) -> None:
        """刷新游戏列表"""
        self._cache_reader.refresh()
        messagebox.showinfo("刷新完成", f"已加载 {len(self._cache_reader.get_all_games())} 个游戏")
    
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
            frame,
            text="导出内容：游戏列表、时长记录、配置",
            foreground="gray"
        ).pack(pady=5)
        
        def do_export():
            file_format = format_var.get()
            
            if file_format == "json":
                file_path = filedialog.asksaveasfilename(
                    defaultextension=".json",
                    filetypes=[("JSON文件", "*.json")],
                    title="保存JSON文件"
                )
                if file_path:
                    self._export_to_json(file_path)
                    messagebox.showinfo("导出成功", f"数据已导出到：\n{file_path}")
            else:
                file_path = filedialog.asksaveasfilename(
                    defaultextension=".csv",
                    filetypes=[("CSV文件", "*.csv")],
                    title="保存CSV文件"
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
                    "playtime_forever": game.playtime_forever
                }
                for game in self._cache_reader.get_all_games()
            ],
            "records": [
                {
                    "date": record.date,
                    "total_time": record.get_total_time(),
                    "game_playtimes": record.game_playtimes
                }
                for record in records
            ],
            "global_limit": self._time_tracker.get_global_limit(),
            "game_limits": [
                {
                    "app_id": limit.app_id,
                    "name": limit.name,
                    "daily_limit": limit.daily_limit
                }
                for limit in [
                    self._time_tracker.get_game_limit(app_id)
                    for app_id in self._cache_reader._games.keys()
                ]
                if limit is not None
            ]
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
                detail = "; ".join([
                    f"{app_id}:{time}分钟"
                    for app_id, time in record.game_playtimes.items()
                ])
                writer.writerow([
                    record.date,
                    record.get_total_time(),
                    detail
                ])
    
    def _show_about(self) -> None:
        """显示关于对话框"""
        messagebox.showinfo(
            "关于 SteamWatch",
            "SteamWatch v0.1.0\n\n"
            "Steam游戏时长监控与限制工具\n\n"
            "https://github.com/your-username/SteamWatch"
        )