using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BluetoothPopup.Native;

namespace BluetoothPopup.Popup;

public enum PopupState
{
    Entering,
    CirclePause,
    Expanding,
    Showing,
    Exiting
}

public partial class BluetoothPopupWindow : Window
{
    private const double CircleSize = 48d;
    private const double PopupWidth = 350d;
    private const double PopupHeight = 80d;

    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CirclePauseDuration = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ContentFadeDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(2_200);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(450);

    private readonly string _deviceName;
    private readonly TranslateTransform _translateTransform = new();
    private readonly ScaleTransform _scaleTransform = new(1d, 1d);
    private bool _positionInitialized;
    private IntPtr _windowHandle;

    public BluetoothPopupWindow(string deviceName)
    {
        InitializeComponent();
        _deviceName = string.IsNullOrWhiteSpace(deviceName) ? "Bluetooth 音频设备" : deviceName;
        DeviceNameText.Text = _deviceName;

        // ScaleTransform first so exit can gently shrink the capsule while it drifts/fades away.
        var surfaceTransform = new TransformGroup();
        surfaceTransform.Children.Add(_scaleTransform);
        surfaceTransform.Children.Add(_translateTransform);
        PopupSurface.RenderTransform = surfaceTransform;
    }

    public PopupState State { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        NativeMethods.AddPopupWindowStyles(_windowHandle);
    }

    public async Task PlayAsync(CancellationToken cancellationToken = default)
    {
        InitializePosition();

        Show();
        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(_windowHandle, NativeMethods.SwShownoactivate);
        }

        State = PopupState.Entering;
        await AnimateDoubleAsync(
            this,
            this,
            TopProperty,
            Top,
            GetWorkAreaTop() + 18d - ((PopupHeight - CircleSize) / 2d),
            EnterDuration,
            CreateSettleEase(),
            cancellationToken);

        State = PopupState.CirclePause;
        await Task.Delay(CirclePauseDuration, cancellationToken);

        State = PopupState.Expanding;

        var expandTask = Task.WhenAll(
            AnimateDoubleAsync(
                this,
                PopupSurface,
                FrameworkElement.WidthProperty,
                PopupSurface.Width,
                PopupWidth,
                ExpandDuration,
                CreateExpandEase(),
                cancellationToken),
            AnimateDoubleAsync(
                this,
                PopupSurface,
                FrameworkElement.HeightProperty,
                PopupSurface.Height,
                PopupHeight,
                ExpandDuration,
                CreateExpandEase(),
                cancellationToken),
            AnimateCornerRadiusAsync(
                this,
                PopupSurface,
                new CornerRadius(24),
                new CornerRadius(22),
                ExpandDuration,
                CreateExpandEase(),
                cancellationToken),
            AnimateDoubleAsync(
                this,
                CircleSurface,
                UIElement.OpacityProperty,
                1,
                0,
                TimeSpan.FromMilliseconds(160),
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken),
            AnimateDoubleAsync(
                this,
                CircleIcon,
                UIElement.OpacityProperty,
                1,
                0,
                TimeSpan.FromMilliseconds(160),
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken),
            AnimateDoubleAsync(
                this,
                PopupSurface,
                UIElement.OpacityProperty,
                0,
                1,
                TimeSpan.FromMilliseconds(220),
                new CubicEase { EasingMode = EasingMode.EaseOut },
                cancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(190), cancellationToken);
        var contentFadeTask = AnimateDoubleAsync(
            this,
            DetailsPanel,
            UIElement.OpacityProperty,
            0,
            1,
            ContentFadeDuration,
            new CubicEase { EasingMode = EasingMode.EaseOut },
            cancellationToken);

        await Task.WhenAll(expandTask, contentFadeTask);

        State = PopupState.Showing;
        await Task.Delay(HoldDuration, cancellationToken);

        State = PopupState.Exiting;
        await Task.WhenAll(
            AnimateDoubleAsync(
                this,
                PopupSurface,
                UIElement.OpacityProperty,
                1,
                0,
                ExitDuration,
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken),
            AnimateDoubleAsync(
                this,
                _translateTransform,
                TranslateTransform.YProperty,
                0,
                -8,
                ExitDuration,
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken),
            // A gentle shrink alongside the fade/drift reads as a soft dissolve rather than an abrupt cut.
            AnimateDoubleAsync(
                this,
                _scaleTransform,
                ScaleTransform.ScaleXProperty,
                1,
                0.94,
                ExitDuration,
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken),
            AnimateDoubleAsync(
                this,
                _scaleTransform,
                ScaleTransform.ScaleYProperty,
                1,
                0.94,
                ExitDuration,
                new CubicEase { EasingMode = EasingMode.EaseIn },
                cancellationToken));

        Close();
    }

    private void InitializePosition()
    {
        if (_positionInitialized)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - PopupWidth) / 2d);
        Top = workArea.Top - PopupHeight;
        Width = PopupWidth;
        Height = PopupHeight;
        _positionInitialized = true;
    }

    private static double GetWorkAreaTop()
    {
        return SystemParameters.WorkArea.Top;
    }

    // A tiny overshoot on the drop-in gives the settle a touch of natural physicality
    // instead of a hard linear stop.
    private static IEasingFunction CreateSettleEase() =>
        new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 };

    // Same idea for the circle-to-capsule expansion: a soft "pop" feels less mechanical
    // than a flat ease-out while still settling precisely on the target size.
    private static IEasingFunction CreateExpandEase() =>
        new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };

    private static Task AnimateDoubleAsync(
        FrameworkElement containingObject,
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        TimeSpan duration,
        IEasingFunction easingFunction,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(duration),
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.HoldEnd
        };

        return BeginAnimationAsync(
            containingObject,
            target,
            property,
            animation,
            completion,
            cancellationToken);
    }

    private static Task AnimateCornerRadiusAsync(
        FrameworkElement containingObject,
        DependencyObject target,
        CornerRadius from,
        CornerRadius to,
        TimeSpan duration,
        IEasingFunction easingFunction,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new CornerRadiusAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(duration),
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.HoldEnd
        };

        return BeginAnimationAsync(
            containingObject,
            target,
            Border.CornerRadiusProperty,
            animation,
            completion,
            cancellationToken);
    }

    private static Task BeginAnimationAsync(
        FrameworkElement containingObject,
        DependencyObject target,
        DependencyProperty property,
        AnimationTimeline animation,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));

        EventHandler? completedHandler = null;
        CancellationTokenRegistration cancellationRegistration = default;
        completedHandler = (_, _) =>
        {
            storyboard.Completed -= completedHandler;
            cancellationRegistration.Dispose();
            completion.TrySetResult(null);
        };

        cancellationRegistration = cancellationToken.Register(() =>
        {
            void RemoveStoryboard()
            {
                storyboard.Completed -= completedHandler;
                storyboard.Remove(containingObject);
            }

            if (containingObject.Dispatcher.CheckAccess())
            {
                RemoveStoryboard();
            }
            else
            {
                _ = containingObject.Dispatcher.BeginInvoke(RemoveStoryboard);
            }

            completion.TrySetCanceled(cancellationToken);
        });

        storyboard.Completed += completedHandler;
        storyboard.Begin(containingObject, HandoffBehavior.SnapshotAndReplace, isControllable: true);
        return completion.Task;
    }
}
