"""
游戏模型
"""

from dataclasses import dataclass
from enum import Enum
from typing import Optional


class GameStatus(Enum):
    """游戏状态"""
    NOT_RUNNING = "not_running"
    RUNNING = "running"
    LIMIT_EXCEEDED = "limit_exceeded"
    APPROACHING_LIMIT = "approaching_limit"


@dataclass
class Game:
    """游戏数据模型"""
    app_id: int
    name: str
    playtime_forever: int = 0
    playtime_two_weeks: int = 0
    daily_limit: int = 0
    today_playtime: int = 0
    status: GameStatus = GameStatus.NOT_RUNNING
    icon_hash: Optional[str] = None
    
    @property
    def is_limited(self) -> bool:
        """是否设置了限制"""
        return self.daily_limit > 0
    
    @property
    def limit_exceeded(self) -> bool:
        """是否超过限制"""
        return self.is_limited and self.today_playtime >= self.daily_limit
    
    @property
    def approaching_limit(self) -> bool:
        """是否接近限制（>=80%）"""
        if not self.is_limited:
            return False
        return self.today_playtime >= self.daily_limit * 0.8
    
    @property
    def remaining_time(self) -> int:
        """剩余时间（分钟）"""
        if not self.is_limited:
            return -1
        return max(0, self.daily_limit - self.today_playtime)
    
    @property
    def progress_percentage(self) -> float:
        """进度百分比"""
        if not self.is_limited:
            return 0.0
        return min(100.0, (self.today_playtime / self.daily_limit) * 100)