using System.ComponentModel;

namespace SlashTag.Util;

public enum UiLanguage
{
    Chinese,
    English,
}

/// <summary>界面文案。切换语言时通过索引器通知所有绑定刷新。</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private UiLanguage _language = UiLanguage.Chinese;

    public UiLanguage Language => _language;

    public string this[string key] =>
        (_language == UiLanguage.English ? English : Chinese).TryGetValue(key, out var value) ? value : key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Set(UiLanguage language)
    {
        if (_language == language) return;
        _language = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public static UiLanguage Parse(string? value) =>
        string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? UiLanguage.English : UiLanguage.Chinese;

    public static string Serialize(UiLanguage language) =>
        language == UiLanguage.English ? "en" : "zh";

    private static readonly Dictionary<string, string> Chinese = new()
    {
        ["QuestLog"] = "任务清单",
        ["Today"] = "今日",
        ["NoQuests"] = "没有任务 · 按右上角 + 新建",
        ["AddQuest"] = "[ + 新任务 ]",
        ["QuickAddPlaceholder"] = "输入任务标题，回车添加",
        ["NewTaskTooltip"] = "新建任务",
        ["SettingsTooltip"] = "设置",
        ["HideTooltip"] = "收进托盘",
        ["BackTooltip"] = "返回",
        ["FullEditTooltip"] = "完整编辑（时间 / 次数）",
        ["LongTag"] = "★ 长期",
        ["History"] = "历史",
        ["HistoryTitle"] = "历史",
        ["SettingsTitle"] = "设置",
        ["NewQuestTitle"] = "新任务",
        ["EditQuestTitle"] = "编辑任务",
        ["Cleared"] = "已完成 {0}",
        ["Ready"] = "▶ 就绪",
        ["Alert"] = "! 超时 {0}",
        ["DayText"] = "第 {0} 天 · {1} · LV.{2}",
        ["StartsAt"] = "{0} 开始",
        ["DueAt"] = "{0} 截止",
        ["Overdue"] = "! 超时 {0}",
        ["CompletedAt"] = "{0} 完成",
        ["CompletedLate"] = "{0} 完成 · 超时 {1}",
        ["DurationUnderMinute"] = "不到 1 分钟",
        ["DurationMinutes"] = "{0} 分钟",
        ["DurationHours"] = "{0} 小时 {1} 分",
        ["DurationDays"] = "{0} 天 {1} 小时",
        ["DayDone"] = "{0} 条完成",
        ["TotalLevel"] = "累计 {0} · LV.{1}",
        ["NoRecords"] = "暂无记录",
        ["StartupLabel"] = "启动",
        ["Autostart"] = "开机时自动启动 SlashTag",
        ["DataFileLabel"] = "数据文件",
        ["OpenDataFolder"] = "打开数据目录",
        ["DataNote"] = "任务存在单个 SQLite 文件里，备份就是复制它。",
        ["LanguageLabel"] = "语言",
        ["AutostartError"] = "写入开机启动项失败，请检查权限。",
        ["OpenFolderError"] = "打开目录失败：",
        ["TitleLabel"] = "任务标题",
        ["StartLabel"] = "开始时间（可选）",
        ["StartHint"] = "留空不提醒；可填 19:30 或 2026-11-03 19:30。",
        ["DueLabel"] = "截止时间（可选）",
        ["DueHint"] = "到点未勾选会提醒一次；完成时记录超时时长。",
        ["LongTermOption"] = "长期待办 · 每天 0 点自动取消勾选",
        ["CountLabel"] = "目标次数（可选）",
        ["CountHint"] = "1 = 普通任务；大于 1 为每日次数任务。",
        ["Cancel"] = "取消",
        ["Save"] = "保存",
        ["TitleRequired"] = "任务标题不能为空。",
        ["StartBeforeDue"] = "开始时间必须早于截止时间。",
        ["CountRange"] = "目标次数需要在 1-99 之间。",
        ["InvalidTime"] = "时间格式无法识别，请用 19:30 或 2026-11-03 19:30。",
        ["TrayText"] = "SlashTag 待办",
        ["TrayOpen"] = "打开便签",
        ["TrayAdd"] = "新建任务",
        ["TraySettings"] = "设置",
        ["TrayExit"] = "退出",
        ["NotifyStart"] = "5 分钟后开始",
        ["NotifyOverdue"] = "已超时 {0}",
        ["NotifySummary"] = "{0} 个任务已超时",
        ["NotifyMore"] = "等 {0} 个任务",
        ["ListSeparator"] = "、",
        ["ToastDue"] = "! 任务到期",
        ["EditTooltip"] = "双击编辑",
        ["CheckTooltip"] = "勾选完成",
        ["UndoTooltip"] = "撤销完成",
        ["DeleteTooltip"] = "删除",
        ["PinOffTip"] = "固定展示（不自动贴边）",
        ["PinOnTip"] = "取消固定（自动贴边）",
        ["EdgeAnimTitle"] = "收起动画",
        ["EdgeAnimPool"] = "随机池",
        ["EdgeAnimCollapse"] = "像素坍缩",
        ["EdgeAnimGlitch"] = "故障",
        ["EdgeAnimFade"] = "淡入淡出",
        ["EdgeAnimRandom"] = "随机",
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["QuestLog"] = "QUEST LOG",
        ["Today"] = "TODAY",
        ["NoQuests"] = "NO QUESTS · use + at the top right",
        ["AddQuest"] = "[ + NEW QUEST ]",
        ["QuickAddPlaceholder"] = "Type a task, press Enter",
        ["NewTaskTooltip"] = "New task",
        ["SettingsTooltip"] = "Settings",
        ["HideTooltip"] = "Hide to tray",
        ["BackTooltip"] = "Back",
        ["FullEditTooltip"] = "Full editor (time / count)",
        ["LongTag"] = "★ LONG",
        ["History"] = "HISTORY",
        ["HistoryTitle"] = "HISTORY",
        ["SettingsTitle"] = "SETTINGS",
        ["NewQuestTitle"] = "NEW QUEST",
        ["EditQuestTitle"] = "EDIT QUEST",
        ["Cleared"] = "CLEARED {0}",
        ["Ready"] = "▶ READY",
        ["Alert"] = "! ALERT {0}",
        ["DayText"] = "DAY {0} · {1} · LV.{2}",
        ["StartsAt"] = "starts {0}",
        ["DueAt"] = "due {0}",
        ["Overdue"] = "! late {0}",
        ["CompletedAt"] = "{0} done",
        ["CompletedLate"] = "{0} done · late {1}",
        ["DurationUnderMinute"] = "<1 min",
        ["DurationMinutes"] = "{0} min",
        ["DurationHours"] = "{0} h {1} min",
        ["DurationDays"] = "{0} d {1} h",
        ["DayDone"] = "{0} DONE",
        ["TotalLevel"] = "TOTAL {0} · LV.{1}",
        ["NoRecords"] = "NO RECORDS YET",
        ["StartupLabel"] = "Startup",
        ["Autostart"] = "Start SlashTag with Windows",
        ["DataFileLabel"] = "Data file",
        ["OpenDataFolder"] = "Open data folder",
        ["DataNote"] = "Tasks live in one SQLite file — copy it to back up.",
        ["LanguageLabel"] = "Language",
        ["AutostartError"] = "Could not write the startup entry. Check permissions.",
        ["OpenFolderError"] = "Could not open the folder: ",
        ["TitleLabel"] = "Title",
        ["StartLabel"] = "Start time (optional)",
        ["StartHint"] = "Empty = no reminder. Try 19:30 or 2026-11-03 19:30.",
        ["DueLabel"] = "Due time (optional)",
        ["DueHint"] = "Reminds once when due; records how late you finish.",
        ["LongTermOption"] = "Long-term · resets at midnight",
        ["CountLabel"] = "Target count (optional)",
        ["CountHint"] = "1 = normal task; above 1 = daily counted task.",
        ["Cancel"] = "Cancel",
        ["Save"] = "Save",
        ["TitleRequired"] = "Title is required.",
        ["StartBeforeDue"] = "Start time must be before due time.",
        ["CountRange"] = "Target count must be 1-99.",
        ["InvalidTime"] = "Invalid time. Use 19:30 or 2026-11-03 19:30.",
        ["TrayText"] = "SlashTag todos",
        ["TrayOpen"] = "Open note",
        ["TrayAdd"] = "New task",
        ["TraySettings"] = "Settings",
        ["TrayExit"] = "Exit",
        ["NotifyStart"] = "Starts in 5 minutes",
        ["NotifyOverdue"] = "Overdue {0}",
        ["NotifySummary"] = "{0} tasks are overdue",
        ["NotifyMore"] = "and {0} more",
        ["ListSeparator"] = ", ",
        ["ToastDue"] = "! QUEST DUE",
        ["EditTooltip"] = "Double-click to edit",
        ["CheckTooltip"] = "Complete",
        ["UndoTooltip"] = "Undo",
        ["DeleteTooltip"] = "Delete",
        ["PinOffTip"] = "Keep open (no auto-hide)",
        ["PinOnTip"] = "Unpin (auto-hide)",
        ["EdgeAnimTitle"] = "Edge animation",
        ["EdgeAnimPool"] = "Random pool",
        ["EdgeAnimCollapse"] = "Pixel collapse",
        ["EdgeAnimGlitch"] = "Glitch",
        ["EdgeAnimFade"] = "Fade",
        ["EdgeAnimRandom"] = "Random",
    };
}
