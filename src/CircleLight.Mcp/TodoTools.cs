using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CircleLight.Data;
using CircleLight.Models;
using CircleLight.Util;
using ModelContextProtocol.Server;

namespace CircleLight.Mcp;

[McpServerToolType]
public sealed class TodoTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TaskStore _store;
    private readonly object _gate = new();

    public TodoTools(TaskStore store) => _store = store;

    [McpServerTool(Name = "list_tasks")]
    [Description("列出当前待办：未完成任务、长期待办（含已勾选）以及今天已完成的任务，并返回今日进度与等级。")]
    public string ListTasks()
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var tasks = _store.GetActiveTasks();
            var cleared = _store.GetTodayCleared(now);
            var done = _store.CountCompletedToday(now);
            var incomplete = _store.CountIncompleteActive();

            return Json(new
            {
                now = now.ToString("yyyy-MM-dd HH:mm:ss"),
                today = new
                {
                    done,
                    total = done + incomplete,
                    level = _store.CountAllCompletions() / 20 + 1,
                },
                tasks = tasks.Select(t => TaskDto(t, now)).ToList(),
                cleared_today = cleared.Select(CompletionDto).ToList(),
            });
        });
    }

    [McpServerTool(Name = "get_task")]
    [Description("按 id 读取一条任务。")]
    public string GetTask([Description("任务 id")] long id)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");
            return Json(new { ok = true, task = TaskDto(task, now) });
        });
    }

    [McpServerTool(Name = "create_task")]
    [Description("新建任务。start_at 与 due_at 可留空；支持 19:30（今天）或 2026-11-03 19:30。long_term=true 表示完成后保留。")]
    public string CreateTask(
        [Description("任务标题，1-500 字")] string title,
        [Description("开始时间，可选；提前 5 分钟提醒")] string? start_at = null,
        [Description("截止时间，可选；到点未完成会提醒并记录超时")] string? due_at = null,
        [Description("是否长期待办（完成后保留在列表）")] bool long_term = false)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var cleanTitle = ValidateTitle(title);

            if (!UserTime.TryParse(start_at, now, out var start, out var startError))
                throw new ArgumentException("开始时间：" + startError);
            if (!UserTime.TryParse(due_at, now, out var due, out var dueError))
                throw new ArgumentException("截止时间：" + dueError);
            ValidateSchedule(start, due);

            var id = _store.AddTask(cleanTitle, start, due, long_term, now);
            var task = _store.GetTask(id) ?? throw new InvalidOperationException("任务创建后无法读取。");

            return Json(new { ok = true, task = TaskDto(task, now) });
        });
    }

    [McpServerTool(Name = "update_task")]
    [Description("修改任务的标题或长期标记。只传需要修改的字段；时间请用 set_task_schedule。")]
    public string UpdateTask(
        [Description("任务 id")] long id,
        [Description("新标题；不传保持不变")] string? title = null,
        [Description("是否长期待办；不传保持不变")] bool? long_term = null)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");

            var newTitle = title is null ? task.Title : ValidateTitle(title);
            var newLongTerm = long_term ?? task.IsLongTerm;

            _store.UpdateTask(id, newTitle, task.StartAt, task.DueAt, newLongTerm);
            var updated = _store.GetTask(id) ?? throw new InvalidOperationException("任务更新后无法读取。");

            return Json(new { ok = true, task = TaskDto(updated, now) });
        });
    }

    [McpServerTool(Name = "set_task_schedule")]
    [Description("设置任务的开始/截止时间。传空字符串清除该项；两项都传空字符串即取消所有时间。")]
    public string SetTaskSchedule(
        [Description("任务 id")] long id,
        [Description("开始时间；空字符串清除")] string start_at,
        [Description("截止时间；空字符串清除")] string due_at)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");

            if (!UserTime.TryParse(start_at, now, out var start, out var startError))
                throw new ArgumentException("开始时间：" + startError);
            if (!UserTime.TryParse(due_at, now, out var due, out var dueError))
                throw new ArgumentException("截止时间：" + dueError);
            ValidateSchedule(start, due);

            _store.UpdateTask(id, task.Title, start, due, task.IsLongTerm);
            var updated = _store.GetTask(id) ?? throw new InvalidOperationException("任务更新后无法读取。");

            return Json(new { ok = true, task = TaskDto(updated, now) });
        });
    }

    [McpServerTool(Name = "complete_task")]
    [Description("勾选完成任务。普通任务进入今日已完成；长期待办保留在列表。已勾选的任务不会重复记录。")]
    public string CompleteTask([Description("任务 id")] long id)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");

            if (task.IsChecked)
                return Json(new { ok = true, already_completed = true, task = TaskDto(task, now) });

            var overdueSeconds = task.DueAt.HasValue && now > task.DueAt.Value
                ? (long)Math.Round((now - task.DueAt.Value).TotalSeconds)
                : 0L;

            _store.SetChecked(id, true, now);
            var updated = _store.GetTask(id) ?? throw new InvalidOperationException("任务勾选后无法读取。");

            return Json(new
            {
                ok = true,
                completed_at = now.ToString("yyyy-MM-dd HH:mm:ss"),
                recorded_overdue_seconds = overdueSeconds,
                task = TaskDto(updated, now),
            });
        });
    }

    [McpServerTool(Name = "uncomplete_task")]
    [Description("撤销任务的勾选。会删除今天的完成记录；更早的历史记录不受影响。")]
    public string UncompleteTask([Description("任务 id")] long id)
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");

            if (!task.IsChecked)
                return Json(new { ok = true, already_pending = true, task = TaskDto(task, now) });

            _store.SetChecked(id, false, now);
            var updated = _store.GetTask(id) ?? throw new InvalidOperationException("任务取消勾选后无法读取。");

            return Json(new { ok = true, task = TaskDto(updated, now) });
        });
    }

    [McpServerTool(Name = "delete_task")]
    [Description("从便签删除任务（软删除）。已有的完成记录与历史统计会保留。")]
    public string DeleteTask([Description("任务 id")] long id)
    {
        return Guarded(() =>
        {
            var task = _store.GetTask(id) ?? throw new ArgumentException($"任务 {id} 不存在或已删除。");
            _store.DeleteTask(id, DateTime.Now);
            return Json(new { ok = true, deleted_id = id, title = task.Title });
        });
    }

    [McpServerTool(Name = "list_completed")]
    [Description("列出某一天完成的任务，默认今天。包含完成时间、当时的超时时长与是否长期待办。")]
    public string ListCompleted([Description("日期 yyyy-MM-dd；默认今天")] string? date = null)
    {
        return Guarded(() =>
        {
            var target = ParseDay(date);
            var items = _store.GetHistory()
                .Where(c => DateOnly.FromDateTime(c.CompletedAt) == target)
                .ToList();

            return Json(new
            {
                date = target.ToString("yyyy-MM-dd"),
                count = items.Count,
                items = items.Select(CompletionDto).ToList(),
            });
        });
    }

    [McpServerTool(Name = "get_history")]
    [Description("按天返回最近 N 天的完成记录，包含每天完成的任务、完成时间与超时时长。")]
    public string GetHistory([Description("最近天数，1-365，默认 7")] int days = 7)
    {
        return Guarded(() =>
        {
            days = Math.Clamp(days, 1, 365);
            var to = DateOnly.FromDateTime(DateTime.Now);
            var from = to.AddDays(1 - days);

            var groups = _store.GetHistory()
                .Where(c =>
                {
                    var day = DateOnly.FromDateTime(c.CompletedAt);
                    return day >= from && day <= to;
                })
                .GroupBy(c => DateOnly.FromDateTime(c.CompletedAt))
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    weekday = TimeText.Weekday(g.Key.ToDateTime(TimeOnly.MinValue)),
                    count = g.Count(),
                    items = g.Select(CompletionDto).ToList(),
                })
                .ToList();

            return Json(new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd"),
                days = groups,
            });
        });
    }

    [McpServerTool(Name = "get_overview")]
    [Description("查看今日进度、等级、未完成数量、逾期数量与累计完成数。")]
    public string GetOverview()
    {
        return Guarded(() =>
        {
            var now = DateTime.Now;
            var done = _store.CountCompletedToday(now);
            var incomplete = _store.CountIncompleteActive();
            var totalCompletions = _store.CountAllCompletions();
            var active = _store.GetActiveTasks();
            var overdue = active.Count(t =>
                !t.IsChecked && t.DueAt.HasValue && now > t.DueAt.Value);

            return Json(new
            {
                now = now.ToString("yyyy-MM-dd HH:mm:ss"),
                today_done = done,
                today_total = done + incomplete,
                level = totalCompletions / 20 + 1,
                active_count = active.Count,
                overdue_count = overdue,
                total_completions = totalCompletions,
            });
        });
    }

    private static object TaskDto(TaskRecord task, DateTime now)
    {
        var overdue = !task.IsChecked && task.DueAt.HasValue && now > task.DueAt.Value;
        var overdueSeconds = overdue ? (long)Math.Round((now - task.DueAt!.Value).TotalSeconds) : (long?)null;

        return new
        {
            id = task.Id,
            title = task.Title,
            start_at = Fmt(task.StartAt),
            due_at = Fmt(task.DueAt),
            long_term = task.IsLongTerm,
            completed = task.IsChecked,
            status = task.IsChecked ? "completed" : overdue ? "overdue" : "pending",
            overdue_seconds = overdueSeconds,
            time_text = TimeText.TaskTimes(task.StartAt, task.DueAt),
            overdue_text = overdueSeconds is null
                ? null
                : "超时 " + TimeText.Duration(overdueSeconds.Value),
        };
    }

    private static object CompletionDto(CompletionRecord completion) => new
    {
        task_id = completion.TaskId,
        title = completion.Title,
        completed_at = completion.CompletedAt.ToString("yyyy-MM-dd HH:mm"),
        overdue_seconds = completion.OverdueSeconds,
        long_term = completion.IsLongTerm,
    };

    private static string ValidateTitle(string? title)
    {
        var clean = (title ?? "").Trim();
        if (clean.Length == 0) throw new ArgumentException("任务标题不能为空。");
        if (clean.Length > 500) throw new ArgumentException("任务标题不能超过 500 个字符。");
        return clean;
    }

    private static void ValidateSchedule(DateTime? start, DateTime? due)
    {
        if (start.HasValue && due.HasValue && start.Value >= due.Value)
            throw new ArgumentException("开始时间必须早于截止时间。");
    }

    private static DateOnly ParseDay(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return DateOnly.FromDateTime(DateTime.Now);
        var text = date.Trim();

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
            return exact;

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
            return DateOnly.FromDateTime(parsed);

        throw new ArgumentException("日期格式无法识别，请用 yyyy-MM-dd。");
    }

    private static string? Fmt(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm");

    private string Guarded(Func<string> action)
    {
        lock (_gate)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);
}
