using System.IO;
using System.Text;

namespace Clipori.Windows.Services;

/// <summary>Local rotating diagnostics. Never log clipboard bodies, titles, file paths or API keys.</summary>
internal static class Diagnostics
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CliporiWindows", "logs");
    private const long MaxBytes = 4 * 1024 * 1024;

    public static string DirectoryPath => Folder;
    public static void Info(string name, string details = "") => Write("INFO", name, details);
    public static void Warn(string name, string details = "") => Write("WARN", name, details);
    public static void Error(string name, Exception exception) =>
        Write("ERROR", name, exception.GetType().FullName + ": " + Sanitize(exception.Message) +
          Environment.NewLine + Sanitize(exception.StackTrace ?? ""));

    private static string Sanitize(string input)
    {
        // Avoid leak of paths, URL query strings, usernames, secrets, or copied content in exception messages.
        var result = System.Text.RegularExpressions.Regex.Replace(input, @"(?i)([A-Z]:\\|/users/|/home/)[^\s,;\)\]]+", "<path>");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?i)(https?://)[^\s]+", "<url>");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?i)(api[_-]?key|bearer|token|password)\s*[:=]\s*[^\s]+", "<secret>");
        return result.Length > 4000 ? result[..4000] : result;
    }

    private static void Write(string level, string name, string details)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var file = Path.Combine(Folder, "clipori.log");
                if (File.Exists(file) && new FileInfo(file).Length >= MaxBytes)
                {
                    for (int i = 3; i >= 1; --i)
                    {
                        string from = Path.Combine(Folder, $"clipori.log.{i}");
                        string to = Path.Combine(Folder, $"clipori.log.{i + 1}");
                        if (File.Exists(from)) File.Move(from, to, true);
                    }
                    File.Move(file, Path.Combine(Folder, "clipori.log.1"), true);
                }
                var message = $"{DateTimeOffset.Now:O} [{level}] {name} {Sanitize(details)}{Environment.NewLine}";
                File.AppendAllText(file, message, Encoding.UTF8);
#if DEBUG
                System.Diagnostics.Debug.Write(message);
#endif
            }
        }
        catch { /* Diagnostics must not crash the clipboard manager. */ }
    }
}
