"""
通知工具模块
"""

import platform
import subprocess
from typing import Optional


class Notifier:
    """通知器"""

    def __init__(self):
        self._system = platform.system()

    def notify(
        self, title: str, message: str, sound: bool = True, urgency: str = "normal"
    ) -> bool:
        """发送通知"""
        try:
            if self._system == "Windows":
                return self._notify_windows(title, message, sound)
            else:
                return self._notify_generic(title, message)
        except Exception as e:
            print(f"Notification error: {e}")
            return False

    def _notify_windows(self, title: str, message: str, sound: bool) -> bool:
        """Windows通知 - 使用PowerShell"""
        try:
            escaped_title = title.replace("'", "''")
            escaped_message = message.replace("'", "''")

            ps_script = f"""
            [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
            [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
            
            $template = @"
            <toast>
                <visual>
                    <binding template='ToastText02'>
                        <text id='1'>{escaped_title}</text>
                        <text id='2'>{escaped_message}</text>
                    </binding>
                </visual>
            </toast>
"@
            
            $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
            $xml.LoadXml($template)
            $toast = New-Object Windows.UI.Notifications.ToastNotification $xml
            [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("SteamWatch").Show($toast)
            """

            subprocess.run(
                ["powershell", "-Command", ps_script], capture_output=True, timeout=5
            )

            if sound:
                self._play_beep()

            return True
        except Exception:
            return self._notify_fallback(title, message, sound)

    def _notify_fallback(self, title: str, message: str, sound: bool) -> bool:
        """备用通知方式"""
        try:
            if sound:
                self._play_beep()
            print(f"\n{'=' * 40}")
            print(f"[{title}]")
            print(f"  {message}")
            print(f"{'=' * 40}\n")
            return True
        except Exception:
            return False

    def _play_beep(self) -> None:
        """播放系统提示音"""
        try:
            import winsound

            winsound.MessageBeep(winsound.MB_ICONEXCLAMATION)
        except Exception:
            pass

    def _notify_generic(self, title: str, message: str) -> bool:
        """通用通知"""
        print(f"[{title}] {message}")
        return True

    def play_sound(self, sound_type: str = "default") -> bool:
        """播放提醒声音"""
        try:
            import winsound

            if sound_type == "warning":
                winsound.MessageBeep(winsound.MB_ICONHAND)
            elif sound_type == "alert":
                winsound.MessageBeep(winsound.MB_ICONHAND)
            else:
                winsound.MessageBeep(winsound.MB_ICONEXCLAMATION)
            return True
        except Exception:
            return False

    def escalate_notify(self, title: str, message: str, level: int = 1) -> bool:
        """渐强通知"""
        result = self.notify(title, message, sound=True)

        if level >= 2:
            self.play_sound("warning")
        if level >= 3:
            self.play_sound("alert")

        return result
