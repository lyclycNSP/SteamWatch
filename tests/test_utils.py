"""
工具模块测试
"""

import pytest
from unittest.mock import Mock, patch, MagicMock
from pathlib import Path
import logging

from steamwatch.utils.storage import Storage
from steamwatch.utils.logger import Logger, get_logger, setup_logging


class TestStorage:
    """Storage测试类"""
    
    def test_init(self, tmp_path):
        """测试初始化"""
        storage = Storage(data_dir=tmp_path)
        assert storage.data_dir == tmp_path
    
    def test_save_and_load(self, tmp_path):
        """测试保存和加载"""
        storage = Storage(data_dir=tmp_path)
        
        data = {"key": "value", "number": 123}
        assert storage.save("test", data)
        
        loaded = storage.load("test")
        assert loaded == data
    
    def test_load_nonexistent(self, tmp_path):
        """测试加载不存在的数据"""
        storage = Storage(data_dir=tmp_path)
        
        result = storage.load("nonexistent", default={"default": True})
        assert result == {"default": True}
    
    def test_delete(self, tmp_path):
        """测试删除"""
        storage = Storage(data_dir=tmp_path)
        
        storage.save("test", {"data": "value"})
        assert storage.exists("test")
        
        storage.delete("test")
        assert not storage.exists("test")


class TestLogger:
    """Logger测试类"""
    
    def test_singleton(self):
        """测试单例"""
        logger1 = Logger()
        logger2 = Logger()
        assert logger1 is logger2
    
    def test_setup(self, tmp_path):
        """测试设置"""
        Logger._logger = None
        Logger._instance = None
        
        logger = setup_logging(log_dir=tmp_path, console=False)
        assert logger is not None
        assert isinstance(logger, logging.Logger)
    
    def test_get_logger(self):
        """测试获取日志器"""
        Logger._logger = None
        Logger._instance = None
        
        logger = get_logger()
        assert logger is not None