using System.Windows;
using BluetoothPopup.Bluetooth;
using BluetoothPopup.Popup;
using BluetoothPopup.Tray;
using WpfApplication = System.Windows.Application;

namespace BluetoothPopup;

public partial class App : WpfApplication
{
    private BluetoothAudioMonitor? _bluetoothMonitor;
    private PopupService? _popupService;
    private TrayService? _trayService;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _popupService = new PopupService(Dispatcher);

        _trayService = new TrayService();
        _trayService.TestPopupRequested += OnTestPopupRequested;
        _trayService.ExitRequested += OnExitRequested;

        _bluetoothMonitor = new BluetoothAudioMonitor();
        _bluetoothMonitor.DeviceConnected += OnBluetoothDeviceConnected;
        _bluetoothMonitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeServices();
        base.OnExit(e);
    }

    private void OnTestPopupRequested(object? sender, EventArgs e)
    {
        _popupService?.ShowTestPopup();
    }

    private void OnBluetoothDeviceConnected(object? sender, BluetoothAudioDeviceConnectedEventArgs e)
    {
        _popupService?.ShowPopup(e.DeviceName);
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Shutdown();
    }

    private void DisposeServices()
    {
        _bluetoothMonitor?.Dispose();
        _bluetoothMonitor = null;

        _trayService?.Dispose();
        _trayService = null;

        _popupService?.Dispose();
        _popupService = null;
    }
}
