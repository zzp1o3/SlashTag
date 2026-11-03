namespace CircleLight.Models;

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
}

public sealed class CompletionRecord
{
    public long TaskId { get; init; }
    public string Title { get; init; } = "";
    public DateTime CompletedAt { get; init; }
    public long OverdueSeconds { get; init; }
    public bool IsLongTerm { get; init; }
}
