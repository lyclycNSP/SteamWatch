"""
Steam监控模块测试
"""

import pytest
from unittest.mock import Mock, patch, MagicMock
from steamwatch.core.steam_monitor import SteamMonitor, GameProcess


class TestSteamMonitor:
    """SteamMonitor测试类"""
    
    def test_init(self):
        """测试初始化"""
        monitor = SteamMonitor(poll_interval=1.0)
        assert monitor.poll_interval == 1.0
    
    def test_on_event_registration(self):
        """测试事件注册"""
        monitor = SteamMonitor()
        callback = Mock()
        monitor.on("game_start", callback)
        assert callback in monitor._callbacks["game_start"]
    
    def test_get_running_games(self):
        """测试获取运行中的游戏"""
        monitor = SteamMonitor()
        running = monitor.get_running_games()
        assert isinstance(running, set)
        assert len(running) == 0


class TestGameProcess:
    """GameProcess测试类"""
    
    def test_game_process_creation(self):
        """测试游戏进程创建"""
        process = GameProcess(
            pid=1234,
            name="game.exe",
            app_id=730,
            start_time=1000.0
        )
        assert process.pid == 1234
        assert process.name == "game.exe"
        assert process.app_id == 730
        assert process.start_time == 1000.0