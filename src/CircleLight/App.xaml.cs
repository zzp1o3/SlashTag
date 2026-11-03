using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CircleLight.Data;
using CircleLight.Services;
using CircleLight.Storage;
using CircleLight.Views;
using CommunityToolkit.WinUI.Notifications;

namespace CircleLight;

public partial class App : System.Windows.Application
{
    private const string MutexName = "CircleLight.SingleInstance";
    private const string ShowEventName = "CircleLight.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private TaskStore? _store;
    private TrayService? _tray;
    private ReminderService? _reminders;
    private FallbackToastWindow? _fallback;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); }
            catch { }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        StartShowListener();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log("unhandled: " + args.ExceptionObject);

        _store = new TaskStore(AppPaths.DatabasePath);

        var window = new MainWindow(_store);
        MainWindow = window;

        _tray = new TrayService(window, window.OpenSettings);

        var notifier = new NotificationService(ShowFallback, Log);
        _reminders = new ReminderService(_store, notifier);
        window.BindReminders(_reminders);

        ToastNotificationManagerCompat.OnActivated += _ =>
            Dispatcher.BeginInvoke(new Action(() => window.ShowNote()));

        window.Show();
        if (!e.Args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase)))
            window.Activate();

        _reminders.Start();
    }

    private void StartShowListener()
    {
        var thread = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
            {
                Dispatcher.BeginInvoke(new Action(() => (MainWindow as MainWindow)?.ShowNote()));
            }
        })
        {
            IsBackground = true,
            Name = "CircleLight.ShowListener",
        };
        thread.Start();
    }

    private void ShowFallback(string header, string body)
    {
        _fallback?.Close();
        _fallback = new FallbackToastWindow(header, body,
            () => (MainWindow as MainWindow)?.ShowNote());
        _fallback.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("dispatcher: " + e.Exception);
        System.Windows.MessageBox.Show("程序遇到错误：\n" + e.Exception.Message, "CircleLight",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _reminders?.Dispose();
        _tray?.Dispose();
        _store?.Dispose();
        _showEvent?.Dispose();

        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); }
            catch { }
            _mutex.Dispose();
        }

        base.OnExit(e);
    }

    public static void Log(string message)
    {
        try
        {
            var dir = AppPaths.DataDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
