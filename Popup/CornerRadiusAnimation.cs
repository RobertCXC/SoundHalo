using System.Windows;
using System.Windows.Media.Animation;

namespace BluetoothPopup.Popup;

internal sealed class CornerRadiusAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From),
        typeof(CornerRadius),
        typeof(CornerRadiusAnimation),
        new PropertyMetadata(default(CornerRadius)));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To),
        typeof(CornerRadius),
        typeof(CornerRadiusAnimation),
        new PropertyMetadata(default(CornerRadius)));

    public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
        nameof(EasingFunction),
        typeof(IEasingFunction),
        typeof(CornerRadiusAnimation),
        new PropertyMetadata(null));

    public CornerRadius From
    {
        get => (CornerRadius)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public CornerRadius To
    {
        get => (CornerRadius)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(CornerRadius);

    protected override Freezable CreateInstanceCore() => new CornerRadiusAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue,
        object defaultDestinationValue,
        AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 0;
        progress = EasingFunction?.Ease(progress) ?? progress;
        var from = From;
        var to = To;

        return new CornerRadius(
            Interpolate(from.TopLeft, to.TopLeft, progress),
            Interpolate(from.TopRight, to.TopRight, progress),
            Interpolate(from.BottomRight, to.BottomRight, progress),
            Interpolate(from.BottomLeft, to.BottomLeft, progress));
    }

    private static double Interpolate(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }
}
