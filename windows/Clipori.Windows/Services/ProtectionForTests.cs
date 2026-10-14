namespace Clipori.Windows.Services;

/// <summary>Minimal public bridge for deterministic security regression checks.</summary>
public static class ProtectionForTests
{
    public static string ProtectString(string text) => Protection.Protect(text);
    public static string UnprotectString(string value) => Protection.Unprotect(value);
}
