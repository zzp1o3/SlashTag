using System.Windows;
using CircleLight.Controls;
using CircleLight.Data;
using CircleLight.Models;
using CircleLight.Util;

namespace CircleLight.Views;

public partial class TaskEditorWindow : PixelWindow
{
    private readonly TaskStore _store;
    private readonly TaskRecord? _task;

    /// <summary>本次编辑是否新建了任务（用于播放新增动效）。</summary>
    public bool Created { get; private set; }

    public TaskEditorWindow(TaskStore store, TaskRecord? task = null, string? initialTitle = null)
    {
        InitializeComponent();
        _store = store;
        _task = task;

        if (task is not null)
        {
            Title = "EDIT QUEST";
            TitleBox.Text = task.Title;
            StartBox.Text = task.StartAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            DueBox.Text = task.DueAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            LongTermBox.IsChecked = task.IsLongTerm;
        }
        else
        {
            Title = "NEW QUEST";
            TitleBox.Text = initialTitle ?? "";
        }

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
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

        if (_task is null)
        {
            _store.AddTask(title, start, due, LongTermBox.IsChecked == true, DateTime.Now);
            Created = true;
        }
        else
        {
            _store.UpdateTask(_task.Id, title, start, due, LongTermBox.IsChecked == true);
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool TryParseTime(string text, out DateTime? value, out string error) =>
        UserTime.TryParse(text, DateTime.Now, out value, out error);
}
