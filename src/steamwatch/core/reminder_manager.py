"""
提醒管理器模块

实现渐强提醒逻辑，管理提醒状态和间隔
"""

import time
from typing import Dict, Optional, Set
from dataclasses import dataclass
from threading import Lock
from enum import Enum

from steamwatch.utils.notification import Notifier


class ReminderLevel(Enum):
    """提醒级别"""
    NONE = 0
    FIRST_WARNING = 1      # 首次提醒（接近限制）
    SECOND_WARNING = 2     # 二次提醒（更接近）
    FINAL_WARNING = 3      # 最终提醒（即将超限）
    EXCEEDED = 4           # 已超限


@dataclass
class ReminderState:
    """提醒状态"""
    app_id: int
    game_name: str
    current_level: ReminderLevel = ReminderLevel.NONE
    last_reminder_time: float = 0.0
    reminder_count: int = 0


class ReminderManager:
    """
    提醒管理器
    
    管理游戏时长提醒，实现渐强提醒逻辑
    """
    
    DEFAULT_THRESHOLDS = {
        ReminderLevel.FIRST_WARNING: 0.7,    # 70% 时首次提醒
        ReminderLevel.SECOND_WARNING: 0.85,  # 85% 时二次提醒
        ReminderLevel.FINAL_WARNING: 0.95,   # 95% 时最终提醒
        ReminderLevel.EXCEEDED: 1.0,         # 100% 时超限提醒
    }
    
    DEFAULT_INTERVALS = {
        ReminderLevel.FIRST_WARNING: 600,    # 10分钟
        ReminderLevel.SECOND_WARNING: 300,   # 5分钟
        ReminderLevel.FINAL_WARNING: 120,    # 2分钟
        ReminderLevel.EXCEEDED: 60,          # 1分钟
    }
    
    def __init__(
        self,
        notifier: Optional[Notifier] = None,
        thresholds: Optional[Dict[ReminderLevel, float]] = None,
        intervals: Optional[Dict[ReminderLevel, int]] = None
    ):
        """
        初始化提醒管理器
        
        Args:
            notifier: 通知器实例
            thresholds: 各级别阈值（进度百分比）
            intervals: 各级别提醒间隔（秒）
        """
        self._notifier = notifier or Notifier()
        self._thresholds = thresholds or self.DEFAULT_THRESHOLDS.copy()
        self._intervals = intervals or self.DEFAULT_INTERVALS.copy()
        self._states: Dict[int, ReminderState] = {}
        self._notified_exceeded: Set[int] = set()
        self._lock = Lock()
    
    def check_and_notify(
        self,
        app_id: int,
        game_name: str,
        progress: float,
        current_time: float
    ) -> Optional[ReminderLevel]:
        """
        检查并发送提醒
        
        Args:
            app_id: 游戏AppID
            game_name: 游戏名称
            progress: 当前进度（0.0-1.0+）
            current_time: 当前时间戳
            
        Returns:
            发送的提醒级别，如果未发送则返回None
        """
        with self._lock:
            state = self._get_or_create_state(app_id, game_name)
            new_level = self._calculate_level(progress)
            
            if new_level <= state.current_level:
                if not self._should_remind(state, new_level, current_time):
                    return None
            
            if self._send_reminder(state, new_level, game_name, progress):
                state.current_level = new_level
                state.last_reminder_time = current_time
                state.reminder_count += 1
                return new_level
            
            return None
    
    def _get_or_create_state(self, app_id: int, game_name: str) -> ReminderState:
        """获取或创建提醒状态"""
        if app_id not in self._states:
            self._states[app_id] = ReminderState(
                app_id=app_id,
                game_name=game_name
            )
        return self._states[app_id]
    
    def _calculate_level(self, progress: float) -> ReminderLevel:
        """根据进度计算提醒级别"""
        if progress >= self._thresholds[ReminderLevel.EXCEEDED]:
            return ReminderLevel.EXCEEDED
        elif progress >= self._thresholds[ReminderLevel.FINAL_WARNING]:
            return ReminderLevel.FINAL_WARNING
        elif progress >= self._thresholds[ReminderLevel.SECOND_WARNING]:
            return ReminderLevel.SECOND_WARNING
        elif progress >= self._thresholds[ReminderLevel.FIRST_WARNING]:
            return ReminderLevel.FIRST_WARNING
        return ReminderLevel.NONE
    
    def _should_remind(
        self,
        state: ReminderState,
        level: ReminderLevel,
        current_time: float
    ) -> bool:
        """判断是否应该发送提醒"""
        interval = self._intervals.get(level, 300)
        return (current_time - state.last_reminder_time) >= interval
    
    def _send_reminder(
        self,
        state: ReminderState,
        level: ReminderLevel,
        game_name: str,
        progress: float
    ) -> bool:
        """发送提醒"""
        if level == ReminderLevel.NONE:
            return False
        
        percentage = int(progress * 100)
        
        messages = {
            ReminderLevel.FIRST_WARNING: f"已游玩 {percentage}%，请注意时间",
            ReminderLevel.SECOND_WARNING: f"已游玩 {percentage}%，即将达到限额",
            ReminderLevel.FINAL_WARNING: f"已游玩 {percentage}%，马上超限！",
            ReminderLevel.EXCEEDED: f"已超过限额！请休息一下",
        }
        
        title = f"SteamWatch - {game_name}"
        message = messages.get(level, "")
        
        self._notifier.escalate_notify(
            title=title,
            message=message,
            level=level.value
        )
        
        return True
    
    def reset(self, app_id: Optional[int] = None) -> None:
        """
        重置提醒状态
        
        Args:
            app_id: 游戏AppID，为None时重置所有
        """
        with self._lock:
            if app_id is not None:
                if app_id in self._states:
                    del self._states[app_id]
                self._notified_exceeded.discard(app_id)
            else:
                self._states.clear()
                self._notified_exceeded.clear()
    
    def mark_exceeded(self, app_id: int) -> None:
        """标记为已超限"""
        with self._lock:
            self._notified_exceeded.add(app_id)
    
    def is_exceeded_notified(self, app_id: int) -> bool:
        """是否已通知过超限"""
        with self._lock:
            return app_id in self._notified_exceeded
    
    def get_state(self, app_id: int) -> Optional[ReminderState]:
        """获取提醒状态"""
        with self._lock:
            return self._states.get(app_id)