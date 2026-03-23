@echo off
REM SteamWatch 测试脚本

echo ========================================
echo SteamWatch 测试脚本
echo ========================================

REM 检查 Python 3.11
echo [1/5] 检查 Python 3.11...
py -3.11 --version
if errorlevel 1 (
    echo 错误: 未找到 Python 3.11，请先安装
    pause
    exit /b 1
)

REM 创建虚拟环境
echo.
echo [2/5] 创建虚拟环境...
if not exist venv (
    py -3.11 -m venv venv
    echo 虚拟环境创建成功
) else (
    echo 虚拟环境已存在
)

REM 激活虚拟环境
echo.
echo [3/5] 激活虚拟环境...
call venv\Scripts\activate.bat

REM 安装依赖
echo.
echo [4/5] 安装依赖...
pip install -r requirements.txt
if errorlevel 1 (
    echo 错误: 依赖安装失败
    pause
    exit /b 1
)

pip install -r requirements-dev.txt
if errorlevel 1 (
    echo 错误: 开发依赖安装失败
    pause
    exit /b 1
)

REM 运行测试
echo.
echo [5/5] 运行测试...
pytest tests/ -v

if errorlevel 1 (
    echo.
    echo ========================================
    echo 测试完成，有失败或错误
    echo ========================================
) else (
    echo.
    echo ========================================
    echo 测试全部通过
    echo ========================================
)

echo.
echo 现在可以运行程序:
echo   python -m steamwatch
echo.

pause