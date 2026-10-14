using System.Windows;
using Clipori.Windows.Services;
using MessageBox = System.Windows.MessageBox;

namespace Clipori.Windows;

public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Diagnostics.Info("app.start", "args=" + (e.Args.Contains("--debug") ? "debug" : "normal"));
        DispatcherUnhandledException += (_, args) =>
        {
            Diagnostics.Error("app.dispatcher-unhandled", args.Exception);
            MessageBox.Show("Clipori 遇到错误，已写入本地日志。请检查日志文件夹。", "Clipori");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Diagnostics.Error("app.unhandled", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Diagnostics.Error("app.task-unobserved", args.Exception);
            args.SetObserved();
        };
        _mutex = new System.Threading.Mutex(true, "Local\\Clipori.Windows.SingleInstance", out bool first);
        _ownsMutex = first;
        if (!first)
        {
            MessageBox.Show("Clipori for Windows 已在运行。请使用 Ctrl + Alt + V 唤醒。", "Clipori");
            Shutdown();
            return;
        }
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            window.Hide();
        }
        catch (Exception ex)
        {
            Diagnostics.Error("app.startup", ex);
            MessageBox.Show("Clipori 启动失败；详情请查看 %LOCALAPPDATA%\\CliporiWindows\\logs", "Clipori", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Diagnostics.Info("app.exit", "code=" + e.ApplicationExitCode);
        if (_mutex != null)
        {
            if (_ownsMutex) _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
