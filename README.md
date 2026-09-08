# CxShell

[English](README.md) | [简体中文](README.zh-CN.md)

CxShell is a Windows-first desktop terminal and file-management client built with .NET 10, Avalonia, and AtomUI. The current product scope is intentionally focused on local shells, SSH-based workflows, file transfer, local performance, and SSH tunnels.

[Download the latest Windows release](https://github.com/luzwales/CxShell/releases/latest) · [Report an issue](https://github.com/luzwales/CxShell/issues)

## Core features

- **SSH terminal**: password/private-key authentication, SSH agent support, keep-alive, automatic reconnect, login scripts, X11 forwarding, and terminal scrollback/selection/copy/paste.
- **SFTP and FTP**: browse directories, upload, download, rename, delete, create folders, and use a shared file-browser workflow.
- **Local terminal**: detects available local shells. On Windows this includes PowerShell, Command Prompt, and WSL when `wsl.exe` is available.
- **Local folders**: open and manage local files and folders from the application workspace.
- **Local performance**: inspect local CPU, memory, disk, and network information.
- **SSH tunnels**: local, remote, and dynamic forwarding, with optional proxy/jump-host configuration.
- **Terminal file transfer**: pure C# ZMODEM, XMODEM, and YMODEM support where the remote shell provides the corresponding command.
- **Workspace customization**: tabs, layouts, themes, terminal appearance, Chinese/English localization, and a maximized startup window.

The connection editor exposes SSH, SFTP, and FTP as its remote protocols. Local shells are started from the local-terminal menu rather than saved as remote sessions.

## Supported protocols

| Protocol | Purpose |
| --- | --- |
| SSH | Remote terminal, monitoring, X11 forwarding, SSH agent, and tunnels |
| SFTP | File browsing and transfer over the SSH.NET SFTP subsystem |
| FTP | File browsing and transfer through FluentFTP |
| Local | PowerShell, Command Prompt, WSL, and detected Unix shells |

## Screenshots

![CxShell terminal and file workspace](docs/images/cxshell-ssh-sftp-monitor.png)

## Technology

- .NET 10 and C#
- Avalonia 12 and AtomUI Desktop Controls 6
- CommunityToolkit.Mvvm and ReactiveUI.Avalonia
- SSH.NET and SshNet.Agent
- FluentFTP
- AvaloniaEdit and TextMate grammars
- Velopack for Windows installation and updates

## Build from source

Requirements:

- .NET 10 SDK
- Git
- Windows 10/11 for the complete local-shell experience; Avalonia can also run on other desktop platforms

```powershell
dotnet restore
dotnet build CxShell.csproj
dotnet run --project CxShell.csproj
dotnet test CxShell.Tests\CxShell.Tests.csproj
```

## Windows packaging

The GitHub Actions workflow builds Windows x64 packages automatically after changes reach `master`. It produces:

- **CxShell-Setup.exe**: Velopack installer with Start Menu integration, uninstaller, and update metadata.
- **CxShell-Portable.exe**: self-contained single-file executable that does not require installation.
- Velopack `RELEASES` and package files used by the installed application's updater.

To create the same artifacts locally:

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

## Online updates

Installed Windows builds use Velopack to check the latest GitHub Release. Automatic checks are enabled by default and can be disabled in application settings. A user can also start **Check for updates** manually. When an update is downloaded, CxShell restarts through Velopack and applies it safely.

Online updates require the installed `CxShell-Setup.exe` edition. The portable executable is intentionally standalone and must be replaced manually when a new release is published.

The update feed is the GitHub Release download endpoint:

```text
https://github.com/luzwales/CxShell/releases/latest/download
```

Do not remove the Velopack `RELEASES`, `.nupkg`, and full-release assets from the latest release; the updater needs them in addition to the installer and portable executable.

## GitHub Actions

- `.github/workflows/ci.yml` runs the .NET test suite on pushes and pull requests.
- `.github/workflows/release.yml` tests the project, publishes a self-contained Windows x64 build, creates Velopack artifacts, and publishes a GitHub Release automatically on `master` pushes. It can also be run manually.

The workflow uses the GitHub-provided `GITHUB_TOKEN`; no private signing key or application secret is required for the default package flow. For production distribution, code-signing can be added as a separate protected environment step.

## Project structure

```text
Views/       Avalonia windows, pages, dialogs, and composed views
ViewModels/  MVVM state, commands, and interaction logic
Models/      Session, tunnel, monitoring, and file models
Services/    SSH, SFTP, FTP, local terminal, monitoring, persistence, and updates
Terminal/    Terminal buffer, ANSI parser, cells, and color handling
Controls/    Custom terminal and reusable controls
Assets/      Icons and embedded UI resources
```

## License

Apache-2.0. See [LICENSE](LICENSE).