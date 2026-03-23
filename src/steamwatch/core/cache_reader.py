"""
Steam缓存读取模块
"""

import os
import json
import re
from pathlib import Path
from typing import Optional, Dict, List
from dataclasses import dataclass


@dataclass
class GameInfo:
    """游戏信息"""
    app_id: int
    name: str
    playtime_forever: int  # 总游玩时长（分钟）
    playtime_two_weeks: int = 0  # 近两周游玩时长（分钟）
    icon_hash: Optional[str] = None
    logo_hash: Optional[str] = None


class CacheReader:
    """
    Steam本地缓存读取器
    
    读取Steam本地缓存文件获取游戏列表和历史时长
    """
    
    STEAM_REGISTRY_PATH = Path("C:/Program Files (x86)/Steam/steamapps")
    USERDATA_DIR = "userdata"
    LOCALCONFIG_FILE = "localconfig.vdf"
    SHAREDDEPOT_FILE = "appinfo.vdf"
    
    def __init__(self, steam_path: Optional[str] = None):
        """
        初始化缓存读取器
        
        Args:
            steam_path: Steam安装路径，默认自动检测
        """
        self.steam_path = Path(steam_path) if steam_path else Path(self.STEAM_REGISTRY_PATH)
        self._games: Dict[int, GameInfo] = {}
        self._current_user_id: Optional[str] = None
    
    def find_steam_path(self) -> Optional[Path]:
        """查找Steam安装路径"""
        common_paths = [
            Path("C:/Program Files (x86)/Steam"),
            Path("C:/Program Files/Steam"),
            Path.home() / "AppData/Local/Steam",
        ]
        
        for path in common_paths:
            if path.exists():
                return path
        
        return None
    
    def get_current_user_id(self) -> Optional[str]:
        """
        获取当前登录用户的Steam ID
        
        Returns:
            Steam ID字符串，如果无法获取则返回None
        """
        loginusers_path = self.steam_path / "config" / "loginusers.vdf"
        
        if not loginusers_path.exists():
            return None
        
        try:
            with open(loginusers_path, "r", encoding="utf-8") as f:
                content = f.read()
                matches = re.findall(r'"(\d{17})"\s*\{[^}]*"MostRecent"\s*"1"', content)
                if matches:
                    self._current_user_id = matches[0]
                    return self._current_user_id
        except Exception as e:
            print(f"Error reading loginusers.vdf: {e}")
        
        return None
    
    def get_user_data_path(self, user_id: Optional[str] = None) -> Optional[Path]:
        """
        获取用户数据目录路径
        
        Args:
            user_id: Steam用户ID，如果为None则使用当前用户
            
        Returns:
            用户数据目录路径
        """
        if user_id is None:
            user_id = self._current_user_id or self.get_current_user_id()
        
        if not user_id:
            return None
        
        user_data_path = self.steam_path / self.USERDATA_DIR / user_id
        if user_data_path.exists():
            return user_data_path
        
        return None
    
    def read_game_list(self, user_id: Optional[str] = None) -> Dict[int, GameInfo]:
        """
        读取用户的游戏列表
        
        Args:
            user_id: Steam用户ID
            
        Returns:
            游戏信息字典，键为AppID
        """
        user_data_path = self.get_user_data_path(user_id)
        if not user_data_path:
            return {}
        
        localconfig_path = user_data_path / "config" / self.LOCALCONFIG_FILE
        
        if not localconfig_path.exists():
            return {}
        
        try:
            with open(localconfig_path, "r", encoding="utf-8") as f:
                content = f.read()
            
            self._parse_vdf_games(content)
            return self._games
        except Exception as e:
            print(f"Error reading game list: {e}")
            return {}
    
    def _parse_vdf_games(self, content: str) -> None:
        """
        解析VDF格式的游戏数据
        
        Args:
            content: VDF文件内容
        """
        games_pattern = r'"(\d+)"\s*\{[^}]*"name"\s*"([^"]+)"[^}]*"Playtime_forever"\s*"(\d+)"[^}]*\}'
        matches = re.findall(games_pattern, content, re.IGNORECASE)
        
        for match in matches:
            try:
                app_id = int(match[0])
                name = match[1]
                playtime = int(match[2])
                
                self._games[app_id] = GameInfo(
                    app_id=app_id,
                    name=name,
                    playtime_forever=playtime
                )
            except (ValueError, IndexError):
                continue
    
    def get_game_info(self, app_id: int) -> Optional[GameInfo]:
        """
        获取指定游戏的信息
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            游戏信息，如果不存在则返回None
        """
        return self._games.get(app_id)
    
    def get_all_games(self) -> List[GameInfo]:
        """
        获取所有游戏列表
        
        Returns:
            游戏信息列表
        """
        return list(self._games.values())
    
    def get_game_icon_path(self, app_id: int) -> Optional[Path]:
        """
        获取游戏图标路径
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            图标文件路径，如果不存在则返回None
        """
        icon_cache_dir = self.steam_path / "appcache" / "librarycache"
        
        if not icon_cache_dir.exists():
            return None
        
        icon_patterns = [
            f"{app_id}_icon.jpg",
            f"{app_id}_icon.png",
            f"{app_id}_icon.jpeg",
            f"{app_id}_header.jpg",
            f"{app_id}_capsule_236x236.jpg",
        ]
        
        for pattern in icon_patterns:
            icon_path = icon_cache_dir / pattern
            if icon_path.exists():
                return icon_path
        
        return None
    
    def get_game_icon_data(self, app_id: int) -> Optional[bytes]:
        """
        获取游戏图标数据
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            图标二进制数据，如果不存在则返回None
        """
        icon_path = self.get_game_icon_path(app_id)
        
        if icon_path and icon_path.exists():
            try:
                with open(icon_path, "rb") as f:
                    return f.read()
            except Exception as e:
                print(f"Error reading icon for app {app_id}: {e}")
        
        return None
    
    def refresh(self) -> None:
        """刷新游戏数据"""
        self._games.clear()
        self.get_current_user_id()
        self.read_game_list()