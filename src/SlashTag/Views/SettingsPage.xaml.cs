using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SlashTag.Data;
using SlashTag.Services;
using SlashTag.Util;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Binding = System.Windows.Data.Binding;

namespace SlashTag.Views;

public partial class SettingsPage : UserControl
{
    private TaskStore? _store;
    private string _animation = "collapse";
    private HashSet<string> _pool = new(StringComparer.OrdinalIgnoreCase);

    public SettingsPage() => InitializeComponent();

    public void Initialize(TaskStore store)
    {
        _store = store;
        DataPathText.Text = store.DatabasePath;
        AutostartBox.IsChecked = StartupService.IsEnabled();
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateLanguageButtons();

        var mode = store.GetSetting("edge.animation");
        _animation = string.IsNullOrWhiteSpace(mode) ? "collapse" : mode;
        var pool = (store.GetSetting("edge.pool") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _pool = new HashSet<string>(pool, StringComparer.OrdinalIgnoreCase);
        if (_pool.Count == 0)
            _pool = new HashSet<string>(EdgeAnimator.Catalog.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        BuildAnimationControls();
        UpdateAnimationSelection();

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = $"SlashTag {version}";
    }

    private void BuildAnimationControls()
    {
        AnimationPanel.Children.Clear();
        PoolChecks.Children.Clear();

        foreach (var info in EdgeAnimator.Catalog)
        {
            var button = MakeButton(info.NameKey, () => SetAnimation(info.Id));
            button.Tag = info.Id;
            AnimationPanel.Children.Add(button);

            var check = new CheckBox
            {
                Style = (Style)FindResource("PixelCheckBox"),
                Margin = new Thickness(0, 4, 14, 0),
                Tag = info.Id,
                IsChecked = _pool.Contains(info.Id),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            check.SetBinding(ContentControl.ContentProperty,
                new Binding($"[{info.NameKey}]") { Source = Loc.Instance });
            check.Click += (_, _) =>
            {
                if (check.IsChecked == true) _pool.Add(info.Id);
                else _pool.Remove(info.Id);
                SavePool();
            };
            PoolChecks.Children.Add(check);
        }

        var random = MakeButton("EdgeAnimRandom", () => SetAnimation("random"));
        random.Tag = "random";
        AnimationPanel.Children.Add(random);
    }

    private Button MakeButton(string nameKey, Action click)
    {
        var button = new Button
        {
            Style = (Style)FindResource("PixelSecondaryButton"),
            MinWidth = 84,
            Margin = new Thickness(0, 0, 6, 6),
        };
        button.SetBinding(ContentControl.ContentProperty,
            new Binding($"[{nameKey}]") { Source = Loc.Instance });
        button.Click += (_, _) => click();
        return button;
    }

    private void SetAnimation(string id)
    {
        _animation = id;
        _store?.SetSetting("edge.animation", id);
        UpdateAnimationSelection();
    }

    private void UpdateAnimationSelection()
    {
        foreach (var child in AnimationPanel.Children)
        {
            if (child is not Button button) continue;
            var id = button.Tag as string;
            button.Style = (Style)FindResource(id == _animation ? "PixelPrimaryButton" : "PixelSecondaryButton");
        }

        PoolPanel.Visibility = _animation == "random" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SavePool()
    {
        if (_pool.Count == 0)
        {
            _pool = new HashSet<string>(EdgeAnimator.Catalog.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var child in PoolChecks.Children)
                if (child is CheckBox box) box.IsChecked = true;
        }

        _store?.SetSetting("edge.pool", string.Join(',', _pool));
    }

    private void Chinese_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.Chinese);

    private void English_Click(object sender, RoutedEventArgs e) => SetLanguage(UiLanguage.English);

    private void SetLanguage(UiLanguage language)
    {
        _store?.SetSetting("ui.language", Loc.Serialize(language));
        Loc.Instance.Set(language);
        UpdateLanguageButtons();
    }

    private void UpdateLanguageButtons()
    {
        var chinese = Loc.Instance.Language == UiLanguage.Chinese;
        ChineseButton.Style = (Style)FindResource(chinese ? "PixelPrimaryButton" : "PixelSecondaryButton");
        EnglishButton.Style = (Style)FindResource(chinese ? "PixelSecondaryButton" : "PixelPrimaryButton");
    }

    private void Autostart_Click(object sender, RoutedEventArgs e)
    {
        var wanted = AutostartBox.IsChecked == true;
        if (StartupService.SetEnabled(wanted))
        {
            ErrorText.Visibility = Visibility.Collapsed;
            return;
        }

        AutostartBox.IsChecked = !wanted;
        ErrorText.Text = Loc.Instance["AutostartError"];
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(_store?.DatabasePath ?? "");
            if (string.IsNullOrEmpty(folder)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorText.Text = Loc.Instance["OpenFolderError"] + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
