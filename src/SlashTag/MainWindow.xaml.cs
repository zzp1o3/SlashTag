using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SlashTag.Data;
using SlashTag.Services;
using SlashTag.Util;
using SlashTag.ViewModels;
using SlashTag.Views;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using CheckBox = System.Windows.Controls.CheckBox;

namespace SlashTag;

public partial class MainWindow : Window
{
    private const string BoundsKey = "window.bounds";
    private const string ScaleKey = "ui.scale";
    private const string PinKey = "window.pin";
    private const string PosKey = "window.pos";
    private const double MinScale = 0.8;
    private const double MaxScale = 1.6;

    private readonly TaskStore _store;
    private readonly MainViewModel _vm;
    private readonly EffectService _effects;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _dataWatcher;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _slideTimer;
    private long _lastDataVersion;
    private int _lastLevel = 1;
    private bool _loaded;
    private bool _exiting;
    private bool _editorFromQuickAdd;
    private double _uiScale = 1.0;
    private PageKind? _page;
    private bool _pinned;
    private bool _collapsed;
    private bool _animating;
    private bool _hideRight;
    private int _expandedPhysLeft;
    private int _expandedPhysTop;
    private int _slideFromX;
    private int _slideToX;
    private int _slideY;
    private int _slideDuration;
    private long _slideStart;
    private bool _slideAccelerate;
    private NativeMethods.RECT _activeWork;

    private enum PageKind { History, Settings, Editor }

    public MainWindow(TaskStore store)
    {
        InitializeComponent();
        _store = store;
        _vm = new MainViewModel(store);
        DataContext = _vm;
        _effects = new EffectService(EffectLayer, Shell);

        _uiScale = ReadScale(store);
        ApplyScale();

        _pinned = store.GetSetting(PinKey) == "1";
        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (_pinned || _collapsed || _animating || IsMouseOver) return;
            Collapse();
        };
        UpdatePinVisual();

