"""
系统托盘模块
"""

import threading
from typing import Optional, Callable
import tkinter as tk
from tkinter import messagebox
from PIL import Image, ImageDraw

try:
    import pystray
    from pystray import MenuItem, Menu
except ImportError:
    pystray = None
    MenuItem = None
    Menu = None

from steamwatch.core.steam_monitor import SteamMonitor
from steamwatch.core.cache_reader import CacheReader
from steamwatch.core.time_tracker import TimeTracker
from steamwatch.utils.notification import Notifier
from steamwatch.ui.main_window import MainWindow


class TrayApp:
    """
    系统托盘应用
    
    管理系统托盘图标和菜单
    """
    
    def __init__(self):
        """初始化托盘应用"""
        self._icon: Optional[pystray.Icon] = None
        self._monitor: Optional[SteamMonitor] = None
        self._cache_reader: Optional[CacheReader] = None
        self._time_tracker: Optional[TimeTracker] = None
        self._notifier: Optional[Notifier] = None
        self._main_window: Optional[MainWindow] = None
        self._running = False
        
        self._setup_components()
        self._create_icon()
    
    def _setup_components(self) -> None:
        """设置组件"""
        self._cache_reader = CacheReader()
        self._time_tracker = TimeTracker()
        self._notifier = Notifier()
        
        self._monitor = SteamMonitor()
        self._monitor.on("game_start", self._on_game_start)
        self._monitor.on("game_stop", self._on_game_stop)
    
    def _create_icon_image(self) -> Image.Image:
        """创建托盘图标"""
        width = 64
        height = 64
        color = (66, 133, 244, 255)
        
        image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        dc = ImageDraw.Draw(image)
        
        dc.ellipse([8, 8, 56, 56], fill=color)
        dc.rectangle([24, 20, 40, 44], fill=(255, 255, 255, 255))
        dc.rectangle([28, 36, 36, 48], fill=(255, 255, 255, 255))
        
        return image
    
    def _create_icon(self) -> None:
        """创建托盘图标"""
        if pystray is None:
            raise ImportError("pystray is required for system tray functionality")
        
        image = self._create_icon_image()
        
        menu = Menu(
            MenuItem("打开主窗口", self._show_main_window, default=True),
            Menu.SEPARATOR,
            MenuItem("设置", self._show_settings),
            MenuItem("关于", self._show_about),
            Menu.SEPARATOR,
            MenuItem("退出", self._quit),
        )
        
        self._icon = pystray.Icon(
            "SteamWatch",
            image,
            "SteamWatch",
            menu
        )
    
    def run(self) -> None:
        """运行应用"""
        if self._icon is None:
            raise RuntimeError("Icon not created")
        
        self._running = True
        self._cache_reader.refresh()
        self._monitor.start()
        
        self._icon.run()
    
    def stop(self) -> None:
        """停止应用"""
        self._running = False
        if self._monitor:
            self._monitor.stop()
        if self._icon:
            self._icon.stop()
    
    def _show_main_window(self) -> None:
        """显示主窗口"""
        if self._main_window is None:
            self._main_window = MainWindow(
                self._time_tracker,
                self._cache_reader,
                self._monitor
            )
        
        self._main_window.show()
    
    def _show_settings(self) -> None:
        """显示设置窗口"""
        self._show_main_window()
        if self._main_window:
            self._main_window.show_settings_tab()
    
    def _show_about(self) -> None:
        """显示关于对话框"""
        messagebox.showinfo(
            "关于 SteamWatch",
            "SteamWatch v0.1.0\n\n"
            "Steam游戏时长监控与限制工具\n\n"
            "https://github.com/your-username/SteamWatch"
        )
    
    def _quit(self) -> None:
        """退出应用"""
        self.stop()
    
    def _on_game_start(self, app_id: int) -> None:
        """游戏启动回调"""
        game_info = self._cache_reader.get_game_info(app_id) if self._cache_reader else None
        game_name = game_info.name if game_info else f"App {app_id}"
        
        if self._notifier:
            self._notifier.notify(
                "游戏启动",
                f"{game_name} 开始运行"
            )
    
    def _on_game_stop(self, app_id: int) -> None:
        """游戏停止回调"""
        pass