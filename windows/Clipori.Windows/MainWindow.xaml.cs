using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipori.Windows.Interop;
using Clipori.Windows.Models;
using Clipori.Windows.Services;
using Clipori.Windows.Views;
using Forms = System.Windows.Forms;

namespace Clipori.Windows;

public partial class MainWindow : Window
{
    private readonly ClipRepository _repository;
    private readonly ObservableCollection<ClipEntry> _visible = new();
    private readonly List<ClipEntry> _all = new();
    private readonly Forms.NotifyIcon _tray;
    private static readonly HashSet<string> PrivateProcesses = new(StringComparer.OrdinalIgnoreCase)
    { "KeePass", "KeePassXC", "Bitwarden", "1Password", "LastPass", "CredentialUIBroker" };

    private HwndSource? _source;
    private IntPtr _ownHandle;
    private IntPtr _previousTarget;
    private bool _paused;
    private bool _exiting;
    private bool _registeredHotkey;
    private string _category = "all";
    private string? _writtenHash;
    private DateTime _writtenAtUtc;
    private bool _busyCapturing;
    private int _captureRetries;

    public MainWindow()
    {
        InitializeComponent();
        _repository = new ClipRepository();
        _all.AddRange(_repository.Load());
        CardsList.ItemsSource = _visible;
        RefreshCards();
        PositionShelf();

        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Clipori for Windows · Ctrl+Alt+V",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("显示 / 隐藏", null, (_, _) => Dispatcher.Invoke(ToggleShelf));
        _tray.ContextMenuStrip.Items.Add("暂停 / 继续记录", null, (_, _) => Dispatcher.Invoke(ToggleCapture));
        _tray.ContextMenuStrip.Items.Add("退出 Clipori", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ToggleShelf);

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void PositionShelf()
    {
        Rect screen = SystemParameters.WorkArea;
        Width = Math.Max(540, Math.Min(1190, screen.Width - 32));
        Height = 398;
        Left = screen.Left + (screen.Width - Width) / 2;
        Top = Math.Max(screen.Top, screen.Bottom - Height - 10);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _ownHandle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_ownHandle);
        _source?.AddHook(WndProc);
        if (!NativeMethods.AddClipboardFormatListener(_ownHandle))
        {
            MessageBox.Show("剪贴板监听注册失败。请重启应用。", "Clipori", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _registeredHotkey = NativeMethods.RegisterHotKey(_ownHandle, (int)NativeMethods.HOTKEY_ID,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_V);
        if (!_registeredHotkey)
        {
            StatusLabel.Text = "全局快捷键被占用，可通过托盘图标打开";
            _tray?.ShowBalloonTip(4000, "Clipori", "Ctrl+Alt+V 注册失败，可双击托盘图标打开。", Forms.ToolTipIcon.Warning);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => TryCaptureClipboard()));
        }
        else if (message == NativeMethods.WM_HOTKEY && wParam.ToInt64() == NativeMethods.HOTKEY_ID)
        {
            ToggleShelf();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void TryCaptureClipboard()
    {
        if (_paused || _busyCapturing) return;
        _busyCapturing = true;
        try
        {
            CaptureClipboard();
            _captureRetries = 0;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            RetryBusyClipboard();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "记录失败：" + ex.Message;
        }
        finally { _busyCapturing = false; }
    }

    private void RetryBusyClipboard()
    {
        if (++_captureRetries > 3) { _captureRetries = 0; return; }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        timer.Tick += (_, _) => { timer.Stop(); TryCaptureClipboard(); };
        timer.Start();
    }

    private void CaptureClipboard()
    {
        // Privacy protection is best-effort. Clipboard managers cannot reliably detect every secret.
        var source = NativeMethods.ForegroundProcessName();
        if (PrivateProcesses.Contains(source)) return;

        ClipEntry? newItem = null;
        byte[]? imageBytes = null;
        if (Clipboard.ContainsImage())
        {
            var bitmap = Clipboard.GetImage();
            if (bitmap != null && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0 &&
                (long)bitmap.PixelWidth * bitmap.PixelHeight <= 80_000_000)
            {
                using var ms = new MemoryStream();
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                png.Save(ms);
                if (ms.Length > 128L * 1024 * 1024) return;
                imageBytes = ms.ToArray();
                newItem = new ClipEntry {
                    Kind = "image", Title = $"图片 · {bitmap.PixelWidth} × {bitmap.PixelHeight}",
                    Text = "", Source = source, Hash = HashBytes(imageBytes)
                };
            }
        }
        else if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
        {
            var content = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrWhiteSpace(content) || content.Length > 1_000_000) return;
            var normalized = content.Trim();
            string kind = GuessKind(normalized);
            var firstLine = normalized.Split('\n', 2)[0].Trim();
            string title = firstLine.Length > 48 ? firstLine[..48] + "…" : firstLine;
            newItem = new ClipEntry {
                Kind = kind, Text = content, Title = title,
                Source = source, Hash = HashBytes(Encoding.UTF8.GetBytes(content))
            };
        }

        if (newItem == null) return;
        if (newItem.Hash == _writtenHash && DateTime.UtcNow - _writtenAtUtc < TimeSpan.FromSeconds(3))
        { return; }
        if (_all.Count > 0 && _all[0].Hash == newItem.Hash && _all[0].Kind == newItem.Kind) return;

        if (imageBytes != null)
        {
            string file = Path.Combine(_repository.ImagesPath, newItem.Id + ".png");
            File.WriteAllBytes(file, imageBytes);
            newItem.ImagePath = file;
        }
        try { _repository.Add(newItem); }
        catch { if (newItem.ImagePath != null) File.Delete(newItem.ImagePath); throw; }
        _all.Insert(0, newItem);
        if (_all.Count > 1000) _all.RemoveAt(_all.Count - 1);
        RefreshCards();
    }

    private static string GuessKind(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http")) return "link";
        if ((value.Contains("{", StringComparison.Ordinal) && value.Contains("}", StringComparison.Ordinal)) ||
            value.Contains("=>", StringComparison.Ordinal) || value.StartsWith("using ", StringComparison.Ordinal) ||
            value.StartsWith("def ", StringComparison.Ordinal)) return "code";
        return "text";
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private void RefreshCards()
    {
        string query = SearchBox?.Text.Trim() ?? "";
        IEnumerable<ClipEntry> filtered = _all;
        if (_category == "pinned") filtered = filtered.Where(c => c.IsPinned);
        else if (_category != "all") filtered = filtered.Where(c => c.Kind == _category);
        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(c => (c.Title + "\n" + c.Text + "\n" + c.Source)
                .Contains(query, StringComparison.OrdinalIgnoreCase));
        _visible.Clear();
        foreach (var entry in filtered) _visible.Add(entry);
        CountLabel.Text = $"{_visible.Count} 条记忆";
        EmptyHint.Visibility = _visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var b in new[] { AllTab, TextTab, LinkTab, CodeTab, ImageTab, PinnedTab })
        {
            bool active = string.Equals(b.Tag?.ToString(), _category, StringComparison.Ordinal);
            b.Background = active ? new SolidColorBrush(Color.FromRgb(235, 225, 249)) : Brushes.Transparent;
            b.Foreground = active ? new SolidColorBrush(Color.FromRgb(113, 81, 173)) : new SolidColorBrush(Color.FromRgb(123, 115, 136));
        }
    }

    private void ToggleShelf()
    {
        if (IsVisible) { Hide(); return; }
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != IntPtr.Zero && foreground != _ownHandle) _previousTarget = foreground;
        PositionShelf();
        Show();
        Activate();
        SearchBox.Focus();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Keep the shelf open to support context menus, previews and notes.
        // Escape and the tray are always available to dismiss it.
    }

