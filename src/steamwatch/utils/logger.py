"""
日志系统模块
"""

import logging
import sys
from pathlib import Path
from datetime import datetime
from typing import Optional


class Logger:
    """
    日志管理器
    
    提供统一的日志记录功能
    """
    
    _instance: Optional["Logger"] = None
    _logger: Optional[logging.Logger] = None
    
    def __new__(cls) -> "Logger":
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance
    
    def setup(
        self,
        name: str = "SteamWatch",
        log_dir: Optional[Path] = None,
        level: int = logging.INFO,
        console: bool = True
    ) -> logging.Logger:
        """
        设置日志系统
        
        Args:
            name: 日志名称
            log_dir: 日志目录
            level: 日志级别
            console: 是否输出到控制台
            
        Returns:
            配置好的 Logger 实例
        """
        if self._logger is not None:
            return self._logger
        
        self._logger = logging.getLogger(name)
        self._logger.setLevel(level)
        
        formatter = logging.Formatter(
            fmt="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S"
        )
        
        if log_dir:
            log_dir = Path(log_dir)
            log_dir.mkdir(parents=True, exist_ok=True)
            
            log_file = log_dir / f"{name.lower()}_{datetime.now().strftime('%Y%m%d')}.log"
            file_handler = logging.FileHandler(log_file, encoding="utf-8")
            file_handler.setLevel(level)
            file_handler.setFormatter(formatter)
            self._logger.addHandler(file_handler)
        
        if console:
            console_handler = logging.StreamHandler(sys.stdout)
            console_handler.setLevel(level)
            console_handler.setFormatter(formatter)
            self._logger.addHandler(console_handler)
        
        return self._logger
    
    def get_logger(self) -> logging.Logger:
        """获取 Logger 实例"""
        if self._logger is None:
            return self.setup()
        return self._logger


def get_logger() -> logging.Logger:
    """获取全局 Logger 实例"""
    return Logger().get_logger()


def setup_logging(
    log_dir: Optional[Path] = None,
    level: int = logging.INFO,
    console: bool = True
) -> logging.Logger:
    """
    设置全局日志系统
    
    Args:
        log_dir: 日志目录
        level: 日志级别
        console: 是否输出到控制台
        
    Returns:
        Logger 实例
    """
    return Logger().setup(
        log_dir=log_dir,
        level=level,
        console=console
    )