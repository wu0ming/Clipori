using System.Text.RegularExpressions;

namespace Clipori.Windows.Services;

public static class ClipboardClassifier
{
    private static readonly Regex CodeTokens = new(
        @"(?m)^\s*(?:#!.*(?:python|bash|node)|(?:from\s+\w+\s+import\s+|import\s+\w+|def\s+\w+\(|class\s+\w+\s*[:({]|for\s+\w+\s+in\s+|while\s+.+:|if\s+.+:|elif\s+.+:|print\s*\(|return\s+|async\s+def\s+|public\s+|private\s+|using\s+System\.|const\s+|let\s+|function\s+|SELECT\s+|INSERT\s+INTO\s+|<\?php)|[}\{;]\s*$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypicalSyntax = new(
        @"(?m)(\b(?:range|append|console\.log|System\.out\.println|printf|print|len|await|fetch)\s*\(|\b\w+\s*(?:\+=|=>|==|:=)\s*|\b(?:def|func|function|class|if|else|for|while)\b.*[:{]|^\s*#\s*(?:include|!/usr/bin)|^\s*(?:</?\w+|\{\s*[""']))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Classify(string value)
    {
        string s = value.Trim();
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri) && 
            (uri.Scheme == "http" || uri.Scheme == "https") && !s.Any(char.IsWhiteSpace)) return "link";
        if (LooksLikeCode(s)) return "code";
        return "text";
    }

    public static bool LooksLikeCode(string s)
    {
        if (s.Length == 0) return false;
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int tokenLines = CodeTokens.Matches(s).Count;
        int syntax = TypicalSyntax.Matches(s).Count;
        int indented = lines.Count(line => line.Length - line.TrimStart(' ', '\t').Length >= 2);
        if (s.StartsWith("#!/") && syntax >= 1) return true;
        if (tokenLines >= 2 && syntax >= 1) return true;
        if (lines.Length >= 3 && syntax >= 2 && indented >= 1) return true;
        if (lines.Length >= 2 && syntax >= 3) return true;
        return false;
    }
}
