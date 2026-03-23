# SteamWatch 开发记录

## v0.1.0 - 项目初始化

### 2024-01-XX 开发进度

#### 已完成

1. **项目初始化**
   - Git 仓库初始化（main 分支）
   - 项目目录结构创建
   - 基础配置文件（.gitignore, pyproject.toml, requirements.txt）

2. **核心模块开发**
   - SteamMonitor: 进程监控模块，检测 Steam 和游戏进程
   - CacheReader: Steam 本地缓存读取，获取游戏列表和历史时长
   - TimeTracker: 时长追踪模块，记录时长、设置限制、检查超限
   - ReminderManager: 提醒管理器，实现渐强提醒逻辑

3. **界面模块开发**
   - TrayApp: 系统托盘应用，管理托盘图标和菜单
   - MainWindow: 主窗口，游戏列表、统计、设置界面

4. **辅助模块开发**
   - Notifier: 通知模块，支持渐强提醒
   - Storage: JSON 文件存储工具
   - Settings: 应用设置管理
   - Logger: 日志系统
   - AutoStartManager: 开机自启动管理

5. **测试文件**
   - test_steam_monitor.py
   - test_cache_reader.py
   - test_time_tracker.py
   - test_reminder_manager.py
   - test_utils.py

6. **文档**
   - README.md
   - LICENSE (MIT)
   - CONTRIBUTING.md
   - CHANGELOG.md
   - docs/architecture.md
   - docs/dev-log.md

7. **打包配置**
   - build.spec (PyInstaller配置)
   - build.bat (Windows打包脚本)
   - build.sh (Linux/Mac打包脚本)

#### 待完成

1. **核心功能完善**
   - [x] 进程监控的 AppID 提取准确性优化
   - [x] VDF 解析的健壮性增强
   - [x] 时长数据自动持久化
   - [x] 游戏图标从Steam缓存读取

2. **提醒系统完善**
   - [x] 渐强提醒逻辑实现
   - [x] 使用系统默认声音
   - [ ] 提醒间隔配置UI

3. **界面完善**
   - [x] 折线图展示近7天时长（使用matplotlib）
   - [x] 游戏图标显示支持
   - [x] 开机自启动设置
   - [x] 数据手动导出功能（JSON/CSV）

4. **打包发布**
   - [x] PyInstaller 打包配置
   - [x] ZIP压缩包分发
   - [ ] 自动更新机制

5. **安全与可靠性**
   - [x] 错误处理增强
   - [x] 日志系统
   - [x] 数据导出备份

### 架构决策记录

#### ADR-001: 数据获取方式选择

**背景**: 需要获取 Steam 游戏时长数据

**选项**:
1. Steam Web API - 需要 API Key，有请求限制
2. 本地缓存读取 - 离线可用，无限制
3. 进程监控 - 实时精准，但无历史数据

**决策**: 采用本地缓存 + 进程监控结合
- 缓存读取：获取游戏列表和历史累计时长
- 进程监控：实时追踪当日游戏运行状态

**理由**:
- 无需网络连接和 API Key
- 无请求限制
- 实时性更好
- 用户隐私友好

#### ADR-002: 技术栈选择

**背景**: 需要选择开发语言和 GUI 框架

**决策**: Python + pystray + tkinter

**理由**:
- 用户熟悉 Python
- pystray 轻量，打包后约 15MB
- tkinter 是 Python 标准库，无需额外安装
- 开发效率高

#### ADR-003: 数据存储方式

**背景**: 需要持久化游戏时长和配置数据

**决策**: JSON 文件存储

**理由**:
- 轻量简单，无需数据库依赖
- 易于调试和手动修改
- 跨平台兼容性好
- 数据量小，性能足够

### 已知问题

1. **AppID 提取不准确**: 部分游戏进程名和命令行参数无法准确提取 AppID
2. **VDF 解析不完整**: 当前使用正则解析，可能遗漏某些格式
3. **通知依赖**: win10toast 在某些 Windows 版本可能不稳定

### 下一步计划

1. 完善核心功能，确保基本可用
2. 添加测试覆盖
3. 优化用户体验
4. 准备首次发布