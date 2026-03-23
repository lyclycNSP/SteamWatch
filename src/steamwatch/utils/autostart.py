"""
Windows开机自启动工具模块
"""

import sys
from pathlib import Path
from typing import Optional
import platform


class AutoStartManager:
    """
    开机自启动管理器
    
    管理 Windows 开机自启动设置
    """
    
    REGISTRY_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
    APP_NAME = "SteamWatch"
    
    def __init__(self):
        """初始化自启动管理器"""
        self._system = platform.system()
    
    def is_enabled(self) -> bool:
        """
        检查是否已启用开机自启动
        
        Returns:
            是否已启用
        """
        if self._system != "Windows":
            return False
        
        try:
            import winreg
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                self.REGISTRY_KEY,
                0,
                winreg.KEY_READ
            )
            
            try:
                value, _ = winreg.QueryValueEx(key, self.APP_NAME)
                winreg.CloseKey(key)
                return Path(value).exists() if value else False
            except FileNotFoundError:
                winreg.CloseKey(key)
                return False
        except Exception:
            return False
    
    def enable(self, executable_path: Optional[str] = None) -> bool:
        """
        启用开机自启动
        
        Args:
            executable_path: 可执行文件路径，默认使用当前程序
            
        Returns:
            是否成功启用
        """
        if self._system != "Windows":
            return False
        
        if executable_path is None:
            executable_path = sys.executable
            if getattr(sys, "frozen", False):
                pass
            else:
                import steamwatch
                package_path = Path(steamwatch.__file__).parent
                executable_path = f'"{sys.executable}" -m steamwatch'
        
        try:
            import winreg
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                self.REGISTRY_KEY,
                0,
                winreg.KEY_WRITE
            )
            
            winreg.SetValueEx(
                key,
                self.APP_NAME,
                0,
                winreg.REG_SZ,
                executable_path
            )
            winreg.CloseKey(key)
            return True
        except Exception as e:
            print(f"Enable auto-start failed: {e}")
            return False
    
    def disable(self) -> bool:
        """
        禁用开机自启动
        
        Returns:
            是否成功禁用
        """
        if self._system != "Windows":
            return False
        
        try:
            import winreg
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                self.REGISTRY_KEY,
                0,
                winreg.KEY_WRITE
            )
            
            try:
                winreg.DeleteValue(key, self.APP_NAME)
            except FileNotFoundError:
                pass
            
            winreg.CloseKey(key)
            return True
        except Exception as e:
            print(f"Disable auto-start failed: {e}")
            return False
    
    def toggle(self) -> bool:
        """
        切换开机自启动状态
        
        Returns:
            切换后的状态
        """
        if self.is_enabled():
            self.disable()
            return False
        else:
            self.enable()
            return True