using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CircleLight.Views;

public partial class FallbackToastWindow : Window
{
    private readonly Action _open;
    private readonly DispatcherTimer _timer;

    public FallbackToastWindow(string header, string body, Action open)
    {
        InitializeComponent();
        HeaderText.Text = header;
        BodyText.Text = body;
        _open = open;

        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 16;
        Top = work.Bottom - Height - 16;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (_, _) => Close();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _open();
        Close();
    }
}
