@echo off
REM SteamWatch 运行脚本
REM 请先运行 test.bat 进行环境初始化

echo 启动程序 SteamWatch...

REM 检查虚拟环境是否存在
if exist venv\Scripts\activate.bat (
    call venv\Scripts\activate.bat
    python -m steamwatch
) else (
    echo 错误: 虚拟环境不存在
    echo 请先运行 test.bat 进行初始化
    pause
)