using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Clipori.Windows.Services;

namespace Clipori.Windows.Models;

public sealed class ClipEntry : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "text";
    public string Text { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "未知应用";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Hash { get; set; } = "";
    public string? ImagePath { get; set; }
    public string CategoryName { get; set; } = "";
    public string CategoryLabel => string.IsNullOrWhiteSpace(CategoryName) ? "未分类" : CategoryName;
    private int _displayIndex;
    public int DisplayIndex
    {
        get => _displayIndex;
        set { _displayIndex = value; Changed(nameof(IndexLabel)); }
    }
    public string IndexLabel => DisplayIndex is >= 1 and <= 999 ? DisplayIndex.ToString("00") : "";
    public string Notes { get; set; } = "";

    private bool _isPinned;
    private bool _isSelected;
    public bool IsPinned { get => _isPinned; set { if (_isPinned == value) return; _isPinned = value; Changed(); Changed(nameof(PinMark)); } }
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Changed(); } }
    public string PinMark => IsPinned ? "📌" : "";
    public string Preview => Kind switch {
        "image" => "点击预览图片",
        "files" => string.Join("\n", FileNames.Take(4)),
        _ => (Text + (string.IsNullOrEmpty(Notes) ? "" : "\n" + Notes)).Replace('\r', ' ').Replace('\n', ' ')
    };
    private string? _searchSnippet;
    public string SearchSnippet { get => _searchSnippet ?? Preview; set { _searchSnippet = value; Changed(); } }
    public BitmapImage? DetailImage => LoadImage(false);
    private BitmapImage? LoadImage(bool thumbnail)
    {
        if (Kind != "image" || string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath)) return null;
        try
        {
            byte[] bytes = File.ReadAllBytes(ImagePath);
            if (ImagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                bytes = Protection.UnprotectBytes(bytes);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = thumbnail ? 420 : 0;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) { Diagnostics.Error("image.load", ex); return null; }
    }
    public string TypeLabel => Kind switch { "image" => "图片", "files" => "文件", "link" => "链接", "code" => "代码", _ => "文字" };
    public string Glyph => Kind switch { "image" => "▧", "files" => "▣", "link" => "↗", "code" => "</>", _ => "≡" };
    public string When => CreatedUtc.ToLocalTime().ToString("MM/dd HH:mm");
    public string[] FileNames
    {
        get
        {
            if (Kind != "files") return Array.Empty<string>();
            try { return (JsonSerializer.Deserialize<string[]>(Text) ?? Array.Empty<string>()).Select(Path.GetFileName).ToArray()!; }
            catch { return Array.Empty<string>(); }
        }
    }
    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail => _thumbnail ??= LoadImage(true);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void NotifyChanged() { Changed(nameof(Preview)); Changed(nameof(Title)); Changed(nameof(CategoryName)); Changed(nameof(CategoryLabel)); Changed(nameof(SearchSnippet)); }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
