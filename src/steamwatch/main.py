"""
SteamWatch 主入口模块
"""

import sys
from typing import Optional

from steamwatch.ui.tray import TrayApp


def main() -> Optional[int]:
    """
    主入口函数
    
    Returns:
        退出码，0表示正常退出
    """
    try:
        app = TrayApp()
        app.run()
        return 0
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main() or 0)