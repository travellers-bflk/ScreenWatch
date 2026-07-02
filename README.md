<p align="center">
  <img src="docs/assets/logo.png" alt="ScreenWatch Logo" width="128" height="128" />
</p>

<h1 align="center">ScreenWatch</h1>

<p align="center">
  <strong>隐私优先的 Windows 屏幕使用时间统计工具</strong><br/>
  <em>了解你的时间都花在了哪里，所有数据完全保留在本地。</em>
</p>

<p align="center">
  <a href="https://github.com/travellers-bflk/ScreenWatch/actions/workflows/ci.yml">
    <img src="https://github.com/travellers-bflk/ScreenWatch/actions/workflows/ci.yml/badge.svg" alt="CI" />
  </a>
  <a href="https://github.com/travellers-bflk/ScreenWatch/actions/workflows/codeql.yml">
    <img src="https://github.com/travellers-bflk/ScreenWatch/actions/workflows/codeql.yml/badge.svg" alt="CodeQL" />
  </a>
  <a href="./LICENSE">
    <img src="https://img.shields.io/github/license/travellers-bflk/ScreenWatch?color=blue" alt="License" />
  </a>
  <a href="https://github.com/travellers-bflk/ScreenWatch/releases">
    <img src="https://img.shields.io/github/v/release/travellers-bflk/ScreenWatch?include_prereleases" alt="Release" />
  </a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4" alt=".NET 9" />
  <img src="https://img.shields.io/badge/platform-Windows-0078D6" alt="Windows" />
</p>

---

## 为什么选择 ScreenWatch？

macOS 有原生的「屏幕使用时间」，但 Windows 用户一直没有一个好用的本地化替代方案。ScreenWatch 正是为此而生——它是一款**完全离线、零数据上传**的 Windows 屏幕使用时间统计工具，参考了 macOS 的设计风格，同时注重保护用户隐私。

## 隐私承诺

> **本软件不收集、不上传任何用户数据。**
>
> - 不发送任何数据到任何服务器
> - 不包含任何遥测或分析代码
> - 不包含任何第三方数据收集 SDK
> - 所有统计数据完全保存在你的本地设备上
> - 你可以随时删除所有数据

## 功能特性

| 功能 | 描述 |
|------|------|
| **双维度统计** | 同时追踪前台应用使用时间和屏幕活跃时间 |
| **灵活周期** | 支持按日、周、月、自定义区间查看统计数据 |
| **时段分析** | 将一天划分为多个时段，分析不同时段的使用习惯 |
| **智能分类** | 自动将应用归类（工作、社交、娱乐、开发等） |
| **自定义分类** | 支持自定义分类规则，按需管理 |
| **排除白名单** | 可设置白名单应用，排除不需要统计的程序 |
| **开机自启** | 支持开机自动启动并静默驻留系统托盘 |

## 数据存储

所有数据存储在本地 SQLite 数据库中：

- **存储位置**：`%APPDATA%\ScreenWatch\data\usage.db`
- **数据库格式**：SQLite 3
- **数据可控**：你可以随时通过应用内功能或手动删除文件来清除所有数据
- **无备份**：本软件不会将数据备份到任何云端或网络位置

## 快速开始

### 前置要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 1809+ 或 Windows 11

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/travellers-bflk/ScreenWatch.git
cd ScreenWatch

# 还原依赖
dotnet restore

# 编译
dotnet build

# 运行
dotnet run --project src/ScreenWatch.App

# 发布（生成独立可执行文件）
dotnet publish src/ScreenWatch.App -c Release -r win-x64 --self-contained
```

## 技术栈

- **语言**：C# 12
- **框架**：.NET 9
- **UI 框架**：WPF (Windows Presentation Foundation)
- **MVVM**：CommunityToolkit.Mvvm
- **数据库**：Microsoft.Data.Sqlite (SQLite)
- **系统托盘**：H.NotifyIcon.Wpf
- **测试**：xUnit

## 项目结构

```
ScreenWatch/
├── src/
│   ├── ScreenWatch.Core/     # 核心库：数据层、服务、Win32 封装、模型
│   └── ScreenWatch.App/      # WPF 应用：UI、托盘、启动入口
├── tests/
│   └── ScreenWatch.Tests/    # 单元测试
└── .github/                  # CI/CD 配置与 issue 模板
```

## 贡献

欢迎参与贡献！请阅读 [贡献指南](CONTRIBUTING.md) 了解详情。

## 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

## 致谢

- 灵感来源于 macOS 的「屏幕使用时间」功能
- 感谢所有开源社区的贡献者

## 免责声明

本软件按"现状"提供，不提供任何明示或暗示的担保。使用本软件产生的一切后果由使用者自行承担。本软件不收集任何用户数据，但因本软件运行于用户本地设备，用户应自行负责保护其本地数据的安全。
