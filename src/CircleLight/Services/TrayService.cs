using System.Drawing;
using WinForms = System.Windows.Forms;

namespace CircleLight.Services;

/// <summary>托盘图标与右键菜单。图标是运行时画出来的 16×16 像素图，不依赖外部资源。</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _notifyIcon;
    private readonly IntPtr _iconHandle;

    public TrayService(MainWindow window, Action openSettings)
    {
        _notifyIcon = CreateIcon(out _iconHandle);
        _icon = new NotifyIcon
        {
            Icon = _notifyIcon,
            Text = "CircleLight 待办",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("打开便签");
        open.Click += (_, _) => window.Dispatcher.BeginInvoke(window.ShowNote);

        var add = new ToolStripMenuItem("新建任务");
        add.Click += (_, _) => window.Dispatcher.BeginInvoke(window.BeginQuickAdd);

        var settings = new ToolStripMenuItem("设置");
        settings.Click += (_, _) => window.Dispatcher.BeginInvoke(openSettings);

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => window.Dispatcher.BeginInvoke(window.ExitApp);

        menu.Items.Add(open);
        menu.Items.Add(add);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => window.Dispatcher.BeginInvoke(window.ShowNote);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _notifyIcon.Dispose();
        if (_iconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(_iconHandle);
    }

    private static Icon CreateIcon(out IntPtr handle)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(26, 24, 54));
            using var yellow = new SolidBrush(Color.FromArgb(255, 228, 92));
            using var cyan = new SolidBrush(Color.FromArgb(102, 224, 255));

            g.FillRectangle(bg, 1, 1, 14, 14);
            g.FillRectangle(yellow, 1, 1, 14, 2);
            g.FillRectangle(yellow, 1, 13, 14, 2);
            g.FillRectangle(yellow, 1, 1, 2, 14);
            g.FillRectangle(yellow, 13, 1, 2, 14);
            g.FillRectangle(cyan, 5, 5, 6, 6);
            g.FillRectangle(bg, 7, 7, 2, 2);
        }

        handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }
}
