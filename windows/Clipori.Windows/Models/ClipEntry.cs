using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

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
    private bool _isPinned;
    public bool IsPinned { get => _isPinned; set { if (_isPinned == value) return; _isPinned = value; OnPropertyChanged(); OnPropertyChanged(nameof(PinMark)); } }
    public string PinMark => IsPinned ? "📌" : "";
    public string Preview => Kind == "image" ? "点击预览图片" : Text.Replace('\r', ' ').Replace('\n', ' ');
    public string TypeLabel => Kind switch { "image" => "图片", "link" => "链接", "code" => "代码", _ => "文字" };
    public string Glyph => Kind switch { "image" => "▧", "link" => "↗", "code" => "</>", _ => "≡" };
    public string When => CreatedUtc.ToLocalTime().ToString("MM/dd HH:mm");
    public BitmapImage? Thumbnail
    {
        get
        {
            if (Kind != "image" || string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath)) return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(ImagePath, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 420;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch { return null; }
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
