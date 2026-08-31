using System.Drawing;
using Forms = System.Windows.Forms;

namespace BluetoothPopup.Tray;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private bool _disposed;

    public TrayService()
    {
        _contextMenu = new Forms.ContextMenuStrip();

        var testPopupItem = new Forms.ToolStripMenuItem("测试弹窗");
        testPopupItem.Click += (_, _) => TestPopupRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(testPopupItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = SystemIcons.Application,
            Text = "Bluetooth Popup",
            Visible = true
        };
        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
    }

    public event EventHandler? TestPopupRequested;

    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.DoubleClick -= OnNotifyIconDoubleClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        TestPopupRequested?.Invoke(this, EventArgs.Empty);
    }
}
