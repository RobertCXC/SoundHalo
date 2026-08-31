# AirPods Popup · 声环

一个轻量、常驻系统托盘的 Windows 蓝牙音频连接提示工具。

当 AirPods 或其他蓝牙音频设备连接成功后，屏幕顶部会先出现一个耳机圆标，再展开为设备信息卡片，显示设备名称和连接状态。提示结束后自动淡出，不打开主窗口，也不会抢走当前应用的焦点。

## 效果预览

<p align="center">
  <img src="./picture/1.png" alt="AirPods Popup 效果图" width="600">
</p>

## 功能特点

- 顶部居中显示蓝牙音频设备连接提示。
- 圆形提示平滑展开为蓝灰色玻璃质感的信息卡片。
- 显示设备名称和“已连接”状态。
- 使用透明宿主窗口，不激活窗口、不抢键盘焦点。
- 常驻系统托盘，不创建传统主窗口。
- 支持从托盘菜单或双击托盘图标测试弹窗动画。
- 自动取消上一次提示，避免多个弹窗重叠。
- 兼容常见的蓝牙耳机和蓝牙音频输出设备。

## 动画流程

```text
圆形入场 → 短暂停留 → 展开信息卡片 → 显示设备信息 → 保持显示 → 淡出离场
  250 ms      120 ms           350 ms              2.2 s          450 ms
```

弹窗尺寸从 `48 × 48` 的圆形提示展开为 `350 × 80` 的信息卡片，圆角、透明度、位置和内容均使用 WPF 动画完成。

## 工作方式

程序启动时会建立一次 Windows 当前处于活动状态的音频输出端点快照，并注册 Windows Core Audio 的设备端点通知回调。端点新增、移除、状态或属性发生变化时，程序会在短暂防抖后重新枚举设备，通过设备标识、枚举器信息和设备名称判断其是否为蓝牙音频设备。首次启动只建立设备快照；之后只对新出现的设备显示提示，避免程序启动时重复弹窗。

目前会重点识别包含以下常见标识的设备：

`AirPods`、`Beats`、`Bose`、`FreeBuds`、`Galaxy Buds`、`Jabra`、`LinkBuds`、`Soundcore`、`WH-`、`WF-`、`蓝牙`、`Bluetooth`

## 技术栈

- C# / .NET 8
- WPF：弹窗布局、渐变视觉和动画
- Windows Forms `NotifyIcon`：系统托盘入口
- Windows Core Audio / MMDevice COM API：监听并枚举活动音频输出设备
- x64，支持 Per-Monitor DPI
- Win32 `WS_EX_NOACTIVATE`、`WS_EX_TOOLWINDOW`：实现不抢焦点的后台提示

## 运行要求

- Windows 10 版本 1809（`10.0.17763`）或更高版本
- x64 系统
- .NET 8 Windows Desktop Runtime

## 构建与运行

本项目固定使用 .NET SDK `10.0.302` 的 MSBuild。请先确认对应文件存在，再执行构建。

### 准备构建环境

```powershell
$msbuildPath = 'C:\Program Files\dotnet\sdk\10.0.302\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuildPath)) {
    throw 'Required .NET SDK 10.0.302 MSBuild.exe was not found.'
}

$restoreConfig = Join-Path (Get-Location) 'NuGet.Config'
```

### 构建 Debug

```powershell
& $msbuildPath .\AirPodsPopup.slnx `
    /t:Build `
    /p:Configuration=Debug `
    /p:Platform=x64 `
    /p:RestoreConfigFile=$restoreConfig `
    /v:minimal
```

### 运行 Debug

```powershell
& '.\bin\x64\Debug\net8.0-windows10.0.17763.0\BluetoothPopup.exe'
```

### 构建 Release

```powershell
& $msbuildPath .\AirPodsPopup.slnx `
    /t:Build `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:RestoreConfigFile=$restoreConfig `
    /v:minimal
```

## 使用方式

1. 启动程序后，应用会隐藏在系统托盘中。
2. 双击托盘图标，或在右键菜单中选择“测试弹窗”，预览提示动画。
3. 连接新的蓝牙音频设备，程序会自动显示设备名称和连接状态。
4. 在托盘右键菜单中选择“退出”关闭程序。

## 项目结构

```text
AirPodsPopup/
├─ App.xaml / App.xaml.cs                 应用生命周期和服务编排
├─ Bluetooth/
│  └─ BluetoothAudioMonitor.cs             蓝牙音频设备事件监听与识别
├─ Popup/
│  ├─ BluetoothPopupWindow.xaml            弹窗布局和视觉效果
│  ├─ BluetoothPopupWindow.xaml.cs         弹窗状态与动画时间线
│  ├─ CornerRadiusAnimation.cs             圆角补间动画
│  └─ PopupService.cs                      弹窗创建、取消和生命周期管理
├─ Tray/
│  └─ TrayService.cs                       系统托盘图标和右键菜单
├─ Native/
│  └─ NativeMethods.cs                     Win32 窗口样式封装
├─ picture/
│  └─ 1.png                                效果预览图
├─ AirPodsPopup.csproj
└─ AirPodsPopup.slnx
```

## 当前限制

- 只监控 Windows 音频输出端点；普通蓝牙键盘、鼠标等设备不会触发提示。
- 设备检测采用 Windows 音频端点事件通知；收到通知后会进行一次短暂防抖刷新，不使用固定周期轮询。
- 当前使用通用耳机图标，不读取 AirPods 电量、左右耳状态等额外信息。
- 设备是否被识别为蓝牙音频设备，取决于 Windows 暴露的设备标识或名称。

## 设计目标

```text
轻   不引入大型 UI 框架
静   不抢焦点，不打断当前操作
准   只提示新连接的蓝牙音频设备
美   小尺寸也保留完整的动效和层次
```
