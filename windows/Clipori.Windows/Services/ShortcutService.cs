using System.Windows.Input;

namespace Clipori.Windows.Services;

/// <summary>Configurable local gestures and native global hotkey mapping.</summary>
internal static class ShortcutService
{
    private static readonly KeyGestureConverter Converter = new();

    public static bool TryParse(string? value, out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            gesture = Converter.ConvertFromInvariantString(value.Trim()) as KeyGesture;
            return gesture != null && (gesture.Modifiers != ModifierKeys.None || gesture.Key is Key.Enter or Key.Escape or Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5 or Key.F6);
        }
        catch { return false; }
    }

    public static bool Matches(string shortcut, KeyEventArgs args)
    {
        if (!TryParse(shortcut, out var g) || g == null) return false;
        return g.Matches(null, args);
    }

    public static bool HasConflict(params string[] gestures)
    {
        var parsed = new List<string>();
        foreach (var raw in gestures)
        {
            if (!TryParse(raw, out var gesture) || gesture == null) return true;
            string canonical = gesture.Modifiers + "+" + gesture.Key;
            if (parsed.Contains(canonical, StringComparer.OrdinalIgnoreCase)) return true;
            parsed.Add(canonical);
        }
        return false;
    }

    public static string DescribeGlobal(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        parts.Add(virtualKey is >= 0x70 and <= 0x7B ? "F" + (virtualKey - 0x6F) :
            (virtualKey is >= 0x41 and <= 0x5A ? ((char)virtualKey).ToString() : virtualKey.ToString()));
        return string.Join("+", parts);
    }
}
