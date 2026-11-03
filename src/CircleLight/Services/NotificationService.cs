using CircleLight.Models;
using CircleLight.Util;
using CommunityToolkit.WinUI.Notifications;

namespace CircleLight.Services;

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
        var header = "5 分钟后开始";
        var body = $"{task.Title} · {task.StartAt:HH:mm}";
        Show(header, body);
    }

    public void NotifyOverdue(TaskRecord task, DateTime now)
    {
        var late = TimeText.Duration((long)Math.Round((now - task.DueAt!.Value).TotalSeconds));
        Show($"已超时 {late}", task.Title);
    }

    public void NotifyOverdueSummary(IReadOnlyList<TaskRecord> tasks, DateTime now)
    {
        var header = $"{tasks.Count} 个任务已超时";
        var body = string.Join("、", tasks.Take(3).Select(t => t.Title));
        if (tasks.Count > 3) body += $" 等 {tasks.Count} 项";

        try
        {
            var toast = new ToastContentBuilder().AddText(header);
            foreach (var task in tasks.Take(3)) toast.AddText(task.Title);
            if (tasks.Count > 3) toast.AddText($"等 {tasks.Count} 个任务");
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
