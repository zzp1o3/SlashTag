using System;
using System.Windows;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using System.Windows.Interop;
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
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e) => Hovered?.Invoke();
}
