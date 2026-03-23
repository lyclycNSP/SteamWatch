"""
Steam进程监控模块
"""

import psutil
from typing import Optional, Set, List
import time
from threading import Thread, Event
from dataclasses import dataclass


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
    STEAM_GAME_PREFIX = "steam_app_"
    
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
        self._game_processes: dict[int, GameProcess] = {}
        self._callbacks: dict[str, List[callable]] = {
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
    
    def _check_games(self) -> None:
        """检查游戏进程"""
        current_games: Set[int] = set()
        
        for proc in psutil.process_iter(["pid", "name", "cmdline"]):
            try:
                app_id = self._extract_game_app_id(proc)
                if app_id:
                    current_games.add(app_id)
                    if app_id not in self._running_games:
                        self._on_game_start(app_id, proc.pid, proc.info["name"])
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        
        stopped_games = self._running_games - current_games
        for app_id in stopped_games:
            self._on_game_stop(app_id)
        
        self._running_games = current_games
    
    def _extract_game_app_id(self, proc: psutil.Process) -> Optional[int]:
        """
        从进程中提取游戏AppID
        
        Steam游戏进程通常有以下特征：
        1. 进程名可能是游戏名或steam_app_xxx
        2. 命令行参数可能包含steam://rungameid/xxx
        """
        try:
            name = proc.info.get("name", "")
            if name:
                name_lower = name.lower()
                if name_lower.startswith(self.STEAM_GAME_PREFIX):
                    try:
                        return int(name_lower.replace(self.STEAM_GAME_PREFIX, "").replace(".exe", ""))
                    except ValueError:
                        pass
            
            cmdline = proc.info.get("cmdline") or []
            for arg in cmdline:
                if "steam://rungameid/" in arg.lower():
                    try:
                        app_id_str = arg.split("/")[-1]
                        return int(app_id_str)
                    except (ValueError, IndexError):
                        pass
        except Exception:
            pass
        
        return None
    
    def _on_game_start(self, app_id: int, pid: int, name: str) -> None:
        """游戏启动回调"""
        self._game_processes[app_id] = GameProcess(
            pid=pid,
            name=name,
            app_id=app_id,
            start_time=time.time()
        )
        self._notify("game_start", app_id)
    
    def _on_game_stop(self, app_id: int) -> None:
        """游戏停止回调"""
        if app_id in self._game_processes:
            del self._game_processes[app_id]
        self._notify("game_stop", app_id)
    
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