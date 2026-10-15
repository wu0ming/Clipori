using System.IO;
using System.Windows;
using System.Windows.Input;
using Clipori.Windows.Models;

namespace Clipori.Windows.Views;

public partial class EntryDetailWindow : Window
{
    private readonly ClipEntry _entry;
    private readonly Action<ClipEntry> _copy;
    private readonly Action<ClipEntry> _paste;
    private double _zoom = 1;
    public EntryDetailWindow(ClipEntry entry, Action<ClipEntry> copy, Action<ClipEntry> paste)
    {
        InitializeComponent();
        _entry = entry;
        _copy = copy;
        _paste = paste;
        Heading.Text = entry.Title;
        Meta.Text = $"{entry.TypeLabel}  ·  {entry.Source}  ·  {entry.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  ·  {entry.CategoryLabel}";
        NotesInfo.Text = string.IsNullOrWhiteSpace(entry.Notes) ? "暂无附加备注" : entry.Notes;
        if (entry.Kind == "image")
        {
            TextContent.Visibility = Visibility.Collapsed;
            ImageScroller.Visibility = Visibility.Visible;
            ZoomControls.Visibility = Visibility.Visible;
            DetailImage.Source = entry.DetailImage;
        }
        else if (entry.Kind == "files")
            TextContent.Text = string.Join(Environment.NewLine, entry.FileNames);
        else
            TextContent.Text = entry.Text;
    }

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 0.25, 6);
        ImageScale.ScaleX = _zoom;
        ImageScale.ScaleY = _zoom;
        ZoomLabel.Text = $"{_zoom * 100:0}%";
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.25);
    private void ActualSize_Click(object sender, RoutedEventArgs e) => SetZoom(1);
    private void Fit_Click(object sender, RoutedEventArgs e) => SetZoom(0.75);
    private void ImageScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        SetZoom(_zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12));
        e.Handled = true;
    }
    private void Copy_Click(object sender, RoutedEventArgs e) => _copy(_entry);
    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _paste(_entry);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
