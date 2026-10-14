using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Clipori.Windows.Services;

public sealed class UserSettings
{
    public bool SingleClickPaste { get; set; } = true;
    public bool HideOnDeactivate { get; set; } = true;
    public bool CapturePaused { get; set; }
    public bool AutoStart { get; set; }
    public int RetentionDays { get; set; } = 30;
    public string ExcludedApps { get; set; } = "KeePass,KeePassXC,Bitwarden,1Password,LastPass,CredentialUIBroker";
    public string AiProtocol { get; set; } = "OpenAI";
    public string AiEndpoint { get; set; } = "https://api.openai.com/v1";
    public string AiModel { get; set; } = "";
    public string ProtectedApiKey { get; set; } = "";
    public bool AutoClassify { get; set; } = false;
    public bool DiagnosticsEnabled { get; set; } = true;

    [JsonIgnore]
    public string ApiKey
    {
        get
        {
            try { return string.IsNullOrEmpty(ProtectedApiKey) ? "" : Protection.Unprotect(ProtectedApiKey); }
            catch (Exception ex) { Diagnostics.Error("settings.api-key", ex); return ""; }
        }
        set => ProtectedApiKey = string.IsNullOrWhiteSpace(value) ? "" : Protection.Protect(value);
    }

    public bool IsExcluded(string processName) =>
        ExcludedApps.Split(',', ';', '\n').Any(p => 
            !string.IsNullOrWhiteSpace(p) &&
            string.Equals(p.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.Trim()[..^4] : p.Trim(),
                processName, StringComparison.OrdinalIgnoreCase));

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CliporiWindows", "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex) { Diagnostics.Error("settings.load", ex); }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }

    public void ApplyAutoStart()
    {
        const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(runPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runPath);
        if (AutoStart)
            key?.SetValue("Clipori.Windows", "\"" + Environment.ProcessPath + "\"");
        else key?.DeleteValue("Clipori.Windows", throwOnMissingValue: false);
    }
}
