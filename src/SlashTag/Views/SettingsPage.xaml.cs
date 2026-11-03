using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using SlashTag.Services;

namespace SlashTag.Views;

public partial class SettingsPage : UserControl
{
    private string _dataPath = "";

    public SettingsPage() => InitializeComponent();

    public void Initialize(string dataPath)
    {
        _dataPath = dataPath;
        DataPathText.Text = dataPath;
        AutostartBox.IsChecked = StartupService.IsEnabled();
        ErrorText.Visibility = Visibility.Collapsed;

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = $"SlashTag {version}";
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
        ErrorText.Text = "写入开机启动项失败，请检查权限。";
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(_dataPath);
            if (string.IsNullOrEmpty(folder)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorText.Text = "打开目录失败：" + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
