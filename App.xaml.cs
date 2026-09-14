using System.Windows;
using BluetoothPopup.Bluetooth;
using BluetoothPopup.ComPort;
using BluetoothPopup.Popup;
using BluetoothPopup.Settings;
using BluetoothPopup.Tray;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using System.IO;

namespace BluetoothPopup;

public partial class App : WpfApplication
{
    private BluetoothAudioMonitor? _bluetoothMonitor;
    private ComPortMonitor? _comPortMonitor;
    private PopupService? _popupService;
    private AppSettings? _settings;
    private AppSettingsStore? _settingsStore;
    private TrayService? _trayService;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsStore = new AppSettingsStore();
        _settings = _settingsStore.Load();
        _popupService = new PopupService(Dispatcher);

        _trayService = new TrayService(_settings);
        _trayService.TestPopupRequested += OnTestPopupRequested;
        _trayService.ExitRequested += OnExitRequested;
        _trayService.BluetoothMonitoringToggleRequested += OnBluetoothMonitoringToggleRequested;
        _trayService.ComMonitoringToggleRequested += OnComMonitoringToggleRequested;

        ApplyMonitoringState(_settings);
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

    private void OnComPortDeviceConnected(object? sender, ComPortDeviceConnectedEventArgs e)
    {
        _popupService?.ShowPopup($"串口 {e.PortName}");
    }

    private void OnBluetoothMonitoringToggleRequested(object? sender, MonitoringToggleRequestedEventArgs e)
    {
        UpdateMonitoringSettings(enableBluetoothMonitoring: e.IsEnabled, enableComMonitoring: _settings?.EnableComMonitoring ?? true);
    }

    private void OnComMonitoringToggleRequested(object? sender, MonitoringToggleRequestedEventArgs e)
    {
        UpdateMonitoringSettings(enableBluetoothMonitoring: _settings?.EnableBluetoothMonitoring ?? true, enableComMonitoring: e.IsEnabled);
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
        _settings = null;
        _settingsStore = null;

        if (_bluetoothMonitor is not null)
        {
            _bluetoothMonitor.DeviceConnected -= OnBluetoothDeviceConnected;
            _bluetoothMonitor.Stop();
            _bluetoothMonitor.Dispose();
            _bluetoothMonitor = null;
        }

        if (_comPortMonitor is not null)
        {
            _comPortMonitor.DeviceConnected -= OnComPortDeviceConnected;
            _comPortMonitor.Stop();
            _comPortMonitor.Dispose();
            _comPortMonitor = null;
        }

        _popupService?.Dispose();
        _popupService = null;

        if (_trayService is not null)
        {
            _trayService.TestPopupRequested -= OnTestPopupRequested;
            _trayService.ExitRequested -= OnExitRequested;
            _trayService.BluetoothMonitoringToggleRequested -= OnBluetoothMonitoringToggleRequested;
            _trayService.ComMonitoringToggleRequested -= OnComMonitoringToggleRequested;
            _trayService.Dispose();
            _trayService = null;
        }
    }

    private void UpdateMonitoringSettings(bool enableBluetoothMonitoring, bool enableComMonitoring)
    {
        if (_settings is null || _settingsStore is null)
        {
            return;
        }

        if (_settings.EnableBluetoothMonitoring == enableBluetoothMonitoring
            && _settings.EnableComMonitoring == enableComMonitoring)
        {
            return;
        }

        var previousSettings = new AppSettings
        {
            EnableBluetoothMonitoring = _settings.EnableBluetoothMonitoring,
            EnableComMonitoring = _settings.EnableComMonitoring
        };

        var updatedSettings = new AppSettings
        {
            EnableBluetoothMonitoring = enableBluetoothMonitoring,
            EnableComMonitoring = enableComMonitoring
        };

        try
        {
            ApplyMonitoringState(updatedSettings);
            _settingsStore.Save(updatedSettings);
            _settings = updatedSettings;
            _trayService?.SetMonitoringState(updatedSettings);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ApplyMonitoringState(previousSettings);
            _settings = previousSettings;
            _trayService?.SetMonitoringState(previousSettings);

            Forms.MessageBox.Show(
                $"保存监控设置失败：{exception.Message}",
                "Bluetooth Popup",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private void ApplyMonitoringState(AppSettings settings)
    {
        if (settings.EnableBluetoothMonitoring)
        {
            EnsureBluetoothMonitor().Start();
        }
        else
        {
            _bluetoothMonitor?.Stop();
        }

        if (settings.EnableComMonitoring)
        {
            EnsureComPortMonitor().Start();
        }
        else
        {
            _comPortMonitor?.Stop();
        }
    }

    private BluetoothAudioMonitor EnsureBluetoothMonitor()
    {
        if (_bluetoothMonitor is null)
        {
            _bluetoothMonitor = new BluetoothAudioMonitor();
            _bluetoothMonitor.DeviceConnected += OnBluetoothDeviceConnected;
        }

        return _bluetoothMonitor;
    }

    private ComPortMonitor EnsureComPortMonitor()
    {
        if (_comPortMonitor is null)
        {
            _comPortMonitor = new ComPortMonitor();
            _comPortMonitor.DeviceConnected += OnComPortDeviceConnected;
        }

        return _comPortMonitor;
    }
}
