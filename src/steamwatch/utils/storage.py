"""
数据存储模块
"""

import json
from pathlib import Path
from typing import Any, Optional
from threading import Lock


class Storage:
    """
    数据存储
    
    提供简单的JSON文件存储功能
    """
    
    def __init__(self, data_dir: Optional[Path] = None):
        """
        初始化存储
        
        Args:
            data_dir: 数据目录路径
        """
        self.data_dir = data_dir or Path("data")
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self._lock = Lock()
    
    def _get_file_path(self, key: str) -> Path:
        """获取存储文件路径"""
        return self.data_dir / f"{key}.json"
    
    def save(self, key: str, data: Any) -> bool:
        """
        保存数据
        
        Args:
            key: 存储键名
            data: 数据内容
            
        Returns:
            是否成功保存
        """
        file_path = self._get_file_path(key)
        
        with self._lock:
            try:
                with open(file_path, "w", encoding="utf-8") as f:
                    json.dump(data, f, ensure_ascii=False, indent=2)
                return True
            except Exception as e:
                print(f"Save error: {e}")
                return False
    
    def load(self, key: str, default: Any = None) -> Any:
        """
        加载数据
        
        Args:
            key: 存储键名
            default: 默认值
            
        Returns:
            数据内容
        """
        file_path = self._get_file_path(key)
        
        with self._lock:
            try:
                if file_path.exists():
                    with open(file_path, "r", encoding="utf-8") as f:
                        return json.load(f)
            except Exception as e:
                print(f"Load error: {e}")
        
        return default
    
    def delete(self, key: str) -> bool:
        """
        删除数据
        
        Args:
            key: 存储键名
            
        Returns:
            是否成功删除
        """
        file_path = self._get_file_path(key)
        
        with self._lock:
            try:
                if file_path.exists():
                    file_path.unlink()
                return True
            except Exception as e:
                print(f"Delete error: {e}")
                return False
    
    def exists(self, key: str) -> bool:
        """
        检查数据是否存在
        
        Args:
            key: 存储键名
            
        Returns:
            是否存在
        """
        return self._get_file_path(key).exists()