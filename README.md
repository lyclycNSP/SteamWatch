# SteamWatch

<p align="center">
  <strong>Steam游戏时长监控与限制工具</strong>
</p>

<p align="center">
  <a href="#功能特性">功能特性</a> •
  <a href="#安装">安装</a> •
  <a href="#使用方法">使用方法</a> •
  <a href="#开发">开发</a> •
  <a href="#贡献">贡献</a> •
  <a href="#许可证">许可证</a>
</p>

---

## 功能特性

- **实时监控**: 监控Steam游戏进程，精确追踪当日游戏时长
- **时长限制**: 为每个游戏设置每日游玩时长上限
- **总时长控制**: 设置所有游戏的总时长限额
- **渐强提醒**: 接近限额时逐步加强提醒强度（70%→85%→95%→超限）
- **折线图表**: 近7天游戏时长趋势可视化展示
- **数据导出**: 支持JSON/CSV格式导出历史数据
- **系统托盘**: 轻量级后台运行，最小化到系统托盘
- **开机自启**: 可设置开机自动启动

## 系统要求

- Windows 10/11
- Steam客户端

## 安装

### 方式一：下载发布版本

从 [Releases](../../releases) 页面下载最新版本的ZIP压缩包，解压后直接运行 `SteamWatch.exe`。

### 方式二：从源码运行

```bash
# 克隆仓库
git clone https://github.com/your-username/SteamWatch.git
cd SteamWatch

# 创建虚拟环境
python -m venv venv
venv\Scripts\activate

# 安装依赖
pip install -r requirements.txt

# 运行
python -m steamwatch
```

## 使用方法

1. 启动SteamWatch，程序会自动最小化到系统托盘
2. 右键托盘图标打开主窗口
3. 在设置中配置：
   - 需要监控的游戏
   - 每个游戏的时长限额
   - 总时长限额
   - 开机自启动
4. 开始游戏后，SteamWatch会自动追踪时长并在接近限额时提醒

## 开发

### 环境设置

```bash
# 克隆仓库
git clone https://github.com/your-username/SteamWatch.git
cd SteamWatch

# 创建虚拟环境
python -m venv venv
venv\Scripts\activate

# 安装开发依赖
pip install -r requirements.txt
pip install -r requirements-dev.txt

# 运行测试
pytest

# 代码检查
flake8 src tests
mypy src
```

### 项目结构

```
SteamWatch/
├── src/steamwatch/       # 主代码
│   ├── core/            # 核心功能
│   │   ├── steam_monitor.py    # Steam进程监控
│   │   ├── cache_reader.py     # Steam缓存读取
│   │   ├── time_tracker.py     # 时长追踪
│   │   └── reminder_manager.py # 提醒管理
│   ├── ui/              # 用户界面
│   │   ├── tray.py             # 系统托盘
│   │   └── main_window.py      # 主窗口
│   ├── models/          # 数据模型
│   ├── utils/           # 工具函数
│   └── config/          # 配置管理
├── tests/               # 测试
├── docs/                # 文档
├── assets/              # 资源文件
├── build.spec           # 打包配置
├── build.bat            # Windows打包脚本
└── build.sh             # Linux/Mac打包脚本
```

### 打包

```bash
# Windows
build.bat

# 或手动执行
pip install pyinstaller
pyinstaller build.spec --clean
```

打包后的可执行文件位于 `dist/SteamWatch.exe`

## 贡献

欢迎贡献代码、报告问题或提出建议！请查看 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详情。

## 注意事项

- 部分杀毒软件可能会拦截进程监控功能，如遇到问题请将SteamWatch添加至白名单
- SteamWatch通过读取Steam本地缓存获取游戏列表和历史时长数据
- 本工具仅供个人自我管理使用

### ⚠️ Windows勿扰模式设置

全屏游戏时，Windows默认会启用勿扰模式，导致无法收到提醒通知。请按以下步骤关闭：

1. 按 `Win + i` 打开设置
2. 点击左侧 **系统** → 右侧 **通知**
3. 展开 **自动打开勿扰模式**
4. 取消勾选以下两项：
   - 玩游戏时
   - 全屏使用应用时
5. 确保顶部 **勿扰模式** 处于关闭状态

### 数据说明

- **今日时长**：每天0点自动重置，新的一天从0开始计算
- **限额设置**：永久保存，不会自动清零，需手动修改

## 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。

## 致谢

- 感谢所有贡献者
- 本项目使用以下开源库：
  - pystray - 系统托盘支持
  - tkinter - GUI框架
  - matplotlib - 图表绘制
  - psutil - 进程监控