using SlashTag.Models;
using SlashTag.Util;
using CommunityToolkit.WinUI.Notifications;

namespace SlashTag.Services;

/// <summary>发送 Windows 原生通知；失败时退回应用内像素提示。</summary>
public sealed class NotificationService
{
    private readonly Action<string, string> _fallback;
    private readonly Action<string> _log;

    public NotificationService(Action<string, string> fallback, Action<string> log)
    {
        _fallback = fallback;
        _log = log;
    }

    public void NotifyStart(TaskRecord task, DateTime now)
    {
        var header = Loc.Instance["NotifyStart"];
        var body = $"{task.Title} · {task.StartAt:HH:mm}";
        Show(header, body);
    }

    public void NotifyOverdue(TaskRecord task, DateTime now)
    {
        var late = TimeText.Duration((long)Math.Round((now - task.DueAt!.Value).TotalSeconds));
        Show(string.Format(Loc.Instance["NotifyOverdue"], late), task.Title);
    }

    public void NotifyOverdueSummary(IReadOnlyList<TaskRecord> tasks, DateTime now)
    {
        var header = string.Format(Loc.Instance["NotifySummary"], tasks.Count);
        var body = string.Join(Loc.Instance["ListSeparator"], tasks.Take(3).Select(t => t.Title));
        if (tasks.Count > 3) body += " " + string.Format(Loc.Instance["NotifyMore"], tasks.Count);

        try
        {
            var toast = new ToastContentBuilder().AddText(header);
            foreach (var task in tasks.Take(3)) toast.AddText(task.Title);
            if (tasks.Count > 3) toast.AddText(string.Format(Loc.Instance["NotifyMore"], tasks.Count));
            toast.Show();
        }
        catch (Exception ex)
        {
            _log("toast summary failed: " + ex.Message);
            _fallback(header, body);
        }
    }

    private void Show(string header, string body)
    {
        try
        {
            new ToastContentBuilder().AddText(header).AddText(body).Show();
        }
        catch (Exception ex)
        {
            _log("toast failed: " + ex.Message);
            _fallback(header, body);
        }
    }
}
