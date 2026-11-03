using System.Windows.Threading;
using CircleLight.Data;

namespace CircleLight.Services;

/// <summary>每 15 秒检查一次开始/截止提醒，并驱动界面上的超时文案刷新。</summary>
public sealed class ReminderService : IDisposable
{
    private readonly TaskStore _store;
    private readonly NotificationService _notifier;
    private readonly DispatcherTimer _timer;
    private DateOnly _lastDay;
    private int _lastMinute = -1;

    public event Action<DateTime>? MinuteTick;
    public event Action? DayChanged;

    public ReminderService(TaskStore store, NotificationService notifier)
    {
        _store = store;
        _notifier = notifier;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        var now = DateTime.Now;
        _lastDay = DateOnly.FromDateTime(now);
        _lastMinute = now.Minute;
        CatchUp(now);
        _timer.Start();
    }

    private void CatchUp(DateTime now)
    {
        var active = _store.GetActiveTasks().Where(t => !t.IsChecked).ToList();

        var overdue = active
            .Where(t => t.DueAt is { } due && due <= now && !t.OverdueNotified)
            .ToList();
        if (overdue.Count > 0)
        {
            _notifier.NotifyOverdueSummary(overdue, now);
            foreach (var task in overdue) _store.MarkOverdueNotified(task.Id);
        }

        foreach (var task in active) TryStartReminder(task, now);
    }

    private void Tick()
    {
        var now = DateTime.Now;

        var today = DateOnly.FromDateTime(now);
        if (today != _lastDay)
        {
            _lastDay = today;
            DayChanged?.Invoke();
        }

        if (now.Minute != _lastMinute)
        {
            _lastMinute = now.Minute;
            MinuteTick?.Invoke(now);
        }

        foreach (var task in _store.GetActiveTasks())
        {
            if (task.IsChecked) continue;

            TryStartReminder(task, now);

            if (task.DueAt is { } due && !task.OverdueNotified && now >= due)
            {
                _notifier.NotifyOverdue(task, now);
                _store.MarkOverdueNotified(task.Id);
            }
        }
    }

    private void TryStartReminder(Models.TaskRecord task, DateTime now)
    {
        if (task.StartAt is not { } start || task.StartNotified) return;
        if (now >= start.AddMinutes(-5) && now < start)
        {
            _notifier.NotifyStart(task, now);
            _store.MarkStartNotified(task.Id);
        }
    }

    public void Dispose() => _timer.Stop();
}
