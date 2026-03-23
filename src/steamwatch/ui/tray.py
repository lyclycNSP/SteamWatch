"""
系统托盘模块
"""

import threading
from typing import Optional
import tkinter as tk
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
    """系统托盘应用"""

    def __init__(self):
        self._icon = None
        self._monitor = None
        self._cache_reader = None
        self._time_tracker = None
        self._notifier = None
        self._main_window = None
        self._root = None
        self._running = False
        self._icon_thread = None

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
            raise ImportError("pystray is required")

        image = self._create_icon_image()

        menu = Menu(
            MenuItem("打开主窗口", self._show_main_window_callback, default=True),
            Menu.SEPARATOR,
            MenuItem("设置", self._show_settings_callback),
            MenuItem("关于", self._show_about_callback),
            Menu.SEPARATOR,
            MenuItem("退出", self._quit_callback),
        )

        self._icon = pystray.Icon("SteamWatch", image, "SteamWatch", menu)

    def run(self) -> None:
        """运行应用"""
        if self._icon is None:
            raise RuntimeError("Icon not created")

        self._running = True
        self._cache_reader.refresh()
        self._monitor.start()

        self._icon_thread = threading.Thread(target=self._run_icon, daemon=True)
        self._icon_thread.start()

        self._root = tk.Tk()
        self._root.withdraw()
        self._root.protocol("WM_DELETE_WINDOW", self._on_root_close)

        try:
            self._root.mainloop()
        finally:
            self._cleanup()

    def _run_icon(self) -> None:
        if self._icon:
            self._icon.run()

    def _on_root_close(self) -> None:
        self._quit_callback()

    def _cleanup(self) -> None:
        self._running = False
        if self._monitor:
            self._monitor.stop()
        if self._icon:
            self._icon.stop()

    def _show_main_window_callback(self) -> None:
        if self._root:
            self._root.after(0, self._show_main_window)

    def _show_settings_callback(self) -> None:
        if self._root:
            self._root.after(0, self._show_settings)

    def _show_about_callback(self) -> None:
        if self._notifier:
            self._notifier.notify(
                "关于 SteamWatch", "SteamWatch v0.1.0\nSteam游戏时长监控工具"
            )

    def _quit_callback(self) -> None:
        if self._root:
            self._root.after(0, self._quit)

    def _show_main_window(self) -> None:
        if self._main_window is None:
            self._main_window = MainWindow(
                self._time_tracker, self._cache_reader, self._monitor
            )
        self._main_window.show()

    def _show_settings(self) -> None:
        self._show_main_window()
        if self._main_window:
            self._main_window.show_settings_tab()

    def _quit(self) -> None:
        self._cleanup()
        if self._root:
            self._root.quit()

    def _on_game_start(self, app_id: int, process_name: str = "") -> None:
        """游戏启动回调"""
        real_app_id = self._find_real_app_id(app_id, process_name)
        game_info = (
            self._cache_reader.get_game_info(real_app_id)
            if self._cache_reader
            else None
        )
        game_name = game_info.name if game_info else process_name or f"App {app_id}"

        if self._notifier:
            self._notifier.notify("游戏启动", f"{game_name} 开始运行")

    def _on_game_stop(
        self, app_id: int, process_name: str = "", duration: float = 0.0
    ) -> None:
        """游戏停止回调"""
        real_app_id = self._find_real_app_id(app_id, process_name)

        playtime_minutes = max(1, int(duration / 60))

        if self._time_tracker:
            self._time_tracker.add_game_time(real_app_id, playtime_minutes)

            game_info = (
                self._cache_reader.get_game_info(real_app_id)
                if self._cache_reader
                else None
            )
            game_name = game_info.name if game_info else process_name or f"App {app_id}"

            print(f"[SteamWatch] 记录游戏时长: {game_name} - {playtime_minutes}分钟")

            if self._notifier:
                self._notifier.notify(
                    "游戏结束", f"{game_name} 本次运行 {playtime_minutes} 分钟"
                )

    def _find_real_app_id(self, detected_app_id: int, process_name: str) -> int:
        """根据进程名查找真实的AppID"""
        if not self._cache_reader or not process_name:
            return detected_app_id

        process_lower = process_name.lower().replace(".exe", "").replace("_", " ")

        for game in self._cache_reader.get_all_games():
            game_lower = game.name.lower().replace(":", "").replace("-", "")
            if process_lower in game_lower or game_lower in process_lower:
                return game.app_id

        return detected_app_id
