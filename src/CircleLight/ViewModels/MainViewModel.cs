using System.Collections.ObjectModel;
using System.ComponentModel;
using CircleLight.Data;

namespace CircleLight.ViewModels;

public sealed class XpCell
{
    public bool On { get; init; }
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly TaskStore _store;
    private bool _clearedExpanded;
    private bool _hasActiveTasks;
    private bool _hasCleared;
    private bool _showClearedList;
    private bool _isAlert;
    private string _dayText = "";
    private string _xpText = "0/0";
    private string _clearedHeader = "▸ CLEARED 0";
    private string _alertText = "▶ READY";

    public ObservableCollection<TaskViewModel> ActiveTasks { get; } = new();
    public ObservableCollection<TaskViewModel> ClearedTasks { get; } = new();
    public ObservableCollection<XpCell> XpCells { get; } = new();

    public string DayText { get => _dayText; private set => Set(ref _dayText, value); }
    public string XpText { get => _xpText; private set => Set(ref _xpText, value); }
    public string ClearedHeader { get => _clearedHeader; private set => Set(ref _clearedHeader, value); }
    public string AlertText { get => _alertText; private set => Set(ref _alertText, value); }
    public bool HasActiveTasks { get => _hasActiveTasks; private set => Set(ref _hasActiveTasks, value); }
    public bool HasCleared { get => _hasCleared; private set => Set(ref _hasCleared, value); }
    public bool ShowClearedList { get => _showClearedList; private set => Set(ref _showClearedList, value); }
    public bool IsAlert { get => _isAlert; private set => Set(ref _isAlert, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(TaskStore store) => _store = store;

    public void Reload(DateTime now)
    {
        ActiveTasks.Clear();
        foreach (var task in _store.GetActiveTasks())
            ActiveTasks.Add(TaskViewModel.ForTask(task, now));

        ClearedTasks.Clear();
        foreach (var completion in _store.GetTodayCleared(now))
            ClearedTasks.Add(TaskViewModel.ForCompletion(completion));

        var done = _store.CountCompletedToday(now);
        var incomplete = _store.CountIncompleteActive();
        var total = done + incomplete;
        var level = _store.CountAllCompletions() / 20 + 1;

        DayText = $"DAY {now.DayOfYear:000} · {now:MM/dd} · LV.{level}";
        XpText = $"{done}/{total}";

        XpCells.Clear();
        var filled = total == 0 ? 0 : (int)Math.Round(done * 10.0 / total);
        for (var i = 0; i < 10; i++) XpCells.Add(new XpCell { On = i < filled });

        HasActiveTasks = ActiveTasks.Count > 0;
        HasCleared = ClearedTasks.Count > 0;
        ClearedHeader = $"{(_clearedExpanded ? "▾" : "▸")} CLEARED {ClearedTasks.Count}";
        ShowClearedList = HasCleared && _clearedExpanded;
        RefreshAlert();
    }

    public void RefreshTimes(DateTime now)
    {
        foreach (var task in ActiveTasks) task.Refresh(now);
        RefreshAlert();
    }

    public void ToggleCleared()
    {
        _clearedExpanded = !_clearedExpanded;
        ClearedHeader = $"{(_clearedExpanded ? "▾" : "▸")} CLEARED {ClearedTasks.Count}";
        ShowClearedList = HasCleared && _clearedExpanded;
    }

    private void RefreshAlert()
    {
        var count = ActiveTasks.Count(t => t.IsOverdue);
        IsAlert = count > 0;
        AlertText = count > 0 ? $"! ALERT {count}" : "▶ READY";
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
