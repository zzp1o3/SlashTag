using System;
using System.Windows;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SlashTag.Services;

namespace SlashTag.Views;

/// <summary>贴边收起后留在屏幕边缘的黄色感叹号。</summary>
public partial class EdgeIndicatorWindow : Window
{
    public EdgeIndicatorWindow() => InitializeComponent();

    public event Action? Hovered;

    /// <summary>按物理像素显示在指定位置。</summary>
    public void ShowAt(int x, int y)
    {
        if (!IsVisible) Show();
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

        var scale = new ScaleTransform(0.2, 0.2);
        Root.RenderTransform = scale;
        var duration = TimeSpan.FromMilliseconds(260);
        var pop = new DoubleAnimationUsingKeyFrames { Duration = duration };
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(0.2, TimeSpan.Zero));
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(1.35, TimeSpan.FromMilliseconds(120)));
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(1, duration));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => Hovered?.Invoke();
}
