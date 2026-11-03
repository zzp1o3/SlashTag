using System.ComponentModel;
using CircleLight.Models;
using Fmt = CircleLight.Util.TimeText;

namespace CircleLight.ViewModels;

public sealed class TaskViewModel : INotifyPropertyChanged
{
    private bool _isOverdue;
    private string _overdueText = "";

    public long Id { get; }
    public string Title { get; }
    public DateTime? StartAt { get; }
    public DateTime? DueAt { get; }
    public bool IsLongTerm { get; }
    public bool IsChecked { get; }
    public bool IsCompletedRow { get; }
    public int TargetCount { get; }
    public int DoneCount { get; }
    public bool IsCounted => TargetCount > 1;
    public string ProgressText => $"{DoneCount}/{TargetCount}";
    public double ProgressRatio => TargetCount > 1 ? Math.Clamp((double)DoneCount / TargetCount, 0, 1) : 0;
    public string TimeText { get; }
    public bool HasTimeText => TimeText.Length > 0;
    public bool ShowTag => IsLongTerm && !IsCompletedRow;
    public bool WasLate { get; }

    public bool IsOverdue
    {
        get => _isOverdue;
        private set
        {
            if (_isOverdue == value) return;
            _isOverdue = value;
            Raise(nameof(IsOverdue));
        }
    }

    public string OverdueText
    {
        get => _overdueText;
        private set
        {
            if (_overdueText == value) return;
            _overdueText = value;
            Raise(nameof(OverdueText));
            Raise(nameof(HasOverdueText));
        }
    }

    public bool HasOverdueText => _overdueText.Length > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private TaskViewModel(TaskRecord task)
    {
        Id = task.Id;
        Title = task.Title;
        StartAt = task.StartAt;
        DueAt = task.DueAt;
        IsLongTerm = task.IsLongTerm;
        IsChecked = task.IsChecked;
        TargetCount = task.TargetCount;
        DoneCount = task.DoneCount;
        TimeText = Fmt.TaskTimes(task.StartAt, task.DueAt);
    }

    private TaskViewModel(CompletionRecord completion)
    {
        Id = completion.TaskId;
        Title = completion.Title;
        IsLongTerm = completion.IsLongTerm;
        IsChecked = true;
        IsCompletedRow = true;
        TargetCount = 1;
        WasLate = completion.OverdueSeconds > 0;
        var late = completion.OverdueSeconds > 0
            ? $" · 超时 {Fmt.Duration(completion.OverdueSeconds)}"
            : "";
        TimeText = $"{completion.CompletedAt:HH:mm} 完成{late}";
    }

    public static TaskViewModel ForTask(TaskRecord task, DateTime now)
    {
        var vm = new TaskViewModel(task);
        vm.Refresh(now);
        return vm;
    }

    public static TaskViewModel ForCompletion(CompletionRecord completion) => new(completion);

    public void Refresh(DateTime now)
    {
        if (IsCompletedRow || IsChecked)
        {
            IsOverdue = false;
            OverdueText = "";
            return;
        }

        if (DueAt.HasValue && now > DueAt.Value)
        {
            var seconds = (long)Math.Round((now - DueAt.Value).TotalSeconds);
            IsOverdue = true;
            OverdueText = "! 超时 " + Fmt.Duration(seconds);
        }
        else
        {
            IsOverdue = false;
            OverdueText = "";
        }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
