namespace CircleLight.Util;

public static class TimeText
{
    public static string TaskTimes(DateTime? start, DateTime? due)
    {
        if (start.HasValue && due.HasValue) return $"{start.Value:HH:mm}–{due.Value:HH:mm}";
        if (start.HasValue) return $"{start.Value:HH:mm} 开始";
        if (due.HasValue) return $"{due.Value:HH:mm} 截止";
        return "";
    }

    public static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalMinutes < 1) return "不到 1 分钟";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} 分钟";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} 小时 {span.Minutes} 分";
        return $"{(int)span.TotalDays} 天 {span.Hours} 小时";
    }

    public static string Weekday(DateTime date) =>
        "周" + "日一二三四五六"[(int)date.DayOfWeek];
}
