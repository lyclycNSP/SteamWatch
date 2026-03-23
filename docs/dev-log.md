# SteamWatch 开发记录

## v0.1.0 - 项目初始化

### 2024-03-23 开发进度

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

8. **Windows兼容性修复**
   - 修复Steam路径检测（从注册表读取）
   - 修复Steam ID 64位转32位
   - 从appmanifest读取游戏列表
   - 修复进程名匹配逻辑（支持空格）

9. **UI功能完善**
   - 游戏列表工具栏（刷新、设置限额、取消限额）
   - 双击/右键设置游戏限额
   - 渐强提醒说明
   - Windows勿扰模式提示
   - 自定义图标支持

10. **打包发布**
    - PyInstaller打包配置
    - 生成SteamWatch.exe（约38MB）

#### 待完成

1. **提醒系统完善**
   - [ ] 提醒间隔配置UI

2. **打包发布**
   - [ ] 自动更新机制

3. **安全与可靠性**
   - [ ] 更多错误处理

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
- pystray 轻量，打包后约 38MB
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

1. **任务栏图标**: 通过Python解释器运行时任务栏图标可能显示不正确，打包后正常
2. **部分游戏检测**: 某些特殊启动方式的网游可能无法正确检测

### 下一步计划

1. 发布v0.1.0版本
2. 收集用户反馈
3. 优化游戏检测准确性
4. 添加更多提醒方式