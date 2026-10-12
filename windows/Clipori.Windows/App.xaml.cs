using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Clipori.Windows;

public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
            MessageBox.Show($"Clipori 启动失败：\n{ex}", "Clipori", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex != null)
        {
            if (_ownsMutex) _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
