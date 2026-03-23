"""
时长追踪模块测试
"""

import pytest
from unittest.mock import Mock, patch, MagicMock
from steamwatch.core.time_tracker import TimeTracker, DailyRecord, GameTimeLimit


class TestTimeTracker:
    """TimeTracker测试类"""
    
    def test_init(self, tmp_path):
        """测试初始化"""
        tracker = TimeTracker(data_dir=tmp_path)
        assert tracker._records == {}
        assert tracker._limits == {}
    
    def test_add_game_time(self, tmp_path):
        """测试添加游戏时长"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.add_game_time(730, 30)
        
        today_time = tracker.get_game_time(730)
        assert today_time == 30
    
    def test_set_game_time(self, tmp_path):
        """测试设置游戏时长"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.set_game_time(730, 60)
        
        today_time = tracker.get_game_time(730)
        assert today_time == 60
    
    def test_get_total_time(self, tmp_path):
        """测试获取总时长"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.add_game_time(730, 30)
        tracker.add_game_time(570, 20)
        
        total = tracker.get_total_time()
        assert total == 50
    
    def test_set_game_limit(self, tmp_path):
        """测试设置游戏限制"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.set_game_limit(730, 120, "Counter-Strike 2")
        
        limit = tracker.get_game_limit(730)
        assert limit is not None
        assert limit.daily_limit == 120
        assert limit.name == "Counter-Strike 2"
    
    def test_set_global_limit(self, tmp_path):
        """测试设置全局限制"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.set_global_limit(300)
        
        assert tracker.get_global_limit() == 300
    
    def test_check_limit(self, tmp_path):
        """测试检查限制"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.set_game_limit(730, 120)
        tracker.add_game_time(730, 100)
        
        result = tracker.check_limit(730)
        assert result["current_game_time"] == 100
        assert result["game_limit"] == 120
        assert result["game_exceeded"] is False
    
    def test_check_approaching_limit(self, tmp_path):
        """测试检查接近限制"""
        tracker = TimeTracker(data_dir=tmp_path)
        tracker.set_game_limit(730, 120)
        tracker.add_game_time(730, 100)
        
        result = tracker.check_approaching_limit(730, threshold=0.8)
        assert result["approaching_game_limit"] is True
        assert result["game_progress"] >= 0.8


class TestDailyRecord:
    """DailyRecord测试类"""
    
    def test_get_game_time(self):
        """测试获取游戏时长"""
        record = DailyRecord(
            date="2024-01-01",
            game_playtimes={730: 100, 570: 50}
        )
        assert record.get_game_time(730) == 100
        assert record.get_game_time(999) == 0
    
    def test_get_total_time(self):
        """测试获取总时长"""
        record = DailyRecord(
            date="2024-01-01",
            game_playtimes={730: 100, 570: 50}
        )
        assert record.get_total_time() == 150


class TestGameTimeLimit:
    """GameTimeLimit测试类"""
    
    def test_limit_creation(self):
        """测试限制创建"""
        limit = GameTimeLimit(
            app_id=730,
            daily_limit=120,
            name="Counter-Strike 2"
        )
        assert limit.app_id == 730
        assert limit.daily_limit == 120
        assert limit.name == "Counter-Strike 2"