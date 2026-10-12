using System.Windows;
using Clipori.Windows.Models;

namespace Clipori.Windows.Views;

public partial class PreviewWindow : Window
{
    public PreviewWindow(ClipEntry item)
    {
        InitializeComponent();
        TitleText.Text = item.Title;
        if (item.Kind == "image")
        {
            ContentText.Visibility = Visibility.Collapsed;
            ContentImage.Source = item.Thumbnail;
            ContentImage.Visibility = Visibility.Visible;
        }
        else ContentText.Text = item.Text;
    }
}
