using System.Globalization;

namespace CircleLight.Util;

/// <summary>把用户输入的时间文本解析为本地时间。留空表示不设置。</summary>
public static class UserTime
{
    private static readonly string[] TimeOnlyFormats = { "H:mm", "HH:mm" };
    private static readonly string[] FullFormats =
    {
        "yyyy-MM-dd H:mm", "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss",
        "yyyy/M/d H:mm", "M/d H:mm",
    };

    public static bool TryParse(string? text, DateTime now, out DateTime? value, out string error)
    {
        value = null;
        error = "";
        text = (text ?? "").Trim();
        if (text.Length == 0) return true;

        if (DateTime.TryParseExact(text, TimeOnlyFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            value = now.Date.Add(time.TimeOfDay);
            return true;
        }

        if (DateTime.TryParseExact(text, FullFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            value = TrimSeconds(exact);
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        {
            value = TrimSeconds(parsed);
            return true;
        }

        error = "无法识别，请用 19:30 或 2026-11-03 19:30。";
        return false;
    }

    private static DateTime TrimSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
}
