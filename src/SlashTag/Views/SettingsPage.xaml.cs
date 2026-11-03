using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SlashTag.Data;
using SlashTag.Services;
using SlashTag.Util;
using UserControl = System.Windows.Controls.UserControl;

namespace SlashTag.Views;

public partial class SettingsPage : UserControl
{
    private TaskStore? _store;

    public SettingsPage() => InitializeComponent();

    public void Initialize(TaskStore store)
    {
        _store = store;
        DataPathText.Text = store.DatabasePath;
        AutostartBox.IsChecked = StartupService.IsEnabled();
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateLanguageButtons();

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = $"SlashTag {version}";
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
