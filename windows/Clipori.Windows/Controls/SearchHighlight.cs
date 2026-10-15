using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Clipori.Windows.Services;

namespace Clipori.Windows.Controls;

/// <summary>Decorates visible card text only. Search indexing never touches WPF visuals.</summary>
public static class SearchHighlight
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(SearchHighlight),
            new PropertyMetadata("", Update));
    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.RegisterAttached("Query", typeof(string), typeof(SearchHighlight),
            new PropertyMetadata("", Update));

    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetText(DependencyObject o, string text) => o.SetValue(TextProperty, text);
    public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);
    public static void SetQuery(DependencyObject o, string query) => o.SetValue(QueryProperty, query);

    private static void Update(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (d is not TextBlock block) return;
        string text = GetText(block) ?? "";
        string query = (GetQuery(block) ?? "").Trim();
        block.Inlines.Clear();
        if (query.Length == 0 || text.Length == 0)
        {
            block.Inlines.Add(new Run(text));
            return;
        }
        // Exact highlight has precedence. On fuzzy matches, highlight a short subsequence.
        var hits = new bool[text.Length];
        int index = 0, occurrence = 0;
        while ((index = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) >= 0 && occurrence++ < 32)
        {
            for (int j = index; j < Math.Min(index + query.Length, hits.Length); j++) hits[j] = true;
            index += Math.Max(1, query.Length);
        }
        if (!hits.Any(v => v) && query.Length >= 2)
        {
            var hay = text.ToLowerInvariant();
            var needle = query.ToLowerInvariant();
            // Highlight only when a mostly contiguous sequence appears near the start.
            for (int pos = 0; pos < hay.Length; pos++)
            {
                if (hay[pos] != needle[0]) continue;
                var indexes = new List<int> { pos };
                int matched = 1;
                for (int next = pos + 1; next < hay.Length && next - pos <= needle.Length + 7; next++)
                    if (matched < needle.Length && hay[next] == needle[matched])
                    { indexes.Add(next); matched++; }
                if (matched != needle.Length) continue;
                foreach (int hit in indexes) hits[hit] = true;
                break;
            }
        }
        int start = 0;
        while (start < text.Length)
        {
            bool marked = hits[start];
            int end = start + 1;
            while (end < text.Length && hits[end] == marked) end++;
            var run = new Run(text[start..end]);
            if (marked)
            {
                run.FontWeight = FontWeights.Bold;
                run.Background = new SolidColorBrush(Color.FromRgb(238, 218, 255));
                run.Foreground = new SolidColorBrush(Color.FromRgb(101, 58, 164));
            }
            block.Inlines.Add(run);
            start = end;
        }
    }
}
