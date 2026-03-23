@echo off
REM SteamWatch 打包脚本

echo ========================================
echo SteamWatch Build Script
echo ========================================

REM 检查 Python
python --version >nul 2>&1
if errorlevel 1 (
    echo Error: Python not found
    exit /b 1
)

REM 安装依赖
echo Installing dependencies...
pip install -r requirements.txt
pip install pyinstaller

REM 创建资源目录
if not exist assets mkdir assets

REM 打包
echo Building executable...
pyinstaller build.spec --clean

echo ========================================
echo Build complete!
echo Executable: dist/SteamWatch.exe
echo ========================================
pause