"""
设置管理模块
"""

import json
from pathlib import Path
from typing import Optional
from dataclasses import dataclass, asdict
from threading import Lock


@dataclass
class Settings:
    """应用设置"""
    steam_path: Optional[str] = None
    poll_interval: float = 5.0
    global_daily_limit: int = 0
    notification_enabled: bool = True
    sound_enabled: bool = True
    escalation_enabled: bool = True
    threshold_percent: int = 80
    start_with_windows: bool = False
    minimize_to_tray: bool = True
    
    SETTINGS_FILE = "settings.json"
    
    @classmethod
    def load(cls, data_dir: Optional[Path] = None) -> "Settings":
        """
        从文件加载设置
        
        Args:
            data_dir: 数据目录路径
            
        Returns:
            设置实例
        """
        data_dir = data_dir or Path("data")
        settings_file = data_dir / cls.SETTINGS_FILE
        
        if settings_file.exists():
            try:
                with open(settings_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    return cls(**{k: v for k, v in data.items() if hasattr(cls, k)})
            except Exception as e:
                print(f"Error loading settings: {e}")
        
        return cls()
    
    def save(self, data_dir: Optional[Path] = None) -> bool:
        """
        保存设置到文件
        
        Args:
            data_dir: 数据目录路径
            
        Returns:
            是否成功保存
        """
        data_dir = data_dir or Path("data")
        data_dir.mkdir(parents=True, exist_ok=True)
        settings_file = data_dir / self.SETTINGS_FILE
        
        try:
            with open(settings_file, "w", encoding="utf-8") as f:
                json.dump(asdict(self), f, ensure_ascii=False, indent=2)
            return True
        except Exception as e:
            print(f"Error saving settings: {e}")
            return False


_settings: Optional[Settings] = None
_lock = Lock()


def get_settings(data_dir: Optional[Path] = None, reload: bool = False) -> Settings:
    """
    获取设置实例（单例）
    
    Args:
        data_dir: 数据目录路径
        reload: 是否重新加载
        
    Returns:
        设置实例
    """
    global _settings
    
    with _lock:
        if _settings is None or reload:
            _settings = Settings.load(data_dir)
        return _settings