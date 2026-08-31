# 声环 · SoundHalo

> 给 Windows 的蓝牙音频连接，一点更有质感的回应。

声环是一款轻量、安静、常驻托盘的 Windows 蓝牙音频连接提示工具。

当耳机或其他蓝牙音频设备接入时，屏幕顶部会出现一个蓝灰色玻璃质感的正圆提示，随后自然展开为设备信息卡片；连接完成，提示淡出，不打断当前工作流。

它不抢焦点，不弹主窗口，也不试图变成一套复杂的设备管理中心——只在该出现的时候，给你一个漂亮而明确的反馈。

## 体验关键词

```text
        ●  →  ━━━━━━━━━━━━━━━━━
     正圆出现       信息展开       柔和退场
```

- 48×48 正圆入场，不拉伸成椭圆
- 蓝灰 / 青色半透明玻璃渐变，拒绝沉闷纯黑
- 圆形提示与信息卡片自然交接
- 固定透明宿主窗口，动画更稳定、更少掉帧
- 不激活窗口，不抢键盘焦点
- 常驻系统托盘，双击即可测试弹窗
- 支持常见蓝牙耳机与蓝牙音频输出设备

## 视觉方向

声环的视觉语言很简单：

**冷静的蓝灰色、轻薄的透明度、克制的高光，以及一个像声音回环一样的正圆。**

弹窗使用 WPF 原生半透明渐变和轻量阴影完成，圆外保持透明；动画由固定透明宿主承载，避免频繁调整系统窗口本身造成的卡顿。

## 技术栈

- C# + .NET 8
- WPF：弹窗视觉、布局与动画
- Windows Forms `NotifyIcon`：系统托盘入口
- Windows Core Audio / MMDevice API：枚举活动音频输出端点
- x64 / Per-Monitor DPI
- `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`：后台提示，不干扰当前应用

## 快速开始

### 构建 Debug

```powershell
$msbuildPath = 'C:\Program Files\dotnet\sdk\10.0.302\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuildPath)) {
    throw 'Required .NET SDK 10.0.302 MSBuild.exe was not found.'
}

& $msbuildPath .\AirPodsPopup.slnx `
    /t:Build `
    /p:Configuration=Debug `
    /p:Platform=x64 `
    /p:RestoreConfigFile=D:\Airpods\NuGet.Config `
    /v:minimal
```

### 运行

```powershell
& '.\bin\x64\Debug\net8.0-windows10.0.17763.0\BluetoothPopup.exe'
```

也可以构建 Release：

```powershell
& $msbuildPath .\AirPodsPopup.slnx `
    /t:Build `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:RestoreConfigFile=D:\Airpods\NuGet.Config `
    /v:minimal
```

## 怎么用

程序启动后不会打开主窗口，只会出现在系统托盘：

1. 双击托盘图标，立即预览弹窗动画。
2. 右键托盘图标，选择“测试弹窗”或“退出”。
3. 连接新的蓝牙音频设备，声环会自动显示设备名称和连接状态。

## 项目结构

```text
SoundHalo/
├─ App.xaml / App.xaml.cs                 应用生命周期与服务编排
├─ Bluetooth/
│  └─ BluetoothAudioMonitor.cs             蓝牙音频设备检测
├─ Popup/
│  ├─ BluetoothPopupWindow.xaml            玻璃弹窗视觉
│  ├─ BluetoothPopupWindow.xaml.cs         动画时间线与状态
│  ├─ CornerRadiusAnimation.cs             圆角补间动画
│  └─ PopupService.cs                      弹窗生命周期管理
├─ Tray/
│  └─ TrayService.cs                       系统托盘菜单
├─ Native/
│  └─ NativeMethods.cs                     非激活窗口样式
├─ AirPodsPopup.csproj
└─ AirPodsPopup.slnx
```

## 设计原则

```text
轻       不引入大型 UI 框架
静       不抢焦点，不打断操作
准       只在检测到新连接时提醒
美       小尺寸，也要有完整的动效和层次
```

## 状态

这是一个专注于 Windows 蓝牙音频连接反馈的小工具。功能保持克制，体验持续打磨中。

如果你也喜欢这种“少一点打扰，多一点质感”的系统小组件，欢迎一起把声环做得更漂亮。
