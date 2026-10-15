using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Clipori.Windows.Services;
using MessageBox = System.Windows.MessageBox;

namespace Clipori.Windows.Views;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private readonly ClipRepository _repository;
    public bool CategoriesChanged { get; private set; }

    public SettingsWindow(UserSettings settings, ClipRepository repository)
    {
        InitializeComponent();
        _settings = settings;
        _repository = repository;
        foreach (var option in new[] { ("Ctrl + Alt", 3u), ("Ctrl + Shift", 6u),
            ("Alt + Shift", 5u), ("Ctrl + Alt + Shift", 7u) })
            HotkeyModifiers.Items.Add(new ComboBoxItem { Content = option.Item1, Tag = option.Item2 });
        foreach (char letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            HotkeyKey.Items.Add(new ComboBoxItem { Content = letter.ToString(), Tag = (uint)letter });
        for (uint f = 0; f < 12; f++)
            HotkeyKey.Items.Add(new ComboBoxItem { Content = "F" + (f + 1), Tag = 0x70u + f });
        HotkeyModifiers.SelectedItem = HotkeyModifiers.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(c => (uint)c.Tag == settings.GlobalHotkeyModifiers)
            ?? HotkeyModifiers.Items[0];
        HotkeyKey.SelectedItem = HotkeyKey.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(c => (uint)c.Tag == settings.GlobalHotkeyKey)
            ?? HotkeyKey.Items.Cast<ComboBoxItem>().First(c => (uint)c.Tag == 0x56u);
        SearchShortcut.Text = settings.ShortcutSearch;
        NewNoteShortcut.Text = settings.ShortcutNewNote;
        PauseShortcut.Text = settings.ShortcutPause;
        ClickPaste.IsChecked = settings.SingleClickPaste;
        AutoHide.IsChecked = settings.HideOnDeactivate;
        AutoLaunch.IsChecked = settings.AutoStart;
        Retention.Text = settings.RetentionDays.ToString();
        ExcludedApps.Text = settings.ExcludedApps;
        AiProtocol.SelectedIndex = settings.AiProtocol.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        AiEndpoint.Text = settings.AiEndpoint;
        AiModel.Text = settings.AiModel;
        AutoClassify.IsChecked = settings.AutoClassify;
        EnableDiagnostics.IsChecked = settings.DiagnosticsEnabled;
        RefreshCategories();
    }
    private void RefreshCategories() => CategoryList.ItemsSource = _repository.Categories().Select(c => c.Name).ToList();
    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        string name = NewCategoryName.Text.Trim();
        if (name.Length is < 1 or > 32) { MessageBox.Show(this, "分类名称限制为 1–32 字符。"); return; }
        if (!ValidColor()) return;
        _repository.AddCategory(name, CategoryColor.Text.Trim(), CreatePinboard.IsChecked == true);
        CategoriesChanged = true; NewCategoryName.Clear(); RefreshCategories();
    }
    private bool ValidColor()
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(CategoryColor.Text.Trim(), "^#[0-9A-Fa-f]{6}$")) return true;
        MessageBox.Show(this, "颜色必须是 #RRGGBB，例如 #AE90DB。");
        return false;
    }
    private void UpdateCategoryColor_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not string name || !ValidColor()) return;
        _repository.SetCategoryColor(name, CategoryColor.Text.Trim());
        CategoriesChanged = true;
        RefreshCategories();
    }
    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not string name) return;
        if (MessageBox.Show(this, $"删除分类「{name}」？历史记录将变回未分类。", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _repository.DeleteCategory(name);
        CategoriesChanged = true; RefreshCategories();
    }
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(Diagnostics.DirectoryPath);
        Process.Start(new ProcessStartInfo { FileName = Diagnostics.DirectoryPath, UseShellExecute = true });
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Retention.Text.Trim(), out int days) || !(days == 0 || days == 1 || days == 7 || days == 30 || days == 365))
        { MessageBox.Show(this, "历史保留天数必须为 0、1、7、30 或 365。"); return; }
        if (ShortcutService.HasConflict(SearchShortcut.Text, NewNoteShortcut.Text, PauseShortcut.Text))
        {
            MessageBox.Show(this, "应用内快捷键格式无效或存在冲突。请使用如 Ctrl+F 的组合，并保证各项不同。");
            return;
        }
        if (HotkeyModifiers.SelectedItem is not ComboBoxItem mods ||
            HotkeyKey.SelectedItem is not ComboBoxItem key)
        {
            MessageBox.Show(this, "请选择完整的全局快捷键。");
            return;
        }
        _settings.GlobalHotkeyModifiers = (uint)mods.Tag;
        _settings.GlobalHotkeyKey = (uint)key.Tag;
        _settings.ShortcutSearch = SearchShortcut.Text.Trim();
        _settings.ShortcutNewNote = NewNoteShortcut.Text.Trim();
        _settings.ShortcutPause = PauseShortcut.Text.Trim();
        _settings.RetentionDays = days;
        _settings.SingleClickPaste = ClickPaste.IsChecked == true;
        _settings.HideOnDeactivate = AutoHide.IsChecked == true;
        _settings.AutoStart = AutoLaunch.IsChecked == true;
        _settings.ExcludedApps = ExcludedApps.Text.Trim();
        _settings.AiProtocol = ((AiProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OpenAI");
        _settings.AiEndpoint = AiEndpoint.Text.Trim();
        _settings.AiModel = AiModel.Text.Trim();
        _settings.AutoClassify = AutoClassify.IsChecked == true;
        _settings.DiagnosticsEnabled = EnableDiagnostics.IsChecked == true;
        if (!string.IsNullOrWhiteSpace(ApiKey.Password)) _settings.ApiKey = ApiKey.Password;
        try
        {
            _settings.Save();
            _settings.ApplyAutoStart();
            DialogResult = true;
        }
        catch (Exception ex) { Diagnostics.Error("settings.save", ex); MessageBox.Show(this, "保存失败，查看日志文件夹。"); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
