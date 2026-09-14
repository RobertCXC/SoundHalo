using System.Runtime.InteropServices;

namespace BluetoothPopup.Native;

internal static class NativeMethods
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;

    internal const int SwShownoactivate = 4;
    internal const int WmDeviceChange = 0x0219;
    internal const int DbtDevNodesChanged = 0x0007;
    internal const int DbtDeviceArrival = 0x8000;
    internal const int DbtDeviceInterface = 0x00000005;
    internal const int DeviceNotifyWindowHandle = 0x00000000;
    internal static readonly Guid GuidDevinterfaceComport = new("86E0D1E0-8089-11D0-9CE4-08003E301F73");

    [StructLayout(LayoutKind.Sequential)]
    internal struct DevBroadcastHdr
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DevBroadcastDeviceInterface
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
        internal Guid ClassGuid;
        internal char Name;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", EntryPoint = "RegisterDeviceNotificationW", SetLastError = true)]
    internal static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterDeviceNotification(IntPtr handle);

    internal static void AddPopupWindowStyles(IntPtr handle)
    {
        var currentStyle = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        var popupStyle = currentStyle | WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(popupStyle));
    }

}
