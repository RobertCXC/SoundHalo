using System.Diagnostics;
using System.Runtime.InteropServices;
using ThreadingTimer = System.Threading.Timer;

namespace BluetoothPopup.Bluetooth;

public sealed class BluetoothAudioDeviceConnectedEventArgs : EventArgs
{
    public BluetoothAudioDeviceConnectedEventArgs(string deviceName, string deviceId)
    {
        DeviceName = deviceName;
        DeviceId = deviceId;
    }

    public string DeviceName { get; }

    public string DeviceId { get; }
}

/// <summary>
/// Watches active Windows audio render endpoints and reports newly connected
/// Bluetooth endpoints. The timer is deliberately lightweight and also works
/// on machines where the WinRT Bluetooth watcher does not report audio profile
/// changes consistently.
/// </summary>
public sealed class BluetoothAudioMonitor : IDisposable
{
    private static readonly PropertyKey PkeyDeviceFriendlyName = new(
        new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    private static readonly PropertyKey PkeyDeviceEnumeratorName = new(
        new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 24);

    private static readonly PropertyKey PkeyDeviceInstanceId = new(
        new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256);

    private static readonly string[] BluetoothNameHints =
    [
        "AirPods",
        "Beats",
        "Bose",
        "FreeBuds",
        "Galaxy Buds",
        "Jabra",
        "LinkBuds",
        "Soundcore",
        "WH-",
        "WF-",
        "蓝牙",
        "Bluetooth"
    ];

    private readonly object _syncRoot = new();
    private ThreadingTimer? _pollTimer;
    private HashSet<string> _knownConnectedDevices = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasInitialSnapshot;
    private bool _disposed;
    private int _isPolling;

    public event EventHandler<BluetoothAudioDeviceConnectedEventArgs>? DeviceConnected;

    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_pollTimer is not null)
            {
                return;
            }

            try
            {
                RefreshSnapshot(reportNewDevices: false);
            }
            catch (Exception exception) when (exception is COMException or UnauthorizedAccessException or InvalidOperationException)
            {
                Debug.WriteLine($"Initial Bluetooth audio scan failed: {exception.Message}");
            }

            _pollTimer = new ThreadingTimer(
                PollTimerCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(1),
                period: TimeSpan.FromSeconds(1));
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
            _knownConnectedDevices.Clear();
            _hasInitialSnapshot = false;
        }
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
            _pollTimer?.Dispose();
            _pollTimer = null;
            _knownConnectedDevices.Clear();
        }
    }

    private void PollTimerCallback(object? state)
    {
        if (_disposed || Interlocked.Exchange(ref _isPolling, 1) != 0)
        {
            return;
        }

        try
        {
            RefreshSnapshot(reportNewDevices: true);
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Bluetooth audio scan failed: {exception.Message}");
        }
        finally
        {
            Volatile.Write(ref _isPolling, 0);
        }
    }

    private void RefreshSnapshot(bool reportNewDevices)
    {
        var connectedDevices = EnumerateConnectedBluetoothAudioDevices();
        List<BluetoothAudioDeviceConnectedEventArgs>? newlyConnected = null;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            if (reportNewDevices && _hasInitialSnapshot)
            {
                foreach (var device in connectedDevices)
                {
                    if (_knownConnectedDevices.Contains(device.Key))
                    {
                        continue;
                    }

                    newlyConnected ??= [];
                    newlyConnected.Add(new BluetoothAudioDeviceConnectedEventArgs(device.Value, device.Key));
                }
            }

            _knownConnectedDevices = connectedDevices.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _hasInitialSnapshot = true;
        }

        if (newlyConnected is null)
        {
            return;
        }

        foreach (var device in newlyConnected)
        {
            try
            {
                DeviceConnected?.Invoke(this, device);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Bluetooth audio event failed: {exception.Message}");
            }
        }
    }

    private static Dictionary<string, string> EnumerateConnectedBluetoothAudioDevices()
    {
        var devices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        IMMDeviceCollection? collection = null;

        try
        {
            var result = enumerator.EnumAudioEndpoints(
                DataFlow.Render,
                DeviceState.Active,
                out collection);
            ThrowIfFailed(result, "EnumAudioEndpoints");

            collection.GetCount(out var count);
            for (uint index = 0; index < count; index++)
            {
                IMMDevice? device = null;
                try
                {
                    result = collection.Item(index, out device);
                    ThrowIfFailed(result, "IMMDeviceCollection.Item");

                    device.GetId(out var deviceId);
                    var friendlyName = ReadStringProperty(device, PkeyDeviceFriendlyName) ?? "Bluetooth 音频设备";
                    var enumeratorName = ReadStringProperty(device, PkeyDeviceEnumeratorName);
                    var instanceId = ReadStringProperty(device, PkeyDeviceInstanceId);

                    if (IsBluetoothEndpoint(deviceId, enumeratorName, instanceId, friendlyName))
                    {
                        devices[deviceId] = friendlyName;
                    }
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
        }

        return devices;
    }

    private static bool IsBluetoothEndpoint(
        string deviceId,
        string? enumeratorName,
        string? instanceId,
        string friendlyName)
    {
        if (ContainsBluetoothMarker(deviceId)
            || ContainsBluetoothMarker(enumeratorName)
            || ContainsBluetoothMarker(instanceId))
        {
            return true;
        }

        return BluetoothNameHints.Any(
            hint => friendlyName.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsBluetoothMarker(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.Contains("BTH", StringComparison.OrdinalIgnoreCase)
                || value.Contains("BLUETOOTH", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadStringProperty(IMMDevice device, PropertyKey key)
    {
        IPropertyStore? store = null;
        PropVariant value = default;

        try
        {
            var result = device.OpenPropertyStore(StorageAccess.Read, out store);
            ThrowIfFailed(result, "IMMDevice.OpenPropertyStore");

            result = store.GetValue(ref key, out value);
            if (result < 0)
            {
                return null;
            }

            return value.GetString();
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            PropVariant.Clear(ref value);
            ReleaseComObject(store);
        }
    }

    private static void ThrowIfFailed(int result, string operation)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private enum DataFlow
    {
        Render,
        Capture,
        All
    }

    [Flags]
    private enum DeviceState
    {
        Active = 0x00000001,
        Disabled = 0x00000002,
        NotPresent = 0x00000004,
        Unplugged = 0x00000008,
        All = 0x0000000F
    }

    private enum StorageAccess
    {
        Read = 0x00000000,
        ReadWrite = 0x00000002
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort VariantType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr PointerValue;

        public readonly string? GetString()
        {
            return VariantType switch
            {
                8 => PointerValue == IntPtr.Zero ? null : Marshal.PtrToStringBSTR(PointerValue),
                31 => PointerValue == IntPtr.Zero ? null : Marshal.PtrToStringUni(PointerValue),
                _ => null
            };
        }

        public static void Clear(ref PropVariant value)
        {
            _ = PropVariantClear(ref value);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant propVariant);
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(DataFlow dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint deviceCount);

        [PreserveSig]
        int Item(uint deviceIndex, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(StorageAccess access, out IPropertyStore propertyStore);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);

        [PreserveSig]
        int GetState(out DeviceState state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }
}
