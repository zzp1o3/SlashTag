namespace SlashTag.Models;

public sealed class TaskRecord
{
    public long Id { get; init; }
    public string Title { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? DueAt { get; init; }
    public bool IsLongTerm { get; init; }
    public bool IsChecked { get; init; }
    public bool StartNotified { get; init; }
    public bool OverdueNotified { get; init; }
    public int TargetCount { get; init; } = 1;
    public int DoneCount { get; init; }
    public DateTime? CountDate { get; init; }

    /// <summary>次数任务：每天有目标次数，达标才记完成，次日清零。</summary>
    public bool IsCounted => TargetCount > 1;
}

public sealed class CompletionRecord
{
    public long TaskId { get; init; }
    public string Title { get; init; } = "";
    public DateTime CompletedAt { get; init; }
    public long OverdueSeconds { get; init; }
    public bool IsLongTerm { get; init; }
    public int ProgressDone { get; init; }
    public int ProgressTarget { get; init; }

    public bool HasProgress => ProgressTarget > 1;
    public string ProgressText => HasProgress ? $"{ProgressDone}/{ProgressTarget}" : "";
}
