using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CircleLight.Data;
using CircleLight.Services;
using CircleLight.ViewModels;
using CircleLight.Views;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using CheckBox = System.Windows.Controls.CheckBox;

namespace CircleLight;

public partial class MainWindow : Window
{
    private const string BoundsKey = "window.bounds";

    private readonly TaskStore _store;
    private readonly MainViewModel _vm;
    private readonly EffectService _effects;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _dataWatcher;
    private long _lastDataVersion;
    private bool _loaded;
    private bool _exiting;

    public MainWindow(TaskStore store)
    {
        InitializeComponent();
        _store = store;
        _vm = new MainViewModel(store);
        DataContext = _vm;
        _effects = new EffectService(EffectLayer, Shell);

        _store.ResetDailyCounts(DateTime.Now);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveBounds();
        };
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();

        _vm.Reload(DateTime.Now);

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
        var dialog = new SettingsWindow(_store.DatabasePath) { Owner = this };
        dialog.ShowDialog();
    }

    public void ExitApp()
    {
        _exiting = true;
        _dataWatcher.Stop();
        SaveBounds();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowBounds();
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
        _store.SetSetting(BoundsKey,
            string.Join(';', Left.ToString("0.##", CultureInfo.InvariantCulture),
                Top.ToString("0.##", CultureInfo.InvariantCulture),
                Width.ToString("0.##", CultureInfo.InvariantCulture),
                Height.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private void RestoreWindowBounds()
    {
        Width = 340;
        Height = 560;

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
            _store.SetChecked(task.Id, false, DateTime.Now);
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

        var dialog = new TaskEditorWindow(_store, task) { Owner = this };
        if (dialog.ShowDialog() == true) ReloadLists();
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
        var dialog = new TaskEditorWindow(_store, initialTitle: QuickAddBox.Text.Trim()) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            var created = dialog.Created;
            QuickAddBox.Clear();
            HideQuickAdd();
            ReloadLists(scrollToTop: true);
            if (created) PlayAddForNewest();
        }
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
        var dialog = new HistoryWindow(_store) { Owner = this };
        dialog.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        SaveBounds();
        Hide();
    }
}
