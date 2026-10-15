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
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextDataFormat = System.Windows.TextDataFormat;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using MenuItem = System.Windows.Controls.MenuItem;
using System.IO;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using DataObject = System.Windows.DataObject;
using System.Collections.Specialized;
using System.Text.Json;
using System.Diagnostics;

namespace Clipori.Windows;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty SearchQueryProperty =
        DependencyProperty.Register(nameof(SearchQuery), typeof(string), typeof(MainWindow),
            new PropertyMetadata(""));
    public string SearchQuery
    {
        get => (string)GetValue(SearchQueryProperty);
        set => SetValue(SearchQueryProperty, value);
    }
    private readonly SearchEngine _searchEngine = new();
    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(190) };
    private readonly DispatcherTimer _singleClickDelay = new();
    private ClipEntry? _pendingSingle;
    private Dictionary<string, string> _categoryColors = new();
    private uint _registeredModifiers, _registeredKey;
    private readonly ClipRepository _repository;
    private readonly UserSettings _settings;
    private IntPtr _previousFocus;
    private bool _dialogOpen;
    private bool _contextOpen;
    private bool _pastePending;
    private Point _dragStart;
    private bool _dragged;
    private readonly HashSet<string> _selection = new(StringComparer.Ordinal);
    private readonly List<Button> _dynamicCategoryButtons = new();
    private readonly ObservableCollection<ClipEntry> _visible = new();
    private readonly List<ClipEntry> _all = new();
    private readonly Forms.NotifyIcon _tray;

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
        _settings = UserSettings.Load();
        Diagnostics.Verbose = _settings.DiagnosticsEnabled;
        _paused = _settings.CapturePaused;
        _repository = new ClipRepository();
        _repository.Prune(_settings.RetentionDays);
        _all.AddRange(_repository.Load());
        _searchEngine.Rebuild(_all);
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); RefreshCards(); };
        _singleClickDelay.Interval = TimeSpan.FromMilliseconds((int)NativeMethods.GetDoubleClickTime() + 30);
        _singleClickDelay.Tick += (_, _) =>
        {
            _singleClickDelay.Stop();
            var selected = _pendingSingle;
            _pendingSingle = null;
            if (selected != null) { CopyEntry(selected); Preview(selected); }
        };
        CardsList.ItemsSource = _visible;
        RefreshCustomCategories();
        RefreshCards();
        PositionShelf();

        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIconFactory.Make(),
            Text = "Clipori for Windows · Ctrl+Alt+V",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("显示 / 隐藏", null, (_, _) => Dispatcher.Invoke(ToggleShelf));
        _tray.ContextMenuStrip.Items.Add("暂停 / 继续记录", null, (_, _) => Dispatcher.Invoke(ToggleCapture));
        _tray.ContextMenuStrip.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(() => OpenSettings()));
        _tray.ContextMenuStrip.Items.Add("打开诊断日志", null, (_, _) => Dispatcher.Invoke(OpenLogs));
        _tray.ContextMenuStrip.Items.Add("退出 Clipori", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ToggleShelf);

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        PauseButton.Content = _paused ? "▶" : "⏸";
        StatusDot.Fill = _paused ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(107, 202, 160));
        Diagnostics.Info("main.ready", "history=" + _all.Count);
    }

    private void PositionShelf()
    {
        Rect screen = SystemParameters.WorkArea;
        Width = Math.Max(540, Math.Min(1240, screen.Width - 32));
        Height = 438;
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
        Diagnostics.Info("win32.listener", "clipboard_listener_registered");
        _registeredHotkey = RegisterConfiguredHotkey(_settings.GlobalHotkeyModifiers, _settings.GlobalHotkeyKey);
        Diagnostics.Info("win32.hotkey", "registered=" + _registeredHotkey);
        UpdateHotkeyLabels();
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
            Diagnostics.Error("clipboard.capture", ex);
            StatusLabel.Text = "记录失败 · 查看诊断日志";
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
        if (_settings.IsExcluded(source)) return;
        // Respect Windows clipboard manager exclusion metadata where present.
        if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;

        ClipEntry? newItem = null;
        byte[]? imageBytes = null;
        if (Clipboard.ContainsFileDropList())
        {
            var dropped = Clipboard.GetFileDropList().Cast<string>().Where(p => File.Exists(p) || Directory.Exists(p)).ToArray();
            if (dropped.Length == 0) return;
            string serialized = JsonSerializer.Serialize(dropped);
            newItem = new ClipEntry {
                Kind = "files", Text = serialized, Title = dropped.Length == 1 ? Path.GetFileName(dropped[0]) : $"{dropped.Length} 个文件 / 文件夹",
                Source = source, Hash = HashBytes(Encoding.UTF8.GetBytes(serialized))
            };
        }
        else if (Clipboard.ContainsImage())
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
            string kind = ClipboardClassifier.Classify(normalized);
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
            string file = Path.Combine(_repository.ImagesPath, newItem.Id + ".dat");
            File.WriteAllBytes(file, Protection.ProtectBytes(imageBytes));
            newItem.ImagePath = file;
        }
        try { _repository.Add(newItem); }
        catch { if (newItem.ImagePath != null) File.Delete(newItem.ImagePath); throw; }
        _all.Insert(0, newItem);
        _searchEngine.Upsert(newItem);
        if (_all.Count > 1000)
        {
            _searchEngine.Remove(_all[^1].Id);
            _all.RemoveAt(_all.Count - 1);
        }
        RefreshCards();
        Diagnostics.Info("clipboard.saved", "kind=" + newItem.Kind);
        if (_settings.AutoClassify) _ = AutoClassifyAsync(newItem);
    }

    private async Task AutoClassifyAsync(ClipEntry item)
    {
        try
        {
            var categories = _repository.Categories().Select(c => c.Name).ToArray();
            string chosen = await AiService.SuggestCategoryAsync(_settings, item, categories);
            if (!string.IsNullOrEmpty(chosen) && _all.Contains(item))
            {
                item.CategoryName = chosen;
                _repository.Save(item);
                _searchEngine.Upsert(item);
                RefreshCards();
                Diagnostics.Info("ai.classified", "category_selected");
            }
        }
        catch (Exception ex) { Diagnostics.Error("ai.classification", ex); }
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private void RefreshCards()
    {
        if (CardsList == null || CountLabel == null) return;
        string query = SearchBox?.Text.Trim() ?? "";
        var matches = _searchEngine.Search(query, _category, 70);
        // Rendering previously created up to 1,000 full WPF cards on each keystroke.
        // Debouncing + bound search index + virtualized <=70 cards fixes the UI bottleneck.
        _visible.Clear();
        int index = 1;
        foreach (var match in matches)
        {
            var entry = match.Entry;
            entry.DisplayIndex = index++;
            entry.SearchSnippet = query.Length == 0 ? entry.Preview : match.Excerpt;
            _visible.Add(entry);
        }
        CountLabel.Text = query.Length == 0
            ? $"{_visible.Count} / {_searchEngine.Count} 条记忆"
            : $"{_visible.Count} 条匹配（包含模糊结果）";
        EmptyHint.Visibility = _visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in new[] { AllTab, TextTab, LinkTab, CodeTab, ImageTab, FileTab, PinnedTab }.Concat(_dynamicCategoryButtons))
        {
            bool active = string.Equals(tab.Tag?.ToString(), _category, StringComparison.Ordinal);
            tab.Background = active ? new SolidColorBrush(Color.FromRgb(235, 225, 249)) : Brushes.Transparent;
            tab.Foreground = active ? new SolidColorBrush(Color.FromRgb(113, 81, 173)) : new SolidColorBrush(Color.FromRgb(123, 115, 136));
            if (!active && (tab.Tag?.ToString() ?? "").StartsWith("category:", StringComparison.Ordinal))
            {
                string name = (tab.Tag?.ToString() ?? "")["category:".Length..];
                if (_categoryColors.TryGetValue(name, out var color))
                    try { tab.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(color)); }
                    catch { }
            }
        }
    }

    private bool RegisterConfiguredHotkey(uint modifiers, uint vk)
    {
        bool okay = NativeMethods.RegisterHotKey(_ownHandle, (int)NativeMethods.HOTKEY_ID, modifiers, vk);
        if (okay) { _registeredModifiers = modifiers; _registeredKey = vk; }
        return okay;
    }

    private void ApplyChangedHotkey()
    {
        if (_ownHandle == IntPtr.Zero) return;
        uint requestedMods = _settings.GlobalHotkeyModifiers, requestedKey = _settings.GlobalHotkeyKey;
        if (_registeredHotkey && requestedMods == _registeredModifiers && requestedKey == _registeredKey) return;
        uint previousMods = _registeredModifiers, previousKey = _registeredKey;
        if (_registeredHotkey)
            NativeMethods.UnregisterHotKey(_ownHandle, (int)NativeMethods.HOTKEY_ID);
        bool okay = RegisterConfiguredHotkey(requestedMods, requestedKey);
        if (!okay)
        {
            // Keep the original working shortcut if the user's new shortcut conflicts.
            _registeredHotkey = RegisterConfiguredHotkey(previousMods, previousKey);
            _settings.GlobalHotkeyModifiers = previousMods;
            _settings.GlobalHotkeyKey = previousKey;
            _settings.Save();
            MessageBox.Show(this, "此快捷键已被占用，保留之前的快捷键。", "Clipori 快捷键");
        }
        else { _registeredHotkey = true; Diagnostics.Info("hotkey.changed", ShortcutService.DescribeGlobal(requestedMods, requestedKey)); }
        UpdateHotkeyLabels();
    }

    private void UpdateHotkeyLabels()
    {
        string description = ShortcutService.DescribeGlobal(_settings.GlobalHotkeyModifiers, _settings.GlobalHotkeyKey);
        _tray.Text = "Clipori for Windows · " + description;
        SearchBox.ToolTip = "搜索剪贴板历史 / " + _settings.ShortcutSearch;
    }

    private void ToggleShelf()
    {
        if (IsVisible) { Hide(); return; }
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != IntPtr.Zero && foreground != _ownHandle)
        {
            _previousTarget = foreground;
            _previousFocus = NativeMethods.CaptureFocusedControl(foreground);
            Diagnostics.Info("shelf.target", "focused_child=" + (_previousFocus != IntPtr.Zero));
        }
        PositionShelf();
        Show();
        Activate();
        SearchBox.Focus();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_settings.HideOnDeactivate) return;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (IsVisible && !IsActive && !_dialogOpen && !_contextOpen && !_pastePending)
            {
                Hide();
                Diagnostics.Info("shelf.auto-hide");
            }
        };
        timer.Start();
    }
    private void ContextMenu_Opened(object sender, RoutedEventArgs e) => _contextOpen = true;
    private void ContextMenu_Closed(object sender, RoutedEventArgs e) => _contextOpen = false;

    private void SetClipboardFromEntry(ClipEntry item)
    {
        // Capture suppression targets only our own content, not all clipboard changes.
        _writtenHash = item.Hash;
        _writtenAtUtc = DateTime.UtcNow;
        if (item.Kind == "files")
        {
            var paths = JsonSerializer.Deserialize<string[]>(item.Text) ?? Array.Empty<string>();
            var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (existing.Count == 0) throw new FileNotFoundException("原始文件已移动或删除，无法粘贴。");
            var list = new StringCollection();
            list.AddRange(existing.ToArray());
            Clipboard.SetFileDropList(list);
        }
        else if (item.Kind == "image" && item.ImagePath != null && File.Exists(item.ImagePath))
        {
            byte[] raw = File.ReadAllBytes(item.ImagePath);
            if (item.ImagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) raw = Protection.UnprotectBytes(raw);
            using var ms = new MemoryStream(raw);
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = ms;
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
            Diagnostics.Info("clipboard.copy", "kind=" + item.Kind);
        }
        catch (Exception ex) { MessageBox.Show(this, "复制失败：" + ex.Message, "Clipori"); }
    }

    private void PasteEntry(ClipEntry item)
    {
        if (_pastePending) return;
        try { SetClipboardFromEntry(item); }
        catch (Exception ex)
        {
            Diagnostics.Error("paste.copy", ex);
            MessageBox.Show(this, "无法复制该内容：" + ex.Message, "Clipori");
            return;
        }

        var target = _previousTarget;
        var focus = _previousFocus;
        _pastePending = true;
        Hide(); // hide first, so the shelf is no longer the foreground focus owner
        Diagnostics.Info("paste.begin", "kind=" + item.Kind + ";target_valid=" + NativeMethods.IsWindow(target));

        int polls = 0;
        // Hotkey Ctrl+Alt+V often leaves its modifiers physically down for a moment.
        // CopyQ waits for modifiers to be released before issuing synthetic input.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(65) };
        timer.Tick += (_, _) =>
        {
            ++polls;
            if (NativeMethods.AnyPasteModifierHeld() && polls < 18)
                return;
            timer.Stop();
            try
            {
                if (NativeMethods.AnyPasteModifierHeld())
                {
                    Diagnostics.Warn("paste.fallback", "modifiers_still_held");
                    NotifyManualPaste();
                    return;
                }
                bool focused = NativeMethods.RestoreForegroundAndFocus(target, focus);
                if (!focused)
                {
                    Diagnostics.Warn("paste.fallback", "target_activation_blocked");
                    NotifyManualPaste();
                    return;
                }
                if (NativeMethods.GetForegroundWindow() != target)
                {
                    Diagnostics.Warn("paste.fallback", "foreground_changed_before_send");
                    NotifyManualPaste();
                    return;
                }
                if (!NativeMethods.SendControlV())
                {
                    Diagnostics.Warn("paste.fallback", "send_input_failed;win32=" +
                        System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    NotifyManualPaste();
                    return;
                }
                // SendInput reports injected key events, not confirmation of
                // successful insertion in the target app.
                Diagnostics.Info("paste.input-sent", "kind=" + item.Kind + ";target_foreground=true");
                StatusLabel.Text = "已发送粘贴指令 · " + item.TypeLabel;
            }
            catch (Exception ex)
            {
                Diagnostics.Error("paste.error", ex);
                NotifyManualPaste();
            }
            finally { _pastePending = false; }
        };
        timer.Start();
    }

    private void NotifyManualPaste()
    {
        StatusLabel.Text = "自动粘贴未完成，可手动按 Ctrl+V";
        _tray.ShowBalloonTip(3500, "Clipori", "已复制到剪贴板，请切回目标应用按 Ctrl+V。", Forms.ToolTipIcon.Info);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(this);
    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Border { DataContext: ClipEntry item }) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragged = true;
        if (!_selection.Contains(item.Id))
        {
            foreach (var selected in _all) selected.IsSelected = false;
            _selection.Clear(); _selection.Add(item.Id); item.IsSelected = true;
        }
        var data = new DataObject("Clipori.EntryIds", string.Join(",", _selection));
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: ClipEntry item }) return;
        if (_dragged) { _dragged = false; e.Handled = true; return; }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_selection.Remove(item.Id)) item.IsSelected = false;
            else { _selection.Add(item.Id); item.IsSelected = true; }
            CopyEntry(item);
        }
        else if (_settings.SingleClickPaste) PasteEntry(item);
        else if (e.ClickCount >= 2)
        {
            _singleClickDelay.Stop();
            _pendingSingle = null;
            PasteEntry(item);
        }
        else
        {
            // Copy immediately, but defer opening a modal detail window until
            // the double-click interval has elapsed. Otherwise the first click
            // consumes the second click and makes double-click paste impossible.
            CopyEntry(item);
            _pendingSingle = item;
            _singleClickDelay.Stop();
            _singleClickDelay.Start();
        }
        e.Handled = true;
    }
    private static ClipEntry? MenuEntry(object sender) => (sender as MenuItem)?.DataContext as ClipEntry;
    private void CopyMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) CopyEntry(entry); }
    private void PasteMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) PasteEntry(entry); }
    private void PreviewMenu_Click(object sender, RoutedEventArgs e) { if (MenuEntry(sender) is { } entry) Preview(entry); }
    private void EditMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is not { } entry) return;
        _dialogOpen = true;
        try
        {
            var editor = new TextEditorWindow(entry) { Owner = this };
            if (editor.ShowDialog() != true) return;
            entry.Title = editor.NoteTitle;
            if (entry.Kind != "image" && entry.Kind != "files") entry.Text = editor.NoteBody;
            entry.Notes = editor.NoteNotes;
            entry.Hash = HashBytes(Encoding.UTF8.GetBytes(entry.Text));
            _repository.Save(entry);
            _searchEngine.Upsert(entry);
            entry.NotifyChanged(); RefreshCards();
            Diagnostics.Info("entry.edited", "kind=" + entry.Kind);
        }
        finally { _dialogOpen = false; }
    }
    private void CategoryMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem parent || parent.DataContext is not ClipEntry entry) return;
        parent.Items.Clear();
        foreach (var name in new[] { "" }.Concat(_repository.Categories().Select(c => c.Name)))
        {
            var local = name;
            var item = new MenuItem { Header = local.Length == 0 ? "未分类" : local };
            item.Click += (_, _) => AssignSelected(entry, local);
            parent.Items.Add(item);
        }
    }
    private void AssignSelected(ClipEntry source, string category)
    {
        var selected = _selection.Contains(source.Id) ? _all.Where(c => _selection.Contains(c.Id)).ToList() : new List<ClipEntry> { source };
        foreach (var item in selected)
        {
            item.CategoryName = category; _repository.Save(item); _searchEngine.Upsert(item); item.IsSelected = false;
        }
        _selection.Clear(); RefreshCards(); Diagnostics.Info("category.assign", "count=" + selected.Count);
    }
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
        _searchEngine.Remove(entry.Id);
        _all.Remove(entry);
        RefreshCards();
    }
    private void Preview(ClipEntry item)
    {
        _dialogOpen = true;
        try
        {
            var dialog = new EntryDetailWindow(item, CopyEntry, PasteEntry) { Owner = this };
            dialog.ShowDialog();
        }
        finally { _dialogOpen = false; }
    }
    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        _dialogOpen = true;
        TextEditorWindow editor = new() { Owner = this };
        try { if (editor.ShowDialog() != true) return; }
        finally { _dialogOpen = false; }
        var note = new ClipEntry
        {
            Kind = "text", Title = editor.NoteTitle, Text = editor.NoteBody, Notes = editor.NoteNotes,
            Source = "Clipori 便笺", Hash = HashBytes(Encoding.UTF8.GetBytes(editor.NoteBody))
        };
        _repository.Add(note);
        _all.Insert(0, note);
        _searchEngine.Upsert(note);
        _category = "all";
        SearchBox.Clear();
        RefreshCards();
        Diagnostics.Info("note.created");
    }
    private void Category_Click(object sender, RoutedEventArgs e)
    {
        _category = (sender as Button)?.Tag?.ToString() ?? "all";
        RefreshCards();
    }
    private void RefreshCustomCategories()
    {
        _categoryColors = _repository.Categories().ToDictionary(c => c.Name, c => c.Color);
        CustomCategories.Children.Clear();
        _dynamicCategoryButtons.Clear();
        foreach (var cat in _repository.Categories())
        {
            var button = new Button {
                Content = (cat.Pinboard ? "⌖ " : "● ") + cat.Name, Tag = "category:" + cat.Name,
                Style = (Style)FindResource("CategoryTab"), AllowDrop = true
            };
            button.Click += Category_Click;
            button.Drop += Category_Drop;
            button.DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent("Clipori.EntryIds") ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            try
            {
                var rgb = (Color)System.Windows.Media.ColorConverter.ConvertFromString(cat.Color);
                button.Foreground = new SolidColorBrush(rgb);
            }
            catch { }
            _dynamicCategoryButtons.Add(button);
            CustomCategories.Children.Add(button);
        }
    }
    private void Category_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Button btn || !e.Data.GetDataPresent("Clipori.EntryIds")) return;
        string name = (btn.Tag?.ToString() ?? "")["category:".Length..];
        string raw = e.Data.GetData("Clipori.EntryIds") as string ?? "";
        var ids = new HashSet<string>(raw.Split(',', StringSplitOptions.RemoveEmptyEntries));
        foreach (var item in _all.Where(c => ids.Contains(c.Id)))
        {
            item.CategoryName = name;
            item.IsSelected = false;
            _repository.Save(item);
            _searchEngine.Upsert(item);
        }
        _selection.Clear(); _dragged = false;
        RefreshCards(); e.Handled = true;
        Diagnostics.Info("category.drop", "count=" + ids.Count);
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenSettings()
    {
        _dialogOpen = true;
        try
        {
            var dialog = new SettingsWindow(_settings, _repository) { Owner = this };
            dialog.ShowDialog();
            if (dialog.CategoriesChanged)
            {
                var names = _repository.Categories().Select(c => c.Name).ToHashSet();
                foreach (var entry in _all)
                {
                    if (entry.CategoryName != "" && !names.Contains(entry.CategoryName))
                    { entry.CategoryName = ""; entry.NotifyChanged(); }
                }
            }
            Diagnostics.Verbose = _settings.DiagnosticsEnabled;
            ApplyChangedHotkey();
            _searchEngine.Rebuild(_all);
            RefreshCustomCategories();
            RefreshCards();
            _repository.Prune(_settings.RetentionDays);
        }
        finally { _dialogOpen = false; }
    }
    private void OpenLogs()
    {
        Directory.CreateDirectory(Diagnostics.DirectoryPath);
        Process.Start(new ProcessStartInfo { FileName = Diagnostics.DirectoryPath, UseShellExecute = true });
    }
    private async void AiSearch_Click(object sender, RoutedEventArgs e)
    {
        string prompt = SearchBox.Text.Trim();
        if (prompt.Length == 0) { SearchBox.Focus(); return; }
        _dialogOpen = true;
        try
        {
            StatusLabel.Text = "AI 正在检索…";
            // Manual opt-in: no clipboard content is sent unless the user clicks AI search.
            string answer = await AiService.AskAsync(_settings, prompt, _all);
            var response = new ClipEntry { Title = "AI 找回 · " + prompt, Text = answer, Kind = "text" };
            new PreviewWindow(response) { Owner = this }.ShowDialog();
            Diagnostics.Info("ai.search", "completed");
        }
        catch (Exception ex)
        {
            Diagnostics.Error("ai.search", ex);
            MessageBox.Show(this, ex.Message, "AI 找回");
        }
        finally { _dialogOpen = false; StatusLabel.Text = "正在悄悄记住"; }
    }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CardsList == null) return;
        SearchQuery = SearchBox.Text.Trim();
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }
    private void PauseButton_Click(object sender, RoutedEventArgs e) => ToggleCapture();
    private void ToggleCapture()
    {
        _paused = !_paused;
        _settings.CapturePaused = _paused;
        _settings.Save();
        Diagnostics.Info("capture.toggle", "paused=" + _paused);
        PauseButton.Content = _paused ? "▶" : "⏸";
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

    private void CardsScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (CardsScroller.ExtentWidth <= CardsScroller.ViewportWidth) return;
        CardsScroller.ScrollToHorizontalOffset(CardsScroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
    private void ScrollLeft_Click(object sender, RoutedEventArgs e) =>
        CardsScroller.ScrollToHorizontalOffset(Math.Max(0, CardsScroller.HorizontalOffset - 520));

    private void ScrollRight_Click(object sender, RoutedEventArgs e) =>
        CardsScroller.ScrollToHorizontalOffset(CardsScroller.HorizontalOffset + 520);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _singleClickDelay.Stop(); Hide(); e.Handled = true; return; }
        if (ShortcutService.Matches(_settings.ShortcutSearch, e))
        {
            SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; return;
        }
        if (ShortcutService.Matches(_settings.ShortcutPause, e))
        { ToggleCapture(); e.Handled = true; return; }
        if (ShortcutService.Matches(_settings.ShortcutNewNote, e))
        { NewNote_Click(sender, new RoutedEventArgs()); e.Handled = true; return; }
        bool control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Enter && _visible.Count > 0 &&
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _singleClickDelay.Stop();
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
        _searchDebounce.Stop();
        _singleClickDelay.Stop();
        _source?.RemoveHook(WndProc);
        _tray.Visible = false;
        var trayIcon = _tray.Icon;
        _tray.Dispose();
        trayIcon?.Dispose();
        Diagnostics.Info("app.tray-exit");
        System.Windows.Application.Current.Shutdown();
    }
}
