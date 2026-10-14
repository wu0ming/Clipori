using System.Security.Cryptography;
using System.Text;

namespace Clipori.Windows.Services;

/// <summary>DPAPI per-user encryption. No password/key material stored alongside the database.</summary>
internal static class Protection
{
    private const string Prefix = "dpapi:v1:";
    public static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string text)
    {
        byte[] raw = Encoding.UTF8.GetBytes(text);
        byte[] encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(raw);
        return Prefix + Convert.ToBase64String(encrypted);
    }
    public static string Unprotect(string stored)
    {
        if (!IsProtected(stored)) return stored; // Backward-compatible legacy records.
        byte[] encrypted = Convert.FromBase64String(stored[Prefix.Length..]);
        byte[] raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(raw); }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
    public static byte[] ProtectBytes(byte[] raw) => ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
    public static byte[] UnprotectBytes(byte[] encrypted) => ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
}
