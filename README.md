# Bluetooth Popup

一个轻量的 Windows 11 后台托盘程序：检测蓝牙音频设备连接，在屏幕顶部显示圆球落下、横向展开、文字渐入和淡出动画。

## 运行

```powershell
& 'C:\Program Files\dotnet\sdk\10.0.302\MSBuild.exe' .\AirPodsPopup.slnx /t:Build /p:Configuration=Debug /p:Platform=x64
```

构建后运行：

```powershell
.\bin\x64\Debug\net8.0-windows10.0.17763.0\BluetoothPopup.exe
```

程序没有主窗口。启动后会常驻系统托盘，右键托盘图标即可使用“测试弹窗”或“退出”。双击托盘图标也会触发测试弹窗。

## 结构

```text
App.xaml / App.xaml.cs
├─ Bluetooth/BluetoothAudioMonitor.cs
├─ Popup/BluetoothPopupWindow.xaml(.cs)
├─ Popup/PopupService.cs
└─ Tray/TrayService.cs
```

蓝牙监听通过 Windows Core Audio 的 MMDevice API 枚举活动的音频输出端点，并以轻量轮询识别新连接；动画窗口使用 `WS_EX_NOACTIVATE` 和 `WS_EX_TOOLWINDOW`，不会抢当前应用焦点。