    private void SetClipboardFromEntry(ClipEntry item)
    {
        // Capture suppression targets only our own content, not all clipboard changes.
        _writtenHash = item.Hash;
        _writtenAtUtc = DateTime.UtcNow;
        if (item.Kind == "image" && item.ImagePath != null && File.Exists(item.ImagePath))
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(item.ImagePath, UriKind.Absolute);
            source.EndInit();
            source.Freeze();
            Clipboard.SetImage(source);
        }
        else Clipboard.SetText(item.Text, TextDataFormat.UnicodeText);
    }

    private void CopyEntry(ClipEntry item)
    {
        try
        {
            SetClipboardFromEntry(item);
            StatusLabel.Text = "已复制 · " + item.TypeLabel;
        }
        catch (Exception ex) { MessageBox.Show(this, "复制失败：" + ex.Message, "Clipori"); }
    }

    private void PasteEntry(ClipEntry item)
    {
        try { SetClipboardFromEntry(item); }
        catch (Exception ex) { MessageBox.Show(this, "复制失败：" + ex.Message, "Clipori"); return; }
        Hide();
        var target = _previousTarget;
        if (target == IntPtr.Zero || !NativeMethods.IsWindow(target))
        {
            _tray.ShowBalloonTip(2500, "Clipori", "内容已复制，未找到之前的窗口；请手动 Ctrl+V 粘贴。", Forms.ToolTipIcon.Info);
            return;
        }
        if (NativeMethods.IsIconic(target)) NativeMethods.ShowWindow(target, 9);
        bool focused = NativeMethods.SetForegroundWindow(target);
        if (!focused)
        {
            _tray.ShowBalloonTip(2500, "Clipori", "内容已复制，请切回目标应用并按 Ctrl+V。", Forms.ToolTipIcon.Info);
            return;
        }
        // The target app may need a moment to regain input focus before Ctrl+V.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (NativeMethods.GetForegroundWindow() != target || !NativeMethods.SendControlV())
                _tray.ShowBalloonTip(2500, "Clipori", "内容已复制，自动粘贴未完成；请按 Ctrl+V。", Forms.ToolTipIcon.Info);
        };
        timer.Start();
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: ClipEntry item }) return;
        if (e.ClickCount >= 2) PasteEntry(item);
        else CopyEntry(item);
        e.Handled = true;
    }
    private static ClipEntry? MenuEntry(object sender) => (sender as MenuItem)?.DataContext as ClipEntry;
    private void CopyMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) CopyEntry(entry); }
    private void PasteMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) PasteEntry(entry); }
    private void PreviewMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) Preview(entry); }
    private void PinMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is not { } entry) return;
        entry.IsPinned = !entry.IsPinned;
        _repository.SetPinned(entry);
        RefreshCards();
    }
    private void DeleteMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is not { } entry) return;
        _repository.Delete(entry);
        _all.Remove(entry);
        RefreshCards();
    }
    private void Preview(ClipEntry item)
    {
        var dialog = new PreviewWindow(item) { Owner = this };
        dialog.ShowDialog();
    }
    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TextEditorWindow { Owner = this };
        if (editor.ShowDialog() != true) return;
        var note = new ClipEntry
        {
            Kind = "text", Title = editor.NoteTitle, Text = editor.NoteBody,
            Source = "Clipori 便笺", Hash = HashBytes(Encoding.UTF8.GetBytes(editor.NoteBody))
        };
        _repository.Add(note);
        _all.Insert(0, note);
        _category = "all";
        SearchBox.Clear();
        RefreshCards();
    }
    private void Category_Click(object sender, RoutedEventArgs e)
    {
        _category = (sender as Button)?.Tag?.ToString() ?? "all";
        RefreshCards();
    }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CardsList != null) RefreshCards();
    }
    private void PauseButton_Click(object sender, RoutedEventArgs e) => ToggleCapture();
    private void ToggleCapture()
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "继续记录" : "暂停记录";
        StatusDot.Fill = _paused ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(107, 202, 160));
        StatusLabel.Text = _paused ? "剪贴板记录已暂停" : "正在悄悄记住";
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var node = e.OriginalSource as DependencyObject;
        while (node != null)
        {
            if (node is Button || node is TextBox) return;
            if (node == sender) break;
            node = VisualTreeHelper.GetParent(node);
        }
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; return; }
        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (control && e.Key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (control && e.Key == Key.T) { ToggleCapture(); e.Handled = true; }
        else if (control && e.Key == Key.N) { NewNote_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Enter && _visible.Count > 0 && !(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            PasteEntry(_visible[0]); e.Handled = true;
        }
        else if (control && e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int index = e.Key - Key.D1;
            if (index < _visible.Count) PasteEntry(_visible[index]);
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exiting) { e.Cancel = true; Hide(); }
    }
    private void ExitApp()
    {
        _exiting = true;
        if (_ownHandle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_ownHandle);
            if (_registeredHotkey) NativeMethods.UnregisterHotKey(_ownHandle, (int)NativeMethods.HOTKEY_ID);
        }
        _source?.RemoveHook(WndProc);
        _tray.Visible = false;
        _tray.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
