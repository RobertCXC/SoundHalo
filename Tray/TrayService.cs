using System.Drawing;
using System.Drawing.Drawing2D;
using BluetoothPopup.Native;
using BluetoothPopup.Startup;
using Forms = System.Windows.Forms;

namespace BluetoothPopup.Tray;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Icon _trayIcon;
    private bool _disposed;

    public TrayService()
    {
        _contextMenu = new Forms.ContextMenuStrip();

        var testPopupItem = new Forms.ToolStripMenuItem("测试弹窗");
        testPopupItem.Click += (_, _) => TestPopupRequested?.Invoke(this, EventArgs.Empty);

        _startupItem = new Forms.ToolStripMenuItem("开机自启")
        {
            CheckOnClick = false
        };
        _startupItem.Click += OnStartupItemClick;

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(testPopupItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(_startupItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);
        _contextMenu.Opening += OnContextMenuOpening;

        _trayIcon = CreateTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Icon = _trayIcon,
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
        _contextMenu.Opening -= OnContextMenuOpening;
        _startupItem.Click -= OnStartupItemClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        TestPopupRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _startupItem.Checked = StartupManager.IsEnabled();
        }
        catch
        {
            _startupItem.Checked = false;
        }
    }

    private void OnStartupItemClick(object? sender, EventArgs e)
    {
        try
        {
            var shouldEnable = !StartupManager.IsEnabled();
            StartupManager.SetEnabled(shouldEnable);
            _startupItem.Checked = shouldEnable;
        }
        catch (Exception exception)
        {
            Forms.MessageBox.Show(
                $"设置开机自启失败：{exception.Message}",
                "Bluetooth Popup",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private static Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            using var backgroundBrush = new SolidBrush(Color.FromArgb(255, 75, 112, 130));
            graphics.FillEllipse(backgroundBrush, 1.5f, 1.5f, 29f, 29f);

            using var headsetPen = new Pen(Color.White, 2.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            graphics.DrawArc(headsetPen, 8f, 7f, 16f, 17f, 190f, 160f);
            graphics.DrawLine(headsetPen, 8.8f, 17f, 8.8f, 22f);
            graphics.DrawLine(headsetPen, 23.2f, 17f, 23.2f, 22f);
            graphics.DrawArc(headsetPen, 8.8f, 19f, 5.2f, 6f, 90f, 180f);
            graphics.DrawArc(headsetPen, 18f, 19f, 5.2f, 6f, 270f, 180f);
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(iconHandle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }
}
