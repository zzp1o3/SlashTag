using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Threading;
using SlashTag.Data;
using SlashTag.Models;
using SlashTag.Util;

namespace SlashTag.Views;

public partial class TaskEditorPage : UserControl
{
    private TaskStore? _store;
    private TaskRecord? _task;

    public event Action<bool>? Saved;
    public event Action? Cancelled;

    public bool IsEditing => _task is not null;

    public TaskEditorPage() => InitializeComponent();

    public void Initialize(TaskStore store, TaskRecord? task, string? initialTitle)
    {
        _store = store;
        _task = task;
        ErrorText.Visibility = Visibility.Collapsed;

        if (task is not null)
        {
            TitleBox.Text = task.Title;
            StartBox.Text = task.StartAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            DueBox.Text = task.DueAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            LongTermBox.IsChecked = task.IsLongTerm;
            CountBox.Text = task.TargetCount.ToString();
        }
        else
        {
            TitleBox.Text = initialTitle ?? "";
            StartBox.Text = "";
            DueBox.Text = "";
            LongTermBox.IsChecked = false;
            CountBox.Text = "1";
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;

        var title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            ShowError("任务标题不能为空。");
            return;
        }

        if (!TryParseTime(StartBox.Text, out var start, out var startError))
        {
            ShowError("开始时间：" + startError);
            return;
        }

        if (!TryParseTime(DueBox.Text, out var due, out var dueError))
        {
            ShowError("截止时间：" + dueError);
            return;
        }

        if (start.HasValue && due.HasValue && start.Value >= due.Value)
        {
            ShowError("开始时间必须早于截止时间。");
            return;
        }

        if (!int.TryParse(CountBox.Text.Trim(), out var targetCount) || targetCount < 1 || targetCount > 99)
        {
            ShowError("目标次数需要在 1-99 之间。");
            return;
        }

        var created = _task is null;
        if (created)
        {
            _store.AddTask(title, start, due, LongTermBox.IsChecked == true, DateTime.Now, targetCount);
        }
        else
        {
            _store.UpdateTask(_task!.Id, title, start, due, LongTermBox.IsChecked == true, targetCount);
        }

        Saved?.Invoke(created);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke();

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool TryParseTime(string text, out DateTime? value, out string error) =>
        UserTime.TryParse(text, DateTime.Now, out value, out error);
}
