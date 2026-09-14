using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BluetoothPopup.Native;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace BluetoothPopup.ComPort;

public sealed class ComPortDeviceConnectedEventArgs : EventArgs
{
    public ComPortDeviceConnectedEventArgs(string portName)
    {
        PortName = portName;
    }

    public string PortName { get; }
}

public sealed class ComPortMonitor : IDisposable
{
    private const string SerialCommRegistryPath = @"HARDWARE\DEVICEMAP\SERIALCOMM";

    private readonly object _syncRoot = new();
    private DeviceNotificationWindow? _deviceNotificationWindow;
    private HashSet<string> _knownPorts = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasInitialSnapshot;
    private bool _isStarted;
    private bool _disposed;

    public event EventHandler<ComPortDeviceConnectedEventArgs>? DeviceConnected;

    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
        }

        try
        {
            RefreshSnapshot(reportNewPorts: false);

            var notificationWindow = new DeviceNotificationWindow(OnDeviceChange);

            lock (_syncRoot)
            {
                if (_disposed || !_isStarted)
                {
                    notificationWindow.Dispose();
                    return;
                }

                _deviceNotificationWindow = notificationWindow;
            }
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            Debug.WriteLine($"COM port monitor startup failed: {exception.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        DeviceNotificationWindow? notificationWindow;

        lock (_syncRoot)
        {
            if (!_isStarted && _deviceNotificationWindow is null)
            {
                return;
            }

            _isStarted = false;
            notificationWindow = _deviceNotificationWindow;
            _deviceNotificationWindow = null;
            _knownPorts.Clear();
            _hasInitialSnapshot = false;
        }

        notificationWindow?.Dispose();
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
    }

    private void OnDeviceChange()
    {
        try
        {
            RefreshSnapshot(reportNewPorts: true);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Refreshing COM port snapshot failed: {exception.Message}");
        }
    }

    private void RefreshSnapshot(bool reportNewPorts)
    {
        var currentPorts = GetCurrentPorts();
        string[] newPorts;

        lock (_syncRoot)
        {
            if (!_isStarted)
            {
                return;
            }

            newPorts = reportNewPorts && _hasInitialSnapshot
                ? currentPorts
                    .Except(_knownPorts, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static port => port, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];

            _knownPorts = currentPorts;
            _hasInitialSnapshot = true;
        }

        foreach (var portName in newPorts)
        {
            DeviceConnected?.Invoke(this, new ComPortDeviceConnectedEventArgs(portName));
        }
    }

    private static HashSet<string> GetCurrentPorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var serialCommKey = Registry.LocalMachine.OpenSubKey(SerialCommRegistryPath, writable: false);
        if (serialCommKey is null)
        {
            return ports;
        }

        foreach (var valueName in serialCommKey.GetValueNames())
        {
            if (serialCommKey.GetValue(valueName) is not string portName)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(portName))
            {
                continue;
            }

            ports.Add(portName.Trim());
        }

        return ports;
    }

    private sealed class DeviceNotificationWindow : Forms.NativeWindow, IDisposable
    {
        private readonly Action _deviceChangeHandler;
        private IntPtr _notificationHandle;
        private bool _disposed;

        public DeviceNotificationWindow(Action deviceChangeHandler)
        {
            _deviceChangeHandler = deviceChangeHandler;

            CreateHandle(new Forms.CreateParams
            {
                Caption = "SoundHaloComPortMonitor"
            });

            _notificationHandle = RegisterForComPortNotifications(Handle);
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == NativeMethods.WmDeviceChange && ShouldRefresh(m.WParam, m.LParam))
            {
                _deviceChangeHandler();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_notificationHandle != IntPtr.Zero)
            {
                _ = NativeMethods.UnregisterDeviceNotification(_notificationHandle);
                _notificationHandle = IntPtr.Zero;
            }

            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        private static bool ShouldRefresh(IntPtr eventTypeHandle, IntPtr deviceInfoHandle)
        {
            var eventType = unchecked((int)eventTypeHandle.ToInt64());
            if (eventType == NativeMethods.DbtDevNodesChanged)
            {
                return true;
            }

            if (eventType != NativeMethods.DbtDeviceArrival || deviceInfoHandle == IntPtr.Zero)
            {
                return false;
            }

            var header = Marshal.PtrToStructure<NativeMethods.DevBroadcastHdr>(deviceInfoHandle);
            return header.DeviceType == NativeMethods.DbtDeviceInterface;
        }

        private static IntPtr RegisterForComPortNotifications(IntPtr windowHandle)
        {
            var filter = new NativeMethods.DevBroadcastDeviceInterface
            {
                Size = Marshal.SizeOf<NativeMethods.DevBroadcastDeviceInterface>(),
                DeviceType = NativeMethods.DbtDeviceInterface,
                ClassGuid = NativeMethods.GuidDevinterfaceComport
            };

            var filterBuffer = Marshal.AllocHGlobal(filter.Size);
            try
            {
                Marshal.StructureToPtr(filter, filterBuffer, fDeleteOld: false);
                var notificationHandle = NativeMethods.RegisterDeviceNotification(
                    windowHandle,
                    filterBuffer,
                    NativeMethods.DeviceNotifyWindowHandle);

                if (notificationHandle == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "注册 COM 设备通知失败。");
                }

                return notificationHandle;
            }
            finally
            {
                Marshal.FreeHGlobal(filterBuffer);
            }
        }
    }
}
