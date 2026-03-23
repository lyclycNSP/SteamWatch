#!/bin/bash
# SteamWatch 打包脚本

echo "========================================"
echo "SteamWatch Build Script"
echo "========================================"

# 检查 Python
if ! command -v python &> /dev/null; then
    echo "Error: Python not found"
    exit 1
fi

# 安装依赖
echo "Installing dependencies..."
pip install -r requirements.txt
pip install pyinstaller

# 创建资源目录
mkdir -p assets

# 打包
echo "Building executable..."
pyinstaller build.spec --clean

echo "========================================"
echo "Build complete!"
echo "Executable: dist/SteamWatch.exe"
echo "========================================"