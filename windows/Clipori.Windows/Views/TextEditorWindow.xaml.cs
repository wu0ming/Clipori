using System.Windows;
using Clipori.Windows.Models;
using MessageBox = System.Windows.MessageBox;

namespace Clipori.Windows.Views;

public partial class TextEditorWindow : Window
{
    public string NoteTitle => string.IsNullOrWhiteSpace(TitleBox.Text) ? "我的便笺" : TitleBox.Text.Trim();
    public string NoteBody => BodyBox.Text.Trim();
    public string NoteNotes => NotesBox.Text.Trim();
    public TextEditorWindow(ClipEntry? existing = null)
    {
        InitializeComponent();
        if (existing != null)
        {
            Title = "编辑记录 / 备注";
            TitleBox.Text = existing.Title;
            BodyBox.Text = existing.Text;
            NotesBox.Text = existing.Notes;
            BodyBox.IsReadOnly = existing.Kind is "image" or "files";
        }
        Loaded += (_, _) => BodyBox.Focus();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoteBody) && string.IsNullOrWhiteSpace(NoteNotes)) { MessageBox.Show(this, "先写点内容吧。"); return; }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
