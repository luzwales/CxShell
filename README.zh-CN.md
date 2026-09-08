# CxShell

[English](README.md) | [简体中文](README.zh-CN.md)

CxShell 是一个基于 .NET 10、Avalonia 和 AtomUI 构建的 Windows 优先桌面终端与文件管理客户端。当前版本刻意聚焦于本地终端、SSH 工作流、文件传输、本地性能查看和 SSH 隧道。

[下载最新 Windows 版本](https://github.com/luzwales/CxShell/releases/latest) · [提交问题](https://github.com/luzwales/CxShell/issues)

## 核心功能

- **SSH 终端**：支持密码/私钥、SSH Agent、Keep Alive、自动重连、登录脚本、X11 转发，以及终端滚动历史、选择、复制和粘贴。
- **SFTP 和 FTP**：目录浏览、上传、下载、重命名、删除、新建目录，共享统一的文件浏览交互。
- **本地终端**：自动检测本机 Shell。Windows 下可检测 PowerShell、命令提示符，以及系统存在 `wsl.exe` 时的 WSL。
- **本地文件夹**：在应用工作区中打开和管理本地文件与文件夹。
- **本地性能**：查看本机 CPU、内存、磁盘和网络信息。
- **SSH 隧道**：支持本地转发、远程转发和动态转发，并可配合代理/JUMP HOST。
- **终端文件传输**：提供纯 C# 实现的 ZMODEM、XMODEM 和 YMODEM，前提是远端 Shell 提供对应命令。
- **工作区定制**：多标签、布局、主题、终端外观、中英文界面，以及启动时最大化窗口。

连接配置页面只保留 SSH、SFTP 和 FTP。Shell 从本地终端菜单启动，不作为远程会话保存。

## 支持的协议

| 协议 | 用途 |
| --- | --- |
| SSH | 远程终端、监控、X11 转发、SSH Agent 和隧道 |
| SFTP | 基于 SSH.NET SFTP subsystem 的文件浏览与传输 |
| FTP | 基于 FluentFTP 的文件浏览与传输 |
| Local | PowerShell、命令提示符、WSL 和检测到的 Unix Shell |

## 技术栈

- .NET 10 / C#
- Avalonia 12 / AtomUI Desktop Controls 6
- CommunityToolkit.Mvvm / ReactiveUI.Avalonia
- SSH.NET / SshNet.Agent
- FluentFTP
- AvaloniaEdit / TextMate grammars
- Velopack Windows 安装与更新

## 从源码构建

环境要求：

- .NET 10 SDK
- Git
- Windows 10/11（完整本地 Shell 体验）；Avalonia 也可以在其他桌面系统运行

```powershell
dotnet restore
dotnet build CxShell.csproj
dotnet run --project CxShell.csproj
dotnet test CxShell.Tests\CxShell.Tests.csproj
```

## Windows 自动打包

代码合并到 `master` 后，GitHub Actions 会自动构建 Windows x64 发布包，包含：

- **CxShell-Setup.exe**：Velopack 安装程序，包含开始菜单入口、卸载程序和更新元数据。
- **CxShell-Portable.exe**：自包含单文件便携版，无需安装 .NET 即可运行。
- **CxShell-Portable-FrameworkDependent.exe**：体积更小的单文件便携版，但要求目标机器安装 .NET 10 Desktop Runtime。
- 更新器所需的 Velopack `RELEASES` 和包文件。

也可以在本地生成同样的产物：

```powershell
$version = "0.1.0"
$publish = "artifacts\publish\win-x64"
$release = "artifacts\release"

dotnet publish CxShell.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o $publish `
  /p:Version=$version `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=none `
  /p:DebugSymbols=false

Copy-Item "$publish\CxShell.exe" "artifacts\CxShell-Portable.exe"
dotnet tool install --global vpk
vpk pack `
  --packId CxShell `
  --packVersion $version `
  --packDir $publish `
  --mainExe CxShell.exe `
  --outputDir $release `
  --packTitle CxShell
```

## 在线更新

通过 `CxShell-Setup.exe` 安装的 Windows 版本使用 Velopack 检查 GitHub 最新 Release。自动检查默认开启，也可以在应用设置中关闭；用户还可以手动执行“检查更新”。更新下载完成后，CxShell 会通过 Velopack 重启并安全应用更新。

在线更新只适用于安装版。自包含便携版是独立文件，发布新版本后需要手动替换；framework-dependent 便携版体积更小，但目标机器必须安装 .NET 10 Desktop Runtime。

更新源为：

```text
https://github.com/luzwales/CxShell/releases/latest/download
```

请不要从最新 Release 中删除 Velopack 的 `RELEASES`、`.nupkg` 和完整版本包；更新器除了安装程序和便携版之外还需要这些文件。

## GitHub Actions

- `.github/workflows/ci.yml`：在 push 和 pull request 时运行 .NET 测试。
- `.github/workflows/release.yml`：测试项目、发布自包含 Windows x64、生成 Velopack 安装包，并在 `master` push 时自动创建 GitHub Release，也支持手动运行。

默认流程只使用 GitHub 提供的 `GITHUB_TOKEN`，不需要提交私钥或应用密钥。正式分发时可以在受保护的环境中增加代码签名步骤。

## 项目结构

```text
Views/       Avalonia 窗口、页面、对话框和组合视图
ViewModels/  MVVM 状态、命令和交互逻辑
Models/      会话、隧道、监控和文件模型
Services/    SSH、SFTP、FTP、本地终端、监控、持久化和更新服务
Terminal/    终端缓冲区、ANSI 解析、单元格和颜色处理
Controls/    自定义终端及可复用控件
Assets/      图标和嵌入式 UI 资源
```

## 许可证

Apache-2.0，详见 [LICENSE](LICENSE)。