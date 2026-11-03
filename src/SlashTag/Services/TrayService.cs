using System.Drawing;
using SlashTag.Util;
using WinForms = System.Windows.Forms;

namespace SlashTag.Services;

/// <summary>托盘图标与右键菜单。图标是运行时画出来的 16×16 像素图，不依赖外部资源。</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _notifyIcon;
    private readonly IntPtr _iconHandle;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _addItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _exitItem;

    public TrayService(MainWindow window, Action openSettings)
    {
        _notifyIcon = CreateIcon(out _iconHandle);
        _icon = new NotifyIcon
        {
            Icon = _notifyIcon,
            Text = Loc.Instance["TrayText"],
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem(Loc.Instance["TrayOpen"]);
        _openItem.Click += (_, _) => window.Dispatcher.BeginInvoke(window.ShowNote);

        _addItem = new ToolStripMenuItem(Loc.Instance["TrayAdd"]);
        _addItem.Click += (_, _) => window.Dispatcher.BeginInvoke(window.BeginQuickAdd);

        _settingsItem = new ToolStripMenuItem(Loc.Instance["TraySettings"]);
        _settingsItem.Click += (_, _) => window.Dispatcher.BeginInvoke(openSettings);

        _exitItem = new ToolStripMenuItem(Loc.Instance["TrayExit"]);
        _exitItem.Click += (_, _) => window.Dispatcher.BeginInvoke(window.ExitApp);

        menu.Items.Add(_openItem);
        menu.Items.Add(_addItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => window.Dispatcher.BeginInvoke(window.ShowNote);
    }

    /// <summary>语言切换后刷新托盘文案。</summary>
    public void RefreshLanguage()
    {
        _icon.Text = Loc.Instance["TrayText"];
        _openItem.Text = Loc.Instance["TrayOpen"];
        _addItem.Text = Loc.Instance["TrayAdd"];
        _settingsItem.Text = Loc.Instance["TraySettings"];
        _exitItem.Text = Loc.Instance["TrayExit"];
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
