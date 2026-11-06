using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using SlashTag.Services;

namespace SlashTag.Views;

/// <summary>透明覆盖窗口：贴边动画只画碎片/切片，不露出主窗口的深色底。</summary>
public partial class AnimationOverlayWindow : Window
{
    public AnimationOverlayWindow() => InitializeComponent();

    public Canvas CanvasLayer => Layer;

    public void Prepare(FrameworkElement noteShell)
    {
        Width = Math.Max(1, noteShell.ActualWidth);
        Height = Math.Max(1, noteShell.ActualHeight);

        var point = noteShell.PointToScreen(new System.Windows.Point(0, 0));
        Show();
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
            (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    public void Finish() => Close();
}
