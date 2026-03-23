"""
缓存读取模块测试
"""

import pytest
from unittest.mock import Mock, patch
from steamwatch.core.cache_reader import CacheReader, GameInfo


class TestCacheReader:
    """CacheReader测试类"""
    
    def test_init(self):
        """测试初始化"""
        reader = CacheReader()
        assert reader._games == {}
    
    def test_get_game_info_not_found(self):
        """测试获取不存在的游戏信息"""
        reader = CacheReader()
        info = reader.get_game_info(99999)
        assert info is None
    
    def test_get_all_games_empty(self):
        """测试获取空游戏列表"""
        reader = CacheReader()
        games = reader.get_all_games()
        assert games == []


class TestGameInfo:
    """GameInfo测试类"""
    
    def test_game_info_creation(self):
        """测试游戏信息创建"""
        info = GameInfo(
            app_id=730,
            name="Counter-Strike 2",
            playtime_forever=1000
        )
        assert info.app_id == 730
        assert info.name == "Counter-Strike 2"
        assert info.playtime_forever == 1000