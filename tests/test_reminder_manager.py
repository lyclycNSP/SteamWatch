"""
提醒管理器测试
"""

import pytest
from unittest.mock import Mock, patch
from steamwatch.core.reminder_manager import ReminderManager, ReminderLevel, ReminderState


class TestReminderManager:
    """ReminderManager测试类"""
    
    def test_init(self):
        """测试初始化"""
        manager = ReminderManager()
        assert manager._states == {}
    
    def test_calculate_level(self):
        """测试计算提醒级别"""
        manager = ReminderManager()
        
        assert manager._calculate_level(0.5) == ReminderLevel.NONE
        assert manager._calculate_level(0.7) == ReminderLevel.FIRST_WARNING
        assert manager._calculate_level(0.85) == ReminderLevel.SECOND_WARNING
        assert manager._calculate_level(0.95) == ReminderLevel.FINAL_WARNING
        assert manager._calculate_level(1.0) == ReminderLevel.EXCEEDED
        assert manager._calculate_level(1.5) == ReminderLevel.EXCEEDED
    
    def test_check_and_notify_first_warning(self):
        """测试首次提醒"""
        notifier = Mock()
        manager = ReminderManager(notifier=notifier)
        
        level = manager.check_and_notify(
            app_id=730,
            game_name="Test Game",
            progress=0.7,
            current_time=1000.0
        )
        
        assert level == ReminderLevel.FIRST_WARNING
        assert notifier.escalate_notify.called
    
    def test_check_and_notify_no_reminder(self):
        """测试不发送提醒"""
        notifier = Mock()
        manager = ReminderManager(notifier=notifier)
        
        level = manager.check_and_notify(
            app_id=730,
            game_name="Test Game",
            progress=0.5,
            current_time=1000.0
        )
        
        assert level is None
    
    def test_reset(self):
        """测试重置"""
        manager = ReminderManager()
        manager._states[730] = ReminderState(app_id=730, game_name="Test")
        
        manager.reset(730)
        assert 730 not in manager._states
    
    def test_reset_all(self):
        """测试重置所有"""
        manager = ReminderManager()
        manager._states[730] = ReminderState(app_id=730, game_name="Test")
        manager._states[570] = ReminderState(app_id=570, game_name="Test2")
        
        manager.reset()
        assert len(manager._states) == 0


class TestReminderState:
    """ReminderState测试类"""
    
    def test_state_creation(self):
        """测试状态创建"""
        state = ReminderState(
            app_id=730,
            game_name="Counter-Strike 2"
        )
        assert state.app_id == 730
        assert state.game_name == "Counter-Strike 2"
        assert state.current_level == ReminderLevel.NONE
        assert state.reminder_count == 0