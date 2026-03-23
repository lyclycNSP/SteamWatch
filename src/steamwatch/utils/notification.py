"""
通知工具模块
"""

import platform
from typing import Optional


class Notifier:
    """
    通知器
    
    发送系统通知和播放提醒声音
    """
    
    def __init__(self):
        """初始化通知器"""
        self._system = platform.system()
    
    def notify(
        self,
        title: str,
        message: str,
        sound: bool = True,
        urgency: str = "normal"
    ) -> bool:
        """
        发送通知
        
        Args:
            title: 标题
            message: 消息内容
            sound: 是否播放声音
            urgency: 紧急程度 (low, normal, critical)
            
        Returns:
            是否成功发送
        """
        try:
            if self._system == "Windows":
                return self._notify_windows(title, message, sound)
            else:
                return self._notify_generic(title, message)
        except Exception as e:
            print(f"Notification error: {e}")
            return False
    
    def _notify_windows(self, title: str, message: str, sound: bool) -> bool:
        """Windows通知"""
        try:
            from win10toast import ToastNotifier
            toaster = ToastNotifier()
            toaster.show_toast(
                title,
                message,
                duration=5,
                threaded=True
            )
            return True
        except ImportError:
            try:
                import win32api
                win32api.MessageBox(0, message, title, 0x40)
                return True
            except ImportError:
                return self._notify_generic(title, message)
    
    def _notify_generic(self, title: str, message: str) -> bool:
        """通用通知方式"""
        print(f"[{title}] {message}")
        return True
    
    def play_sound(self, sound_type: str = "default") -> bool:
        """
        播放提醒声音
        
        Args:
            sound_type: 声音类型 (default, warning, alert)
            
        Returns:
            是否成功播放
        """
        try:
            from playsound import playsound
            
            sound_files = {
                "default": "assets/default.wav",
                "warning": "assets/warning.wav",
                "alert": "assets/alert.wav",
            }
            
            sound_file = sound_files.get(sound_type, sound_files["default"])
            
            try:
                playsound(sound_file)
                return True
            except Exception:
                if self._system == "Windows":
                    import winsound
                    winsound.MessageBeep()
                    return True
                return False
        except Exception as e:
            print(f"Sound error: {e}")
            return False
    
    def escalate_notify(
        self,
        title: str,
        message: str,
        level: int = 1
    ) -> bool:
        """
        渐强通知
        
        Args:
            title: 标题
            message: 消息内容
            level: 级别 (1-3)
            
        Returns:
            是否成功发送
        """
        urgencies = ["low", "normal", "critical"]
        urgency = urgencies[min(level - 1, len(urgencies) - 1)]
        
        result = self.notify(title, message, sound=True, urgency=urgency)
        
        if level >= 2:
            self.play_sound("warning")
        if level >= 3:
            self.play_sound("alert")
        
        return result