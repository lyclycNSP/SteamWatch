# 贡献指南

感谢你对 SteamWatch 项目的兴趣！本文档将帮助你了解如何为项目做出贡献。

## 行为准则

请保持友善、尊重和包容。我们欢迎所有背景和经验水平的贡献者。

## 如何贡献

### 报告问题

如果你发现了问题，请：

1. 在 [Issues](../../issues) 页面搜索是否已有相关问题
2. 如果没有，创建新问题并包含：
   - 清晰的问题标题
   - 问题的详细描述
   - 复现步骤
   - 预期行为和实际行为
   - 系统环境信息（Windows版本、Python版本等）
   - 相关日志或截图

### 提交代码

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 进行更改
4. 运行测试确保通过 (`pytest`)
5. 提交更改 (`git commit -m 'Add amazing feature'`)
6. 推送到分支 (`git push origin feature/amazing-feature`)
7. 创建 Pull Request

### 开发环境设置

```bash
# 克隆你的 fork
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

# 代码格式化
black src tests
isort src tests

# 代码检查
flake8 src tests
mypy src
```

### 代码风格

- 使用 Black 进行代码格式化
- 使用 isort 进行 import 排序
- 遵循 PEP 8 规范
- 添加类型注解
- 编写文档字符串

### 提交信息规范

- 使用清晰的提交信息
- 第一行不超过72个字符
- 可以使用以下前缀：
  - `feat:` 新功能
  - `fix:` 修复问题
  - `docs:` 文档更新
  - `style:` 代码格式调整
  - `refactor:` 代码重构
  - `test:` 测试相关
  - `chore:` 构建或工具相关

## 项目结构

```
SteamWatch/
├── src/steamwatch/       # 主代码
│   ├── core/            # 核心功能
│   ├── ui/              # 用户界面
│   ├── models/          # 数据模型
│   ├── utils/           # 工具函数
│   └── config/          # 配置管理
├── tests/               # 测试
├── docs/                # 文档
└── assets/              # 资源文件
```

## 问题？

如有任何问题，欢迎在 Issues 中提问或参与讨论。

感谢你的贡献！