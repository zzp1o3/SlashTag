namespace SlashTag.Util;

public static class TimeText
{
    public static string TaskTimes(DateTime? start, DateTime? due)
    {
        if (start.HasValue && due.HasValue) return $"{start.Value:HH:mm}–{due.Value:HH:mm}";
        if (start.HasValue) return string.Format(Loc.Instance["StartsAt"], start.Value.ToString("HH:mm"));
        if (due.HasValue) return string.Format(Loc.Instance["DueAt"], due.Value.ToString("HH:mm"));
        return "";
    }

    public static string Duration(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalMinutes < 1) return Loc.Instance["DurationUnderMinute"];
        if (span.TotalHours < 1) return string.Format(Loc.Instance["DurationMinutes"], (int)span.TotalMinutes);
        if (span.TotalDays < 1) return string.Format(Loc.Instance["DurationHours"], (int)span.TotalHours, span.Minutes);
        return string.Format(Loc.Instance["DurationDays"], (int)span.TotalDays, span.Hours);
    }

    public static string Weekday(DateTime date) =>
        Loc.Instance.Language == UiLanguage.English
            ? date.DayOfWeek switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                _ => "Sun",
            }
            : "周" + "日一二三四五六"[(int)date.DayOfWeek];
}
