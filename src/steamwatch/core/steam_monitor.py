"""
Steam进程监控模块
"""

import psutil
from typing import Optional, Set, List, Dict
import time
from threading import Thread, Event
from dataclasses import dataclass
import re


@dataclass
class GameProcess:
    """游戏进程信息"""

    pid: int
    name: str
    app_id: Optional[int] = None
    start_time: float = 0.0


class SteamMonitor:
    """
    Steam进程监控器

    监控Steam客户端和游戏进程的运行状态
    """

    STEAM_PROCESS_NAMES = ["steam.exe", "steam"]
    EXCLUDED_PROCESS_NAMES = [
        "steamwebhelper.exe",
        "steamservice.exe",
        "gameoverlayui.exe",
        "gameoverlayui64.exe",
        "steamerrorreporter.exe",
        "streaming_client.exe",
    ]

    def __init__(self, poll_interval: float = 5.0):
        """
        初始化监控器

        Args:
            poll_interval: 轮询间隔（秒）
        """
        self.poll_interval = poll_interval
        self._running = Event()
        self._thread: Optional[Thread] = None
        self._running_games: Set[int] = set()
        self._game_processes: Dict[int, GameProcess] = {}
        self._steam_pids: Set[int] = set()
        self._callbacks: Dict[str, List[callable]] = {
            "game_start": [],
            "game_stop": [],
            "steam_start": [],
            "steam_stop": [],
        }

    def start(self) -> None:
        """启动监控"""
        if self._thread and self._thread.is_alive():
            return

        self._running.set()
        self._thread = Thread(target=self._monitor_loop, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """停止监控"""
        self._running.clear()
        if self._thread:
            self._thread.join(timeout=2.0)
            self._thread = None

    def _monitor_loop(self) -> None:
        """监控循环"""
        steam_running = False

        while self._running.is_set():
            current_steam = self._is_steam_running()

            if current_steam != steam_running:
                steam_running = current_steam
                self._notify("steam_start" if steam_running else "steam_stop")

            if steam_running:
                self._update_steam_pids()
                self._check_games()

            time.sleep(self.poll_interval)

    def _is_steam_running(self) -> bool:
        """检查Steam是否运行"""
        for proc in psutil.process_iter(["name"]):
            try:
                name = proc.info["name"]
                if name and name.lower() in self.STEAM_PROCESS_NAMES:
                    return True
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return False

    def _update_steam_pids(self) -> None:
        """更新Steam主进程PID列表"""
        self._steam_pids.clear()
        for proc in psutil.process_iter(["pid", "name"]):
            try:
                name = proc.info.get("name", "")
                if name and name.lower() in self.STEAM_PROCESS_NAMES:
                    self._steam_pids.add(proc.info["pid"])
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue

    def _check_games(self) -> None:
        """检查游戏进程"""
        current_games: Set[int] = set()
        overlay_game_pid = self._get_overlay_game_pid()

        for proc in psutil.process_iter(["pid", "name", "cmdline", "ppid"]):
            try:
                name = proc.info.get("name", "")
                ppid = proc.info.get("ppid", 0)
                pid = proc.info.get("pid", 0)

                if not name:
                    continue

                name_lower = name.lower()

                if name_lower in self.EXCLUDED_PROCESS_NAMES:
                    continue

                if name_lower in self.STEAM_PROCESS_NAMES:
                    continue

                is_steam_child = ppid in self._steam_pids

                is_overlay_target = overlay_game_pid and pid == overlay_game_pid

                if is_steam_child or is_overlay_target:
                    app_id = self._extract_app_id_from_path(
                        proc.info.get("cmdline", [])
                    )

                    if app_id:
                        current_games.add(app_id)
                        if app_id not in self._running_games:
                            self._on_game_start(app_id, pid, name)
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue

        stopped_games = self._running_games - current_games
        for app_id in stopped_games:
            self._on_game_stop(app_id)

        self._running_games = current_games

    def _get_overlay_game_pid(self) -> Optional[int]:
        """从Steam Overlay进程获取游戏PID"""
        for proc in psutil.process_iter(["name", "cmdline"]):
            try:
                name = proc.info.get("name", "")
                if name and "gameoverlayui" in name.lower():
                    cmdline = proc.info.get("cmdline") or []
                    for i, arg in enumerate(cmdline):
                        if arg == "-pid" and i + 1 < len(cmdline):
                            try:
                                return int(cmdline[i + 1])
                            except (ValueError, IndexError):
                                pass
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return None

    def _extract_app_id_from_path(self, cmdline: List[str]) -> Optional[int]:
        """从命令行参数中提取AppID"""
        if not cmdline:
            return None

        for arg in cmdline:
            if "steam_app_" in arg.lower():
                match = re.search(r"steam_app_(\d+)", arg, re.IGNORECASE)
                if match:
                    return int(match.group(1))

            if "steamapps\\common" in arg.lower() or "steamapps/common" in arg.lower():
                return self._guess_app_id_from_name(cmdline[0] if cmdline else "")

        return self._guess_app_id_from_name(cmdline[0] if cmdline else "")

    def _guess_app_id_from_name(self, path: str) -> Optional[int]:
        """根据游戏路径猜测AppID（临时方案）"""
        return hash(path) % 1000000 if path else None

    def _on_game_start(self, app_id: int, pid: int, name: str) -> None:
        """游戏启动回调"""
        self._game_processes[app_id] = GameProcess(
            pid=pid, name=name, app_id=app_id, start_time=time.time()
        )
        print(f"[SteamWatch] 检测到游戏启动: {name} (AppID: {app_id})")
        self._notify("game_start", app_id, name)

    def _on_game_stop(self, app_id: int) -> None:
        """游戏停止回调"""
        proc_name = ""
        duration = 0.0
        if app_id in self._game_processes:
            proc = self._game_processes[app_id]
            proc_name = proc.name
            duration = time.time() - proc.start_time
            print(
                f"[SteamWatch] 检测到游戏停止: {proc_name} (运行时长: {int(duration / 60)}分钟)"
            )
            del self._game_processes[app_id]
        self._notify("game_stop", app_id, proc_name, duration)

    def on(self, event: str, callback: callable) -> None:
        """
        注册事件回调

        Args:
            event: 事件名称 (game_start, game_stop, steam_start, steam_stop)
            callback: 回调函数
        """
        if event in self._callbacks:
            self._callbacks[event].append(callback)

    def _notify(self, event: str, *args, **kwargs) -> None:
        """通知所有回调"""
        for callback in self._callbacks.get(event, []):
            try:
                callback(*args, **kwargs)
            except Exception as e:
                print(f"Callback error: {e}")

    def get_running_games(self) -> Set[int]:
        """获取正在运行的游戏AppID集合"""
        return self._running_games.copy()

    def get_game_playtime(self, app_id: int) -> float:
        """
        获取游戏本次运行时长（秒）

        Args:
            app_id: 游戏AppID

        Returns:
            运行时长（秒），如果游戏未运行则返回0
        """
        if app_id in self._game_processes:
            return time.time() - self._game_processes[app_id].start_time
        return 0.0
