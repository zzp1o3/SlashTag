using SlashTag.Models;
using SlashTag.Util;

namespace SlashTag.ViewModels;

public sealed class HistoryRowViewModel
{
    public string Title { get; init; } = "";
    public string Time { get; init; } = "";
    public string Overdue { get; init; } = "";
    public string Progress { get; init; } = "";
    public bool HasProgress { get; init; }
    public bool IsLate { get; init; }
    public bool IsLongTerm { get; init; }
}

public sealed class HistoryDayViewModel
{
    public string Header { get; init; } = "";
    public string Count { get; init; } = "";
    public IReadOnlyList<HistoryRowViewModel> Rows { get; init; } = Array.Empty<HistoryRowViewModel>();
}

public sealed class HistoryViewModel
{
    public IReadOnlyList<HistoryDayViewModel> Days { get; }
    public string TotalText { get; }
    public bool HasDays => Days.Count > 0;

    public HistoryViewModel(IReadOnlyList<CompletionRecord> records, int totalCompletions)
    {
        Days = records
            .GroupBy(r => r.CompletedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new HistoryDayViewModel
            {
                Header = $"{g.Key:yyyy-MM-dd} {TimeText.Weekday(g.Key)}",
                Count = $"{g.Count()} DONE",
                Rows = g.Select(r => new HistoryRowViewModel
                {
                    Title = r.Title,
                    Time = $"{r.CompletedAt:HH:mm}",
                    Overdue = r.OverdueSeconds > 0
                        ? $"超时 {TimeText.Duration(r.OverdueSeconds)}"
                        : "",
                    IsLate = r.OverdueSeconds > 0,
                    IsLongTerm = r.IsLongTerm,
                    Progress = r.ProgressText,
                    HasProgress = r.HasProgress,
                }).ToList(),
            })
            .ToList();

        TotalText = $"TOTAL {totalCompletions} · LV.{totalCompletions / 20 + 1}";
    }
}
