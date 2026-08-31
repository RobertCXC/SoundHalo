using System.Windows.Threading;

namespace BluetoothPopup.Popup;

public sealed class PopupService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _popupCancellation;
    private BluetoothPopupWindow? _activePopup;
    private bool _disposed;

    public PopupService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void ShowTestPopup()
    {
        ShowPopup("AirPods Pro");
    }

    public void ShowPopup(string deviceName)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => ShowPopup(deviceName));
            return;
        }

        if (_disposed)
        {
            return;
        }

        CancelActivePopup();

        var popup = new BluetoothPopupWindow(deviceName);
        var cancellation = new CancellationTokenSource();

        lock (_syncRoot)
        {
            _activePopup = popup;
            _popupCancellation = cancellation;
        }

        _ = PlayPopupAsync(popup, cancellation);
    }

    public void Dispose()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Dispose);
            return;
        }

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelActivePopup();
    }

    private async Task PlayPopupAsync(BluetoothPopupWindow popup, CancellationTokenSource cancellation)
    {
        try
        {
            await popup.PlayAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (popup.IsVisible)
            {
                popup.Close();
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Popup animation failed: {exception.Message}");
            if (popup.IsVisible)
            {
                popup.Close();
            }
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activePopup, popup))
                {
                    _activePopup = null;
                    _popupCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void CancelActivePopup()
    {
        lock (_syncRoot)
        {
            _popupCancellation?.Cancel();

            if (_activePopup is { IsVisible: true } popup)
            {
                popup.Close();
            }

            _activePopup = null;
            _popupCancellation = null;
        }
    }
}
