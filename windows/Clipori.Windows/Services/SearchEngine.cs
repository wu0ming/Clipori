using System.Globalization;
using System.Text;
using Clipori.Windows.Models;

namespace Clipori.Windows.Services;

/// <summary>
/// Offline keyword + tolerant search. One immutable normalized index per item,
/// updated only when an item changes. No AI provider or network involved.
/// </summary>
public sealed class SearchEngine
{
    public sealed record SearchResult(ClipEntry Entry, int Score, string Excerpt);
    private sealed record Indexed(ClipEntry Entry, string Normalized, string Title, string Preview);
    private readonly Dictionary<string, Indexed> _index = new();

    public void Rebuild(IEnumerable<ClipEntry> entries)
    {
        _index.Clear();
        foreach (var entry in entries) Upsert(entry);
    }

    public void Upsert(ClipEntry entry)
    {
        // Avoid indexing unbounded clipboard blobs or private file paths.
        string candidate = entry.Title + " " + entry.Text[..Math.Min(entry.Text.Length, 24000)] +
                           " " + entry.Notes + " " + entry.Source + " " + entry.CategoryName;
        _index[entry.Id] = new Indexed(entry, Normalize(candidate), Normalize(entry.Title),
            entry.Text.Length > 0 ? entry.Text : entry.Preview);
    }

    public void Remove(string id) => _index.Remove(id);

    public List<SearchResult> Search(string query, string category, int limit = 70)
    {
        string q = Normalize(query);
        IEnumerable<Indexed> entries = _index.Values;
        if (category == "pinned") entries = entries.Where(e => e.Entry.IsPinned);
        else if (category.StartsWith("category:", StringComparison.Ordinal))
            entries = entries.Where(e => e.Entry.CategoryName == category["category:".Length..]);
        else if (category != "all") entries = entries.Where(e => e.Entry.Kind == category);

        if (q.Length == 0)
            return entries.OrderByDescending(e => e.Entry.CreatedUtc)
                .Take(limit).Select(e => new SearchResult(e.Entry, 0, "")).ToList();

        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var scored = new List<SearchResult>();
        foreach (var e in entries)
        {
            int score = Score(e.Normalized, e.Title, q, tokens);
            if (score <= 0) continue;
            string excerpt = Extract(e.Preview, q);
            scored.Add(new SearchResult(e.Entry, score, excerpt));
        }
        return scored.OrderByDescending(e => e.Score)
            .ThenByDescending(e => e.Entry.CreatedUtc).Take(limit).ToList();
    }

    public int Count => _index.Count;
    public static string Normalize(string s)
    {
        if (s.Length == 0) return "";
        var input = s.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var output = new StringBuilder(input.Length);
        bool space = false;
        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!space) output.Append(' ');
                space = true;
            }
            else if (!char.IsControl(c)) { output.Append(c); space = false; }
        }
        return output.ToString().Trim();
    }

    private static int Score(string text, string title, string query, string[] tokens)
    {
        int at = text.IndexOf(query, StringComparison.Ordinal);
        if (at >= 0)
            return (title.Contains(query, StringComparison.Ordinal) ? 1300 : 1000)
                - Math.Min(at / 80, 100);
        if (tokens.Length > 1 && tokens.All(t => text.Contains(t, StringComparison.Ordinal)))
            return 800 + tokens.Length * 20;

        // Bound tolerance to meaningful queries. Avoid one-character false positives.
        if (query.Length < 2) return 0;
        int best = 0;
        foreach (string token in tokens)
        {
            if (token.Length < 2) continue;
            if (text.Contains(token, StringComparison.Ordinal))
                best += 150;
        }
        if (tokens.Length > 1 && best >= 150 * tokens.Length) return 700;

        // Han-character skip matching makes "不清楚" find "不是很清楚".
        // Require at least 3 CJK chars to reduce misleading matches.
        if (query.Length is >= 3 and <= 16 && IsMostlyHan(query) &&
            IsSubsequenceInWindow(query, text, Math.Min(query.Length + 6, 24)))
            return 560 + query.Length * 7;

        // Long documents: evaluate only beginning and a small title segment,
        // not every possible editing-distance window.
        int sampleSize = Math.Min(text.Length, 8000);
        var corpus = text.AsSpan(0, sampleSize);
        int fuzzy = BestFuzzyScore(query, corpus);
        return Math.Max(best, fuzzy);
    }

    private static int BestFuzzyScore(string query, ReadOnlySpan<char> text)
    {
        if (query.Length is < 3 or > 36 || text.Length == 0) return 0;
        int allowed = query.Length <= 4 ? 1 : query.Length <= 10 ? 2 : 3;
        // Use a character trigram-like anchor before bounded Levenshtein.
        // Only consider candidate spans near first/last character hits; cap work.
        int comparisons = 0, best = 0;
        int lo = Math.Max(2, query.Length - allowed), hi = Math.Min(48, query.Length + allowed);
        for (int pos = 0; pos < text.Length && comparisons < 200; pos++)
        {
            if (text[pos] != query[0] && text[pos] != query[Math.Min(1, query.Length - 1)])
                continue;
            for (int len = lo; len <= hi && pos + len <= text.Length; len++)
            {
                if (++comparisons > 200) break;
                int distance = BoundedDistance(query.AsSpan(), text.Slice(pos, len), allowed);
                if (distance <= allowed)
                    best = Math.Max(best, 400 + 24 * (allowed - distance) - Math.Abs(len - query.Length));
            }
        }
        return best;
    }

    private static int BoundedDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        Span<int> prev = stackalloc int[50];
        Span<int> next = stackalloc int[50];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            next[0] = i;
            int rowMin = next[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                next[j] = Math.Min(Math.Min(next[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, next[j]);
            }
            if (rowMin > max) return max + 1;
            next.Slice(0, b.Length + 1).CopyTo(prev);
        }
        return prev[b.Length];
    }

    private static bool IsMostlyHan(string q)
    {
        int han = 0;
        foreach (char c in q) if (c is >= '\u3400' and <= '\u9fff') ++han;
        return han * 3 >= q.Length * 2;
    }

    private static bool IsSubsequenceInWindow(string q, string text, int maxWindow)
    {
        int compared = Math.Min(text.Length, 12000);
        for (int p = 0; p < compared; p++)
        {
            if (text[p] != q[0]) continue;
            int matched = 1;
            for (int x = p + 1; x < compared && x - p < maxWindow && matched < q.Length; x++)
                if (text[x] == q[matched]) matched++;
            if (matched == q.Length) return true;
        }
        return false;
    }

    private static string Extract(string value, string query)
    {
        if (value.Length == 0) return "";
        string normalized = Normalize(value);
        int at = normalized.IndexOf(query, StringComparison.Ordinal);
        if (at < 0 && query.Length >= 2)
            at = normalized.IndexOf(query[..1], StringComparison.Ordinal);
        if (at < 0) return value[..Math.Min(value.Length, 240)];
        int start = Math.Clamp(at - 32, 0, Math.Max(0, value.Length - 1));
        int size = Math.Min(240, value.Length - start);
        return (start > 0 ? "…" : "") + value.Substring(start, size) +
               (start + size < value.Length ? "…" : "");
    }
}