        _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _slideTimer.Tick += SlideTimer_Tick;

        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]") RefreshLanguage();
        };

        EditorPage.Saved += created =>
        {
            if (created && _editorFromQuickAdd)
            {
                QuickAddBox.Clear();
                HideQuickAdd();
            }
            _editorFromQuickAdd = false;
            ShowMain();
            if (created) PlayAddForNewest();
        };
        EditorPage.Cancelled += () =>
        {
            _editorFromQuickAdd = false;
            ShowMain();
        };

        _store.ResetDailyCounts(DateTime.Now);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveBounds();
        };
        LocationChanged += (_, _) =>
        {
            if (!_animating && !_collapsed)
            {
                var rect = WindowRect();
                _expandedPhysLeft = rect.Left;
                _expandedPhysTop = rect.Top;
            }
            ScheduleSave();
        };
        SizeChanged += (_, _) => ScheduleSave();

        _vm.Reload(DateTime.Now);
        _lastLevel = _vm.Level;
        TitleText.Text = Loc.Instance["QuestLog"];

        _lastDataVersion = _store.GetDataVersion();
        _dataWatcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _dataWatcher.Tick += (_, _) =>
        {
            var version = _store.GetDataVersion();
            if (version == _lastDataVersion) return;
            _lastDataVersion = version;
            ReloadLists();
        };
        _dataWatcher.Start();
    }

    public void BindReminders(ReminderService reminders)
    {
        reminders.MinuteTick += now => _vm.RefreshTimes(now);
        reminders.DayChanged += () => ReloadLists();
    }

    public void ShowNote()
    {
        if (_collapsed) Expand();
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void BeginQuickAdd()
    {
        ShowNote();
        ShowQuickAdd();
    }

    public void OpenSettings()
    {
        ShowNote();
        SettingsPage.Initialize(_store);
        ShowPage(PageKind.Settings);
    }

    public void ExitApp()
    {
        _exiting = true;
        _dataWatcher.Stop();
        _collapseTimer.Stop();
        SaveBounds();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowBounds();
        ApplySavedPosition();
        _loaded = true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting)
        {
            SaveBounds();
            return;
        }

        e.Cancel = true;
        SaveBounds();
        Hide();
    }

    // ---------- 拖动与缩放 ----------

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private static bool IsInsideButton(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is ButtonBase) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    // ---------- 位置与大小持久化 ----------

    private void ScheduleSave()
    {
        if (!_loaded) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveBounds()
    {
        if (!_loaded) return;

        var rect = _collapsed || _animating
            ? new NativeMethods.RECT { Left = _expandedPhysLeft, Top = _expandedPhysTop }
            : WindowRect();

        _store.SetSetting(BoundsKey,
            string.Join(';', Left.ToString("0.##", CultureInfo.InvariantCulture),
                Top.ToString("0.##", CultureInfo.InvariantCulture),
                Width.ToString("0.##", CultureInfo.InvariantCulture),
                Height.ToString("0.##", CultureInfo.InvariantCulture)));
        _store.SetSetting(PosKey, $"{rect.Left};{rect.Top}");
    }

    private void RestoreWindowBounds()
    {
        Width = Math.Round(340 * _uiScale);
        Height = Math.Round(560 * _uiScale);

        var raw = _store.GetSetting(BoundsKey);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(';');
            if (parts.Length == 4
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var left)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var top)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                var view = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

                Width = Math.Clamp(width, MinWidth, Math.Max(MinWidth, view.Width));
                Height = Math.Clamp(height, MinHeight, Math.Max(MinHeight, view.Height));
                Left = left;
                Top = top;

                var visible = Rect.Intersect(new Rect(Left, Top, Width, Height), view);
                if (!visible.IsEmpty && visible.Width >= 80 && visible.Height >= 80) return;
            }
        }

        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 32;
        Top = work.Top + 32;
    }

    // ---------- 任务操作 ----------

    private void ReloadLists(bool scrollToTop = false)
    {
        var offset = TaskScroll.VerticalOffset;
        _store.ResetDailyCounts(DateTime.Now);
        _vm.Reload(DateTime.Now);

        if (_vm.Level != _lastLevel)
        {
            if (_vm.Level > _lastLevel) _effects.PlayLevelUp(_vm.Level);
            _lastLevel = _vm.Level;
        }

        _lastDataVersion = _store.GetDataVersion();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TaskScroll.ScrollToVerticalOffset(scrollToTop ? 0 : offset);
        }), DispatcherPriority.Background);
    }

    private void TaskCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TaskViewModel task } box) return;

        if (box.IsChecked == true)
        {
            var row = ActiveList.ItemContainerGenerator.ContainerFromItem(task) as FrameworkElement;
            var rect = row is null ? Rect.Empty : _effects.RectOf(row);
            var snapshot = row is null ? null : EffectService.Capture(row);

            if (task.IsCounted)
            {
                var counted = _store.IncrementCount(task.Id, DateTime.Now);
                ReloadLists();
                if (row is not null && counted is not null)
                    _effects.PlayComplete(rect, snapshot, counted.IsChecked && task.IsOverdue, false, counted.IsChecked);
                return;
            }

            _store.SetChecked(task.Id, true, DateTime.Now);
            ReloadLists();

            if (row is not null)
                _effects.PlayComplete(rect, snapshot, task.IsOverdue, !task.IsLongTerm);
            return;
        }

        if (task.IsCounted)
        {
            _store.DecrementCount(task.Id, DateTime.Now);
            ReloadLists();
            return;
        }

        _store.SetChecked(task.Id, false, DateTime.Now);
        ReloadLists();
    }

    private void ClearedCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: TaskViewModel task })
        {
            _store.UndoCompletion(task.Id, DateTime.Now);
            ReloadLists();
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TaskViewModel task)
        {
            _store.DeleteTask(task.Id, DateTime.Now);
            ReloadLists();
        }
    }

    private void TaskTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if ((sender as FrameworkElement)?.DataContext is TaskViewModel task)
        {
            OpenEditor(task.Id);
            e.Handled = true;
        }
    }

    private void OpenEditor(long taskId)
    {
        var task = _store.GetTask(taskId);
        if (task is null) return;

        ShowNote();
        _editorFromQuickAdd = false;
        EditorPage.Initialize(_store, task, null);
        ShowPage(PageKind.Editor);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => ShowQuickAdd();

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        ShowNote();
        ShowQuickAdd();
    }

    private void ShowQuickAdd()
    {
        AddButton.Visibility = Visibility.Collapsed;
        QuickAddPanel.Visibility = Visibility.Visible;
        QuickAddBox.Focus();
        QuickAddBox.CaretIndex = QuickAddBox.Text.Length;
    }

    private void HideQuickAdd()
    {
        QuickAddPanel.Visibility = Visibility.Collapsed;
        AddButton.Visibility = Visibility.Visible;
    }

    private void QuickAddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddQuickTask();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideQuickAdd();
            e.Handled = true;
        }
    }

    private void AddQuickTask()
    {
        var title = QuickAddBox.Text.Trim();
        if (title.Length == 0) return;

        _store.AddTask(title, null, null, isLongTerm: false, DateTime.Now);
        QuickAddBox.Clear();
        ReloadLists(scrollToTop: true);
        PlayAddForNewest();
        QuickAddBox.Focus();
    }

    private void QuickAddDetail_Click(object sender, RoutedEventArgs e)
    {
        ShowNote();
        _editorFromQuickAdd = true;
        EditorPage.Initialize(_store, null, QuickAddBox.Text.Trim());
        ShowPage(PageKind.Editor);
    }

    /// <summary>新增的任务排在最前（id 倒序），刷新后对它播放入场动效。</summary>
    private void PlayAddForNewest()
    {
        if (_vm.ActiveTasks.Count == 0) return;
        var item = _vm.ActiveTasks[0];
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ActiveList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement row)
                _effects.PlayAdd(row);
        }), DispatcherPriority.Loaded);
    }

    private void ClearedToggle_Click(object sender, RoutedEventArgs e) => _vm.ToggleCleared();

    private void History_Click(object sender, RoutedEventArgs e)
    {
        ShowNote();
        HistoryPage.Load(_store);
        ShowPage(PageKind.History);
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void Back_Click(object sender, RoutedEventArgs e) => ShowMain();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && PageHost.Visibility == Visibility.Visible)
        {
            ShowMain();
            e.Handled = true;
        }
    }

    /// <summary>便签内切换页面，保持同一个窗口。</summary>
    private void ShowPage(PageKind page)
    {
        HistoryPage.Visibility = page == PageKind.History ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == PageKind.Settings ? Visibility.Visible : Visibility.Collapsed;
        EditorPage.Visibility = page == PageKind.Editor ? Visibility.Visible : Visibility.Collapsed;
        PageHost.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        HeaderAddButton.Visibility = Visibility.Collapsed;
        HeaderSettingsButton.Visibility = Visibility.Collapsed;
        _page = page;
        TitleText.Text = CurrentPageTitle();
    }

    private void ShowMain()
    {
        PageHost.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        HeaderAddButton.Visibility = Visibility.Visible;
        HeaderSettingsButton.Visibility = Visibility.Visible;
        _page = null;
        TitleText.Text = Loc.Instance["QuestLog"];
        ReloadLists();
    }

    private string CurrentPageTitle() => _page switch
    {
        PageKind.History => Loc.Instance["HistoryTitle"],
        PageKind.Settings => Loc.Instance["SettingsTitle"],
        PageKind.Editor => EditorPage.IsEditing ? Loc.Instance["EditQuestTitle"] : Loc.Instance["NewQuestTitle"],
        _ => Loc.Instance["QuestLog"],
    };

    private void RefreshLanguage()
    {
        TitleText.Text = CurrentPageTitle();
        _vm.Reload(DateTime.Now);
        if (HistoryPage.Visibility == Visibility.Visible) HistoryPage.Load(_store);
        EditorPage.ClearError();
        UpdatePinVisual();
    }

    // ---------- Ctrl + 滚轮缩放 ----------

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        SetScale(_uiScale + (e.Delta > 0 ? 0.1 : -0.1));
        e.Handled = true;
    }

    private void SetScale(double value)
    {
        var next = Math.Clamp(Math.Round(value, 1), MinScale, MaxScale);
        if (Math.Abs(next - _uiScale) < 0.001) return;

        var baseWidth = Width / _uiScale;
        var baseHeight = Height / _uiScale;
        _uiScale = next;
        ApplyScale();
        Width = Math.Round(baseWidth * _uiScale);
        Height = Math.Round(baseHeight * _uiScale);
        _store.SetSetting(ScaleKey, _uiScale.ToString("0.0", CultureInfo.InvariantCulture));
        SaveBounds();
    }

    private void ApplyScale()
    {
        RootShell.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
        MinWidth = Math.Round(300 * _uiScale);
        MinHeight = Math.Round(400 * _uiScale);
    }

    private static double ReadScale(TaskStore store)
    {
        var raw = store.GetSetting(ScaleKey);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, MinScale, MaxScale)
            : 1.0;
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        _collapseTimer.Stop();
        SaveBounds();
        Hide();
    }

    // ---------- 图钉与贴边隐藏 ----------

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        _store.SetSetting(PinKey, _pinned ? "1" : "0");
        UpdatePinVisual();

        if (_pinned)
        {
            _collapseTimer.Stop();
            if (_collapsed) Expand();
        }
        else if (!IsMouseOver)
        {
            ScheduleCollapse();
        }
    }

    private void UpdatePinVisual()
    {
        PinFill.Visibility = _pinned ? Visibility.Visible : Visibility.Collapsed;
        PinButton.ToolTip = Loc.Instance[_pinned ? "PinOnTip" : "PinOffTip"];
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (_collapsed && !_animating) Expand();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_pinned || _collapsed || _animating) return;
        ScheduleCollapse();
    }

    private void ScheduleCollapse()
    {
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private double StripWidth => 16 * _uiScale + 6;


    private void Collapse()
    {
        if (_pinned || _collapsed) return;

        var info = MonitorInfo();
        var work = info.rcWork;
        var rect = WindowRect();
        _expandedPhysLeft = rect.Left;
        _expandedPhysTop = rect.Top;

        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var canHideRight = info.rcMonitor.Right >= virtualScreen.Right - 1;
        var canHideLeft = info.rcMonitor.Left <= virtualScreen.Left + 1;
        _hideRight = canHideRight != canHideLeft
            ? canHideRight
            : (rect.Left + rect.Right) / 2 >= (work.Left + work.Right) / 2;
        _activeWork = work;

        var dpi = WindowDpiScale();
        var strip = (int)Math.Ceiling(StripWidth * dpi);
        var width = rect.Right - rect.Left;
        var target = _hideRight ? work.Right - strip : work.Left - width + strip;

        EdgeTab.HorizontalAlignment = _hideRight ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right;
        EdgeTab.Visibility = Visibility.Visible;
        _collapsed = true;
        SlideTo(target, rect.Top, 220, accelerate: true);
    }

    private void Expand()
    {
        if (!_collapsed) return;

        var work = _activeWork;
        if (work.Right <= work.Left) work = MonitorInfo().rcWork;
        var rect = WindowRect();
        var width = rect.Right - rect.Left;
        var target = _hideRight ? work.Right - width : work.Left;

        EdgeTab.Visibility = Visibility.Collapsed;
        _collapsed = false;
        SlideTo(target, rect.Top, 220, accelerate: false);
    }

    private void SlideTo(int targetX, int targetY, int durationMs, bool accelerate)
    {
        _animating = true;
        _slideFromX = WindowRect().Left;
        _slideToX = targetX;
        _slideY = targetY;
        _slideDuration = durationMs;
        _slideAccelerate = accelerate;
        _slideStart = Stopwatch.GetTimestamp();
        _slideTimer.Stop();
        _slideTimer.Start();
    }

    private void SlideTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = (Stopwatch.GetTimestamp() - _slideStart) * 1000.0 / Stopwatch.Frequency;
        var t = Math.Min(1.0, elapsed / _slideDuration);
        var eased = _slideAccelerate ? t * t * t : 1 - Math.Pow(1 - t, 3);
        var x = (int)Math.Round(_slideFromX + (_slideToX - _slideFromX) * eased);

        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, _slideY, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

        if (t < 1.0) return;

        _slideTimer.Stop();
        _animating = false;

        if (_collapsed)
        {
            if (!_pinned && IsMouseOver) Expand();
        }
        else
        {
            var rect = WindowRect();
            _expandedPhysLeft = rect.Left;
            _expandedPhysTop = rect.Top;
            if (!_pinned && !IsMouseOver) ScheduleCollapse();
        }

        ScheduleSave();
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    private NativeMethods.RECT WindowRect()
    {
        NativeMethods.GetWindowRect(Handle, out var rect);
        return rect;
    }

    private NativeMethods.MONITORINFO MonitorInfo()
    {
        var monitor = NativeMethods.MonitorFromWindow(Handle, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        return info;
    }

    private double WindowDpiScale()
    {
        var dpi = NativeMethods.GetDpiForWindow(Handle);
        return dpi <= 0 ? 1.0 : dpi / 96.0;
    }

    private void ApplySavedPosition()
    {
        var raw = _store.GetSetting(PosKey);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var parts = raw.Split(';');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var x)
                && int.TryParse(parts[1], out var y)
                && IsOnAnyMonitor(x, y))
            {
                NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
                    NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
            }
        }

        var rect = WindowRect();
        _expandedPhysLeft = rect.Left;
        _expandedPhysTop = rect.Top;
    }

    private static bool IsOnAnyMonitor(int x, int y)
    {
        var rect = new NativeMethods.RECT { Left = x, Top = y, Right = x + 1, Bottom = y + 1 };
        return NativeMethods.MonitorFromRect(ref rect, NativeMethods.MonitorDefaultToNull) != IntPtr.Zero;
    }
}
