using System.Globalization;
using System.IO;
using CircleLight.Models;
using Microsoft.Data.Sqlite;

namespace CircleLight.Data;

/// <summary>
/// 单文件 SQLite 存储。所有时间以本地时间 "yyyy-MM-dd HH:mm:ss" 文本保存，
/// 按天统计时可以直接用 date() 切分。
/// </summary>
public sealed class TaskStore : IDisposable
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly SqliteConnection _conn;

    public string DatabasePath { get; }

    public TaskStore(string databasePath)
    {
        DatabasePath = databasePath;
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={databasePath}");
        _conn.Open();
        Execute("PRAGMA busy_timeout = 5000;");
        Execute("PRAGMA foreign_keys = ON;");
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                created_at TEXT NOT NULL,
                start_at TEXT NULL,
                due_at TEXT NULL,
                is_long_term INTEGER NOT NULL DEFAULT 0,
                is_checked INTEGER NOT NULL DEFAULT 0,
                start_notified INTEGER NOT NULL DEFAULT 0,
                overdue_notified INTEGER NOT NULL DEFAULT 0,
                deleted_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS completions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id INTEGER NOT NULL REFERENCES tasks(id),
                completed_at TEXT NOT NULL,
                overdue_seconds INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_completions_completed_at ON completions(completed_at);

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        EnsureColumn("tasks", "target_count", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn("tasks", "done_count", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("tasks", "count_date", "TEXT NULL");
    }

    /// <summary>旧库补列：SQLite 的 CREATE TABLE IF NOT EXISTS 不会改已有表。</summary>
    private void EnsureColumn(string table, string column, string definition)
    {
        using (var check = _conn.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        Execute($"ALTER TABLE {table} ADD COLUMN {column} {definition};");
    }

    public long AddTask(string title, DateTime? startAt, DateTime? dueAt, bool isLongTerm, DateTime now, int targetCount = 1)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tasks (title, created_at, start_at, due_at, is_long_term, is_checked,
                               target_count, done_count, count_date)
            VALUES ($title, $created, $start, $due, $long, 0, $target, 0, $day);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$created", ToDb(now));
        cmd.Parameters.AddWithValue("$start", ToDb(startAt));
        cmd.Parameters.AddWithValue("$due", ToDb(dueAt));
        cmd.Parameters.AddWithValue("$long", isLongTerm ? 1 : 0);
        cmd.Parameters.AddWithValue("$target", targetCount);
        cmd.Parameters.AddWithValue("$day", Day(now));
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void UpdateTask(long id, string title, DateTime? startAt, DateTime? dueAt, bool isLongTerm, int targetCount)
    {
        var existing = GetTask(id);
        if (existing is null) return;

        var timesChanged = existing.StartAt != startAt || existing.DueAt != dueAt;
        var targetChanged = existing.TargetCount != targetCount;

        if (targetChanged)
        {
            // 目标次数变了，进度与当天的完成记录一起作废。
            using var del = _conn.CreateCommand();
            del.CommandText = "DELETE FROM completions WHERE task_id = $id AND date(completed_at) = $day;";
            del.Parameters.AddWithValue("$id", id);
            del.Parameters.AddWithValue("$day", Day(DateTime.Now));
            del.ExecuteNonQuery();
        }

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = targetChanged
            ? """
              UPDATE tasks
              SET title = $title, start_at = $start, due_at = $due, is_long_term = $long,
                  target_count = $target, done_count = 0, is_checked = 0,
                  start_notified = 0, overdue_notified = 0, count_date = $day
              WHERE id = $id AND deleted_at IS NULL;
              """
            : timesChanged
                ? """
                  UPDATE tasks
                  SET title = $title, start_at = $start, due_at = $due, is_long_term = $long,
                      target_count = $target,
                      start_notified = 0, overdue_notified = 0
                  WHERE id = $id AND deleted_at IS NULL;
                  """
                : """
                  UPDATE tasks
                  SET title = $title, start_at = $start, due_at = $due, is_long_term = $long,
                      target_count = $target
                  WHERE id = $id AND deleted_at IS NULL;
                  """;
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$start", ToDb(startAt));
        cmd.Parameters.AddWithValue("$due", ToDb(dueAt));
        cmd.Parameters.AddWithValue("$long", isLongTerm ? 1 : 0);
        cmd.Parameters.AddWithValue("$target", targetCount);
        cmd.Parameters.AddWithValue("$id", id);
        if (targetChanged)
            cmd.Parameters.AddWithValue("$day", Day(DateTime.Now));
        cmd.ExecuteNonQuery();
    }

    public void DeleteTask(long id, DateTime now)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE tasks SET deleted_at = $now WHERE id = $id;";
        cmd.Parameters.AddWithValue("$now", ToDb(now));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetChecked(long taskId, bool isChecked, DateTime now)
    {
        var task = GetTask(taskId);
        if (task is null) return;

        if (isChecked)
        {
            if (!HasCompletionOn(taskId, DateOnly.FromDateTime(now)))
            {
                var overdue = task.DueAt.HasValue && now > task.DueAt.Value
                    ? (long)Math.Round((now - task.DueAt.Value).TotalSeconds)
                    : 0L;
                InsertCompletion(taskId, now, overdue);
            }
        }
        else
        {
            // 长期待办取消勾选只是恢复「可以再做一次」，不回收当天的完成记录；
            // 普通任务取消勾选仍然撤销当天那条记录。
            if (!task.IsLongTerm)
            {
                using var del = _conn.CreateCommand();
                del.CommandText = "DELETE FROM completions WHERE task_id = $id AND date(completed_at) = $day;";
                del.Parameters.AddWithValue("$id", taskId);
                del.Parameters.AddWithValue("$day", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                del.ExecuteNonQuery();
            }
        }

        // 勾选后不再提醒；取消勾选后重新参与提醒。
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE tasks
            SET is_checked = $checked, start_notified = $notified, overdue_notified = $notified
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$checked", isChecked ? 1 : 0);
        cmd.Parameters.AddWithValue("$notified", isChecked ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", taskId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>跨天清空次数任务的进度与提醒标记；长期待办与普通任务不受影响。</summary>
    public void ResetDailyCounts(DateTime now)
    {
        var day = Day(now);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE tasks
            SET done_count = 0, is_checked = 0, start_notified = 0, overdue_notified = 0, count_date = $day
            WHERE deleted_at IS NULL AND target_count > 1
              AND (count_date IS NULL OR count_date <> $day);
            """;
        cmd.Parameters.AddWithValue("$day", day);
        cmd.ExecuteNonQuery();
    }

    /// <summary>次数任务 +1；达到目标时勾选并写入当天的完成记录。</summary>
    public TaskRecord? IncrementCount(long taskId, DateTime now)
    {
        var task = GetTask(taskId);
        if (task is null || !task.IsCounted) return task;

        var day = Day(now);
        var done = task.CountDate is { } date && Day(date) == day ? task.DoneCount : 0;
        done = Math.Min(done + 1, task.TargetCount);
        var reached = done >= task.TargetCount;

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE tasks
                SET done_count = $done, count_date = $day, is_checked = $checked,
                    start_notified = CASE WHEN $checked = 1 THEN 1 ELSE start_notified END,
                    overdue_notified = CASE WHEN $checked = 1 THEN 1 ELSE overdue_notified END
                WHERE id = $id AND deleted_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$done", done);
            cmd.Parameters.AddWithValue("$day", day);
            cmd.Parameters.AddWithValue("$checked", reached ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", taskId);
            cmd.ExecuteNonQuery();
        }

        if (reached && !HasCompletionOn(taskId, DateOnly.FromDateTime(now)))
        {
            var overdue = task.DueAt.HasValue && now > task.DueAt.Value
                ? (long)Math.Round((now - task.DueAt.Value).TotalSeconds)
                : 0L;
            InsertCompletion(taskId, now, overdue);
        }

        return GetTask(taskId);
    }

    /// <summary>次数任务取消勾选：进度清零，但保留当天的完成记录（不回收经验）。</summary>
    public TaskRecord? DecrementCount(long taskId, DateTime now)
    {
        var task = GetTask(taskId);
        if (task is null || !task.IsCounted) return task;

        var day = Day(now);
        var done = task.CountDate is { } date && Day(date) == day ? task.DoneCount : 0;
        var wasChecked = task.IsChecked;
        if (!wasChecked && done == 0) return task;

        // 已达标后取消勾选：直接清零重新开始；未达标时按减一处理。
        var newDone = wasChecked ? 0 : Math.Max(0, done - 1);

        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE tasks
                SET done_count = $done, is_checked = 0, start_notified = 0, overdue_notified = 0
                WHERE id = $id AND deleted_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$done", newDone);
            cmd.Parameters.AddWithValue("$id", taskId);
            cmd.ExecuteNonQuery();
        }

        return GetTask(taskId);
    }

    /// <summary>主列表：未完成的任务 + 长期待办 + 次数任务（即使已勾选也保留）。</summary>
    public List<TaskRecord> GetActiveTasks()
    {
        var list = new List<TaskRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, created_at, start_at, due_at, is_long_term, is_checked,
                   start_notified, overdue_notified, target_count, done_count, count_date
            FROM tasks
            WHERE deleted_at IS NULL AND (is_checked = 0 OR is_long_term = 1 OR target_count > 1)
            ORDER BY is_long_term ASC, is_checked ASC, id DESC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(ReadTask(reader));
        return list;
    }

    /// <summary>今日已完成的普通任务，用于折叠区。</summary>
    public List<CompletionRecord> GetTodayCleared(DateTime now)
    {
        var list = new List<CompletionRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.title, t.is_long_term, c.completed_at, c.overdue_seconds
            FROM completions c
            JOIN tasks t ON t.id = c.task_id
            WHERE t.deleted_at IS NULL
              AND t.is_long_term = 0
              AND t.target_count = 1
              AND date(c.completed_at) = $day
            ORDER BY c.completed_at DESC, c.id DESC;
            """;
        cmd.Parameters.AddWithValue("$day", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CompletionRecord
            {
                TaskId = reader.GetInt64(0),
                Title = reader.GetString(1),
                IsLongTerm = reader.GetInt32(2) != 0,
                CompletedAt = FromDb(reader.GetString(3)) ?? now,
                OverdueSeconds = reader.GetInt64(4),
            });
        }
        return list;
    }

    public List<CompletionRecord> GetHistory()
    {
        var list = new List<CompletionRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.id, t.title, t.is_long_term, c.completed_at, c.overdue_seconds
            FROM completions c
            JOIN tasks t ON t.id = c.task_id
            ORDER BY c.completed_at DESC, c.id DESC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CompletionRecord
            {
                TaskId = reader.GetInt64(0),
                Title = reader.GetString(1),
                IsLongTerm = reader.GetInt32(2) != 0,
                CompletedAt = FromDb(reader.GetString(3)) ?? DateTime.Now,
                OverdueSeconds = reader.GetInt64(4),
            });
        }
        return list;
    }

    public int CountCompletedToday(DateTime now)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM completions WHERE date(completed_at) = $day;";
        cmd.Parameters.AddWithValue("$day", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public int CountIncompleteActive()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM tasks WHERE deleted_at IS NULL AND is_checked = 0;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public int CountAllCompletions()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM completions;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void MarkStartNotified(long taskId)
    {
        Execute("UPDATE tasks SET start_notified = 1 WHERE id = $id;", ("$id", taskId));
    }

    public void MarkOverdueNotified(long taskId)
    {
        Execute("UPDATE tasks SET overdue_notified = 1 WHERE id = $id;", ("$id", taskId));
    }

    public string? GetSetting(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>别的连接提交写入后该值会变化；GUI 用来感知 MCP 服务端的外部修改。</summary>
    public long GetDataVersion()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA data_version;";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    public void Dispose() => _conn.Dispose();

    public TaskRecord? GetTask(long id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, created_at, start_at, due_at, is_long_term, is_checked,
                   start_notified, overdue_notified, target_count, done_count, count_date
            FROM tasks WHERE id = $id AND deleted_at IS NULL;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadTask(reader) : null;
    }

    private bool HasCompletionOn(long taskId, DateOnly day)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM completions WHERE task_id = $id AND date(completed_at) = $day;";
        cmd.Parameters.AddWithValue("$id", taskId);
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    private void InsertCompletion(long taskId, DateTime completedAt, long overdueSeconds)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO completions (task_id, completed_at, overdue_seconds)
            VALUES ($id, $completed, $overdue);
            """;
        cmd.Parameters.AddWithValue("$id", taskId);
        cmd.Parameters.AddWithValue("$completed", ToDb(completedAt));
        cmd.Parameters.AddWithValue("$overdue", overdueSeconds);
        cmd.ExecuteNonQuery();
    }

    private static TaskRecord ReadTask(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Title = reader.GetString(1),
        CreatedAt = FromDb(reader.GetString(2)) ?? DateTime.MinValue,
        StartAt = reader.IsDBNull(3) ? null : FromDb(reader.GetString(3)),
        DueAt = reader.IsDBNull(4) ? null : FromDb(reader.GetString(4)),
        IsLongTerm = reader.GetInt32(5) != 0,
        IsChecked = reader.GetInt32(6) != 0,
        StartNotified = reader.GetInt32(7) != 0,
        OverdueNotified = reader.GetInt32(8) != 0,
        TargetCount = reader.GetInt32(9),
        DoneCount = reader.GetInt32(10),
        CountDate = reader.IsDBNull(11) ? null : FromDay(reader.GetString(11)),
    };

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private static object ToDb(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString(TimeFormat, CultureInfo.InvariantCulture)
            : DBNull.Value;

    private static string Day(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime? FromDay(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static DateTime? FromDb(string? value) =>
        DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
