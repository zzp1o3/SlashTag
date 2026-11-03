using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SlashTag.Controls;

/// <summary>无边框像素风对话框基类：统一阴影、黄色边框、可拖动标题条。</summary>
public class PixelWindow : Window
{
    public static readonly RoutedCommand CloseCommand =
        new("ClosePixelWindow", typeof(PixelWindow));

    public PixelWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 9, 24));
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");

        SetResourceReference(StyleProperty, "PixelWindowStyle");
        CommandBindings.Add(new CommandBinding(CloseCommand, (_, _) => Close()));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }
}

/// <summary>标题条拖动手柄：拖动时移动所属窗口。</summary>
public class WindowDragThumb : Thumb
{
    public WindowDragThumb()
    {
        DragDelta += (_, e) =>
        {
            var window = Window.GetWindow(this);
            if (window is null) return;
            window.Left += e.HorizontalChange;
            window.Top += e.VerticalChange;
        };
    }
}
