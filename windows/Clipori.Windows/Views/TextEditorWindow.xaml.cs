using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Clipori.Windows.Views;

public partial class TextEditorWindow : Window
{
    public string NoteTitle => string.IsNullOrWhiteSpace(TitleBox.Text) ? "我的便笺" : TitleBox.Text.Trim();
    public string NoteBody => BodyBox.Text.Trim();
    public TextEditorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BodyBox.Focus();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoteBody)) { MessageBox.Show(this, "先写点内容吧。"); return; }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
