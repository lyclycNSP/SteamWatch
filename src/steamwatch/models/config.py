"""
应用配置模型
"""

from dataclasses import dataclass, field
from typing import Dict, Optional
from pathlib import Path


@dataclass
class NotificationConfig:
    """通知配置"""
    enabled: bool = True
    sound_enabled: bool = True
    escalation_enabled: bool = True
    threshold_percent: int = 80
    reminder_interval_minutes: int = 5


@dataclass
class AppConfig:
    """应用配置"""
    steam_path: Optional[str] = None
    poll_interval_seconds: float = 5.0
    global_daily_limit: int = 0
    notification: NotificationConfig = field(default_factory=NotificationConfig)
    game_limits: Dict[int, int] = field(default_factory=dict)
    data_dir: str = "data"
    start_with_windows: bool = False
    
    def get_data_path(self) -> Path:
        """获取数据目录路径"""
        return Path(self.data_dir)
    
    def get_game_limit(self, app_id: int) -> int:
        """
        获取游戏的每日限制
        
        Args:
            app_id: 游戏AppID
            
        Returns:
            每日限制（分钟），0表示无限制
        """
        return self.game_limits.get(app_id, 0)
    
    def set_game_limit(self, app_id: int, limit: int) -> None:
        """
        设置游戏的每日限制
        
        Args:
            app_id: 游戏AppID
            limit: 每日限制（分钟），0表示无限制
        """
        if limit > 0:
            self.game_limits[app_id] = limit
        elif app_id in self.game_limits:
            del self.game_limits[app_id]