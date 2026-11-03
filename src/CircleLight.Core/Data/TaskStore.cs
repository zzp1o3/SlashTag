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
    }

    public long AddTask(string title, DateTime? startAt, DateTime? dueAt, bool isLongTerm, DateTime now)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tasks (title, created_at, start_at, due_at, is_long_term, is_checked)
            VALUES ($title, $created, $start, $due, $long, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$created", ToDb(now));
        cmd.Parameters.AddWithValue("$start", ToDb(startAt));
        cmd.Parameters.AddWithValue("$due", ToDb(dueAt));
        cmd.Parameters.AddWithValue("$long", isLongTerm ? 1 : 0);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    public void UpdateTask(long id, string title, DateTime? startAt, DateTime? dueAt, bool isLongTerm)
    {
        var existing = GetTask(id);
        if (existing is null) return;

        var timesChanged = existing.StartAt != startAt || existing.DueAt != dueAt;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = timesChanged
            ? """
              UPDATE tasks
              SET title = $title, start_at = $start, due_at = $due, is_long_term = $long,
                  start_notified = 0, overdue_notified = 0
              WHERE id = $id AND deleted_at IS NULL;
              """
            : """
              UPDATE tasks
              SET title = $title, start_at = $start, due_at = $due, is_long_term = $long
              WHERE id = $id AND deleted_at IS NULL;
              """;
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$start", ToDb(startAt));
        cmd.Parameters.AddWithValue("$due", ToDb(dueAt));
        cmd.Parameters.AddWithValue("$long", isLongTerm ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
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
            using var del = _conn.CreateCommand();
            del.CommandText = "DELETE FROM completions WHERE task_id = $id AND date(completed_at) = $day;";
            del.Parameters.AddWithValue("$id", taskId);
            del.Parameters.AddWithValue("$day", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            del.ExecuteNonQuery();
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

    /// <summary>主列表：未完成的任务 + 长期待办（即使已勾选也保留）。</summary>
    public List<TaskRecord> GetActiveTasks()
    {
        var list = new List<TaskRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, created_at, start_at, due_at, is_long_term, is_checked,
                   start_notified, overdue_notified
            FROM tasks
            WHERE deleted_at IS NULL AND (is_checked = 0 OR is_long_term = 1)
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
                   start_notified, overdue_notified
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

    private static DateTime? FromDb(string? value) =>
        DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
