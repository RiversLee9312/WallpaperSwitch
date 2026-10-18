# WallpaperSwitch

[English](README.md) · [下载最新版](https://github.com/RiversLee9312/WallpaperSwitch/releases/latest) · [MIT 许可证](LICENSE)

根据黎明、白天、黄昏和夜晚自动切换 Windows 壁纸，以及应用和系统的浅色/深色外观。
支持自定义时刻，也可根据位置计算日出日落。

## 安装和使用

1. 在 [GitHub Releases](https://github.com/RiversLee9312/WallpaperSwitch/releases/latest)
   下载 `WallpaperSwitch-Setup-<版本>-win-x64.exe`。
2. 运行安装程序。安装包包含所需的 .NET 和 Windows App SDK 运行时，适用于 Windows 10 1809 及以上的 x64 系统。
3. 为四个时段选择壁纸、外观和时间，按需开启锁屏同步与位置刷新。
4. 如需更早自启，在应用中手动开启“高优先级开机自启”。注册服务时需要管理员授权；桌面应用仍以当前用户身份运行。

升级会保留用户配置。每个正式版本同时提供 `SHA256SUMS.txt`，可用 PowerShell 的
`Get-FileHash -Algorithm SHA256 <安装包路径>` 核对下载文件。

## 功能

- 四时段壁纸和浅色/深色外观，自定义时间或日出日落计划。
- 可选锁屏同步、位置刷新、普通开机自启及服务辅助的高优先级自启。
- 托盘控制、托盘注册失败重试和任务栏重建后恢复。
- 单例运行；再次手动启动会显示已有窗口。
- 设置保存、图片读取、系统外观修改等耗时操作异步执行，等待时显示加载动画。
- 24 种界面语言，支持跟随系统、保存语言选择及阿拉伯语/希伯来语从右到左布局。
- “关于”中的版本、源码链接和离线许可文档。长文档分页显示。

## 许可证和隐私

项目原创代码、文档和项目制作的美术资源使用 [MIT 许可证](LICENSE)。使用和分发时保留版权及许可声明即可。
依赖组件仍遵循各自的原始条款；Windows App SDK 二进制组件适用微软的软件许可条款。

- [许可证说明](docs/LICENSES.md)
- [第三方声明与版本清单](THIRD_PARTY_NOTICES.md)
- [安装分发条款](DISTRIBUTION_TERMS.txt)
- [隐私和本地数据说明](docs/PRIVACY.md)
- [更新记录](CHANGELOG.md)

设置保存在 `%LOCALAPPDATA%\WallpaperSwitch\settings.json`。启用位置功能后，配置和日志可能含坐标；
诊断日志也可能包含壁纸路径，提交反馈前请先检查。应用没有账号、广告或自建分析后台。

## 开发

技术栈为 C#/.NET 8、WinUI 3 和 Windows App SDK 2.5.1；国际化使用微软 MRT Core 与 `.resw`。
准备 .NET SDK、Windows SDK 和 Inno Setup 6.7.3 或更新版本后，在 PowerShell 中执行：

```powershell
.\tools\Build-Release.ps1
```

脚本还原依赖、生成第三方声明、发布应用和服务助手、运行 MSTest 单元测试，并生成安装包。
完整构建与 UI 测试命令见 [英文 README](README.md#tests)。
欢迎通过 [Issues](https://github.com/RiversLee9312/WallpaperSwitch/issues) 反馈问题或提交 PR。
