# SteamWatch 系统架构文档

## 1. 系统概述

SteamWatch 是一个 Windows 平台的 Steam 游戏时长监控与限制工具，采用 Python 开发，以系统托盘形式运行。

### 1.1 核心功能

- 实时监控 Steam 游戏进程
- 追踪每个游戏的游玩时长
- 设置单游戏和全局时长限制
- 渐强提醒系统（通知 → 声音 → 重复提醒）
- 近7天数据统计与图表展示

### 1.2 技术栈

- **语言**: Python 3.8+
- **GUI**: tkinter + pystray
- **进程监控**: psutil
- **通知系统**: win10toast / playsound
- **数据存储**: JSON 文件

## 2. 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                      SteamWatch                              │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   UI Layer  │  │  Tray App   │  │   Main Window       │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                    │              │
├─────────┼────────────────┼────────────────────┼──────────────┤
│         └────────────────┼────────────────────┘              │
│                          ▼                                   │
│  ┌───────────────────────────────────────────────────────┐   │
│  │                    Core Layer                          │   │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐  │   │
│  │  │SteamMonitor │ │CacheReader  │ │  TimeTracker    │  │   │
│  │  │(进程监控)   │ │(缓存读取)   │ │  (时长追踪)     │  │   │
│  │  └─────────────┘ └─────────────┘ └─────────────────┘  │   │
│  └───────────────────────────────────────────────────────┘   │
│                          │                                   │
├──────────────────────────┼───────────────────────────────────┤
│                          ▼                                   │
│  ┌───────────────────────────────────────────────────────┐   │
│  │                   Data Layer                           │   │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐  │   │
│  │  │  Models     │ │   Storage   │ │    Config       │  │   │
│  │  └─────────────┘ └─────────────┘ └─────────────────┘  │   │
│  └───────────────────────────────────────────────────────┘   │
│                          │                                   │
├──────────────────────────┼───────────────────────────────────┤
│                          ▼                                   │
│  ┌───────────────────────────────────────────────────────┐   │
│  │                  Utils Layer                           │   │
│  │  ┌─────────────┐ ┌─────────────────────────────────┐  │   │
│  │  │  Notifier   │ │      Helper Functions           │  │   │
│  │  └─────────────┘ └─────────────────────────────────┘  │   │
│  └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## 3. 模块说明

### 3.1 Core Layer（核心层）

#### 3.1.1 SteamMonitor（进程监控）

**职责**: 实时监控 Steam 客户端和游戏进程

**主要功能**:
- 轮询检测 Steam 进程是否运行
- 识别正在运行的游戏进程（通过进程名和命令行参数提取 AppID）
- 提供事件回调机制（game_start, game_stop, steam_start, steam_stop）

**依赖**: psutil

**文件**: `src/steamwatch/core/steam_monitor.py`

#### 3.1.2 CacheReader（缓存读取）

**职责**: 读取 Steam 本地缓存获取游戏列表和历史时长

**主要功能**:
- 查找 Steam 安装路径
- 获取当前登录用户 ID
- 解析 VDF 格式的本地配置文件
- 提供游戏信息查询接口

**数据源**: Steam 本地缓存文件（localconfig.vdf, loginusers.vdf）

**文件**: `src/steamwatch/core/cache_reader.py`

#### 3.1.3 TimeTracker（时长追踪）

**职责**: 追踪和管理游戏时长数据

**主要功能**:
- 记录每日游戏时长
- 设置游戏时长限制（单游戏 + 全局）
- 检查是否超限或接近限制
- 提供近 N 天统计数据

**数据存储**: JSON 文件（data/timedata.json, data/config.json）

**文件**: `src/steamwatch/core/time_tracker.py`

### 3.2 UI Layer（界面层）

#### 3.2.1 TrayApp（系统托盘）

**职责**: 管理系统托盘图标和菜单

**主要功能**:
- 创建托盘图标
- 提供右键菜单（打开窗口、设置、关于、退出）
- 响应游戏事件（启动/停止通知）

**依赖**: pystray, PIL

**文件**: `src/steamwatch/ui/tray.py`

#### 3.2.2 MainWindow（主窗口）

**职责**: 提供图形化配置和统计界面

**主要功能**:
- 游戏列表展示（名称、时长、限制、状态）
- 时长统计图表
- 设置面板（全局限制、提醒配置）

**依赖**: tkinter

**文件**: `src/steamwatch/ui/main_window.py`

### 3.3 Models Layer（模型层）

#### 3.3.1 Game（游戏模型）

**属性**: app_id, name, playtime_forever, daily_limit, today_playtime, status

**文件**: `src/steamwatch/models/game.py`

#### 3.3.2 AppConfig（应用配置）

**属性**: steam_path, poll_interval, global_daily_limit, notification_config

**文件**: `src/steamwatch/models/config.py`

### 3.4 Utils Layer（工具层）

#### 3.4.1 Notifier（通知器）

**职责**: 发送系统通知和播放提醒声音

**主要功能**:
- Windows 系统通知
- 渐强提醒机制
- 声音播放

**文件**: `src/steamwatch/utils/notification.py`

#### 3.4.2 Storage（存储工具）

**职责**: 提供简单的 JSON 文件存储功能

**文件**: `src/steamwatch/utils/storage.py`

## 4. 数据流

```
Steam 进程运行
      │
      ▼
SteamMonitor 检测到游戏启动
      │
      ├──▶ 提取 AppID
      │
      ▼
TimeTracker 记录游戏时长
      │
      ├──▶ 检查是否接近/超限
      │         │
      │         ▼
      │    Notifier 发送提醒
      │
      ▼
MainWindow 更新界面显示
```

## 5. 文件结构

```
SteamWatch/
├── src/steamwatch/
│   ├── main.py              # 主入口
│   ├── __init__.py
│   ├── core/                # 核心模块
│   │   ├── steam_monitor.py # 进程监控
│   │   ├── cache_reader.py  # 缓存读取
│   │   └── time_tracker.py  # 时长追踪
│   ├── ui/                  # 界面模块
│   │   ├── tray.py          # 系统托盘
│   │   └── main_window.py   # 主窗口
│   ├── models/              # 数据模型
│   │   ├── game.py
│   │   └── config.py
│   ├── utils/               # 工具模块
│   │   ├── notification.py  # 通知
│   │   └── storage.py       # 存储
│   └── config/              # 配置管理
│       └── settings.py
├── tests/                   # 测试文件
├── docs/                    # 文档
├── assets/                  # 资源文件
└── data/                    # 运行时数据
```

## 6. 配置说明

### 6.1 用户配置（data/config.json）

```json
{
  "global_limit": 300,
  "limits": [
    {"app_id": 730, "daily_limit": 120, "name": "Counter-Strike 2"}
  ]
}
```

### 6.2 时长数据（data/timedata.json）

```json
{
  "records": {
    "2024-01-15": {
      "game_playtimes": {"730": 90, "570": 30}
    }
  }
}
```

## 7. 扩展点

### 7.1 添加新的数据源

实现新的 Reader 类，继承基础接口，注入到 TimeTracker。

### 7.2 添加新的通知方式

扩展 Notifier 类，添加新的通知渠道（如邮件、Webhook）。

### 7.3 添加新的限制策略

在 TimeTracker 中实现新的限制检查逻辑。