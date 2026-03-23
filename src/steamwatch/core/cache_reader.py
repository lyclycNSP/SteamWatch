"""
Steam缓存读取模块
"""

import os
import re
import sys
from pathlib import Path
from typing import Optional, Dict, List
from dataclasses import dataclass

if sys.platform == "win32":
    import winreg


@dataclass
class GameInfo:
    """游戏信息"""

    app_id: int
    name: str
    playtime_forever: int
    playtime_two_weeks: int = 0
    icon_hash: Optional[str] = None
    logo_hash: Optional[str] = None


class CacheReader:
    """Steam本地缓存读取器"""

    def __init__(self, steam_path: Optional[str] = None):
        self.steam_path = Path(steam_path) if steam_path else self._find_steam_path()
        self._games: Dict[int, GameInfo] = {}
        self._current_user_id: Optional[str] = None

    def _find_steam_path(self) -> Path:
        """自动查找Steam安装路径"""
        # 优先从Windows注册表读取（支持任意安装位置）
        if sys.platform == "win32":
            try:
                key = winreg.OpenKey(
                    winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam"
                )
                steam_path, _ = winreg.QueryValueEx(key, "InstallPath")
                return Path(steam_path)
            except Exception:
                pass

        # 备用：扫描常见位置
        common_paths = []
        for drive in ["C", "D", "E", "F", "G"]:
            common_paths.extend(
                [
                    Path(f"{drive}:/Program Files (x86)/Steam"),
                    Path(f"{drive}:/Program Files/Steam"),
                    Path(f"{drive}:/Steam"),
                    Path(f"{drive}:/Games/Steam"),
                    Path(f"{drive}:/SteamLibrary"),
                ]
            )

        for path in common_paths:
            if path.exists():
                return path

        return Path("C:/Program Files (x86)/Steam")

    def get_current_user_id(self) -> Optional[str]:
        """获取当前登录用户的Steam ID"""
        loginusers_path = self.steam_path / "config" / "loginusers.vdf"

        if loginusers_path.exists():
            try:
                with open(loginusers_path, "r", encoding="utf-8") as f:
                    content = f.read()
                matches = re.findall(r'"(\d{17})"\s*\{[^}]*"MostRecent"\s*"1"', content)
                if matches:
                    steam_id_64 = matches[0]
                    steam_id_32 = str(int(steam_id_64) - 76561197960265728)
                    self._current_user_id = steam_id_32
                    return self._current_user_id
            except Exception:
                pass

        try:
            users = list((self.steam_path / "userdata").iterdir())
            if users:
                self._current_user_id = users[0].name
                return self._current_user_id
        except Exception:
            pass

        return None

    def get_user_data_path(self, user_id: Optional[str] = None) -> Optional[Path]:
        """获取用户数据目录路径"""
        if user_id is None:
            user_id = self._current_user_id or self.get_current_user_id()

        if not user_id:
            return None

        user_data_path = self.steam_path / "userdata" / user_id
        if user_data_path.exists():
            return user_data_path

        return None

    def read_game_list(self, user_id: Optional[str] = None) -> Dict[int, GameInfo]:
        """读取游戏列表"""
        user_data_path = self.get_user_data_path(user_id)
        if not user_data_path:
            print("[CacheReader] 无法找到用户数据目录")
            return {}

        playtimes: Dict[int, int] = {}
        localconfig_path = user_data_path / "config" / "localconfig.vdf"

        if localconfig_path.exists():
            try:
                with open(localconfig_path, "r", encoding="utf-8") as f:
                    content = f.read()
                pattern = r'"(\d+)"[\s\S]*?"Playtime"\s*"(\d+)"'
                matches = re.findall(pattern, content)
                for app_id_str, playtime_str in matches:
                    playtimes[int(app_id_str)] = int(playtime_str)
            except Exception:
                pass

        steamapps_path = self.steam_path / "steamapps"
        if steamapps_path.exists():
            for acf_file in steamapps_path.glob("appmanifest_*.acf"):
                try:
                    with open(acf_file, "r", encoding="utf-8") as f:
                        content = f.read()

                    app_id_match = re.search(r'"appid"\s*"(\d+)"', content)
                    name_match = re.search(r'"name"\s*"([^"]+)"', content)

                    if app_id_match and name_match:
                        app_id = int(app_id_match.group(1))
                        name = name_match.group(1)
                        playtime = playtimes.get(app_id, 0)

                        self._games[app_id] = GameInfo(
                            app_id=app_id, name=name, playtime_forever=playtime
                        )
                except Exception:
                    continue

        print(f"[CacheReader] 已加载 {len(self._games)} 个游戏")
        return self._games

    def get_game_info(self, app_id: int) -> Optional[GameInfo]:
        """获取指定游戏的信息"""
        return self._games.get(app_id)

    def get_all_games(self) -> List[GameInfo]:
        """获取所有游戏列表"""
        return list(self._games.values())

    def get_game_icon_path(self, app_id: int) -> Optional[Path]:
        """获取游戏图标路径"""
        icon_cache_dir = self.steam_path / "appcache" / "librarycache"

        if not icon_cache_dir.exists():
            return None

        icon_patterns = [
            f"{app_id}_icon.jpg",
            f"{app_id}_icon.png",
            f"{app_id}_header.jpg",
        ]

        for pattern in icon_patterns:
            icon_path = icon_cache_dir / pattern
            if icon_path.exists():
                return icon_path

        return None

    def refresh(self) -> None:
        """刷新游戏数据"""
        self._games.clear()
        self.get_current_user_id()
        self.read_game_list()
