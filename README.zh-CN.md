# per-monitor-virtual-desktop

这是一个 Windows 按显示器控制虚拟桌面的工具。应用名为 **PerMonitorVD**，CLI 名为 **pvdctl**。

[English](README.md) | [한국어](README.ko.md)

PerMonitorVD 是一个 Windows 原型，用于实现**按显示器划分的虚拟桌面体验**。它不会让 Windows Shell 同时激活多个原生虚拟桌面，而是保持一个 `[PMVD] ACTIVE` 组合桌面，并把非活动显示器工作区的窗口停放到真实的 Windows 虚拟桌面中。

## 功能

- 按显示器切换 workspace。
- 使用 `Ctrl+Alt+Shift+Left/Right` 只切换鼠标所在显示器。
- 使用 `Ctrl+Alt+Shift+Up/Down` 将当前聚焦窗口移动到上一个/下一个 workspace。
- 使用 `Ctrl+Alt+Shift+Home` 打开 PMVD Home。
- 在 Home 覆盖窗口中只查看每个显示器的 PMVD 桌面和正在使用的应用。
- 将应用 chip 直接拖放到目标桌面卡片，即可移动该应用。
- 在托盘菜单中查看当前不可见的其他 PMVD 桌面应用，并直接跳转到该应用所在的桌面。
- 类似 Windows Task View 的按显示器桌面卡片界面。
- 在 Home 窗口中设置每个显示器的最大托管窗口数。`0` 表示不限制。
- 使用 `pvdctl diagnostics` 导出诊断报告。
- 尽量设置 Windows 任务栏，使其显示所有原生虚拟桌面的应用窗口。
- 不显示普通任务栏应用按钮，而是在系统托盘中常驻运行，并可随 Windows 登录自动启动。

## 构建

普通使用时，可以从 GitHub Releases 下载 `per-monitor-virtual-desktop-*-win-x64.zip`，解压到准备长期保存的文件夹后运行：

```powershell
.\PerMonitorVD.exe
.\pvdctl.exe status
```

Release zip 是 Windows x64 self-contained 包，不需要另外安装 .NET 运行时。

```powershell
dotnet restore .\PerMonitorVD.sln
dotnet build .\PerMonitorVD.sln -c Release
```

运行 tray app:

```powershell
.\src\PerMonitorVD\bin\x64\Release\net8.0-windows10.0.19041.0\PerMonitorVD.exe
```

CLI:

```powershell
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe status
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe diagnostics
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe home
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe activate-window --hwnd 0x00000000000A1234
```

## 默认快捷键

```text
Ctrl + Alt + Shift + Left    鼠标所在显示器：上一个 workspace
Ctrl + Alt + Shift + Right   鼠标所在显示器：下一个 workspace
Ctrl + Alt + Shift + 1/2/3   鼠标所在显示器：切换到 workspace 1/2/3
Ctrl + Alt + Shift + Up      将聚焦窗口移动到上一个 workspace
Ctrl + Alt + Shift + Down    将聚焦窗口移动到下一个 workspace
Ctrl + Alt + Shift + Home    打开 PMVD Home
Ctrl + Alt + Shift + R       修复状态
```

## Logitech 推荐映射

```text
Gesture left   -> Ctrl + Alt + Shift + Left
Gesture right  -> Ctrl + Alt + Shift + Right
Gesture up     -> Ctrl + Alt + Shift + Up
Gesture down   -> Ctrl + Alt + Shift + Down
```

首次测试时不要使用 Logitech 内置的 Virtual Desktop profile，因为它通常会发送 Windows 原生的全局虚拟桌面切换命令。

## 系统托盘和自动启动

PerMonitorVD 不显示普通任务栏应用按钮。启动后，请使用 Windows 系统托盘/通知区域中的图标，也就是 Wi-Fi、Bluetooth、音量图标附近的位置。如果 Windows 把图标隐藏到托盘溢出菜单中，可以在 `Settings -> Personalization -> Taskbar -> Other system tray icons` 中打开 PerMonitorVD。

`StartWithWindows` 默认启用。应用启动时会把当前 `PerMonitorVD.exe` 路径注册到当前用户的 `Run` 注册表项，使其在 Windows 登录时自动启动。如果之后移动了解压文件夹，请在新位置运行一次 `PerMonitorVD.exe` 来刷新自动启动路径。可以在托盘菜单的 `Start with Windows` 中切换，也可以编辑 `%LOCALAPPDATA%\PerMonitorVD\config.json`。

## 验证流程

1. 运行 `PerMonitorVD.exe`。
2. 在 Task View 中确认 `[PMVD] ACTIVE` 和 parking desktop 已创建。
3. 在显示器 1 打开记事本，在显示器 2 打开浏览器。
4. 将鼠标放在显示器 1，按 `Ctrl+Alt+Shift+Right`。
5. 确认只有显示器 1 的窗口切换，显示器 2 保持不变。
6. 按 `Ctrl+Alt+Shift+Left` 返回。
7. 运行 `pvdctl diagnostics` 并保存返回的报告路径。
8. 使用 `Ctrl+Alt+Shift+Home` 打开 Home 窗口，将应用 chip 拖放到另一个桌面卡片，验证移动操作。
9. 右键点击托盘图标 -> `Other desktop apps`，选择其他桌面的应用，确认会跳转到该应用所在桌面。
10. 在 Home 窗口中调整每个显示器的最大托管窗口数。达到上限后，PMVD 会停止自动跟踪该显示器上的新窗口，并跳过显式移动到已满显示器的操作。
11. 按推荐方式配置 Logitech gesture。

## 注意事项

- 正常使用时不要在 Task View 中直接使用 `[PMVD] ...` parking desktop。
- 如果任务栏没有显示所有桌面的应用，请在 Windows 设置中将 `Personalization -> Taskbar -> Taskbar behaviors -> On the taskbar, show all open windows` 设置为 `On all desktops`。
- 管理员权限窗口通常需要 PerMonitorVD 也以管理员权限运行。
- 如果窗口消失，请在 tray menu 中依次使用 `Return to active desktop`、`Rescue all windows`、`Repair state`。
