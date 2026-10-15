using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Clipori.Windows.Services;

public sealed class UserSettings
{
    public int InteractionSettingsVersion { get; set; } = 2;
    public bool SingleClickPaste { get; set; } = false;
    // Global show/hide shortcut (Win32 RegisterHotKey, default Ctrl+Alt+V).
    public uint GlobalHotkeyModifiers { get; set; } = 0x0003;
    public uint GlobalHotkeyKey { get; set; } = 0x56;
    // WPF local commands, editable in Settings.
    public string ShortcutSearch { get; set; } = "Ctrl+F";
    public string ShortcutNewNote { get; set; } = "Ctrl+N";
    public string ShortcutPause { get; set; } = "Ctrl+T";
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
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json) ?? new();
                using var doc = JsonDocument.Parse(json);
                bool legacy = !doc.RootElement.TryGetProperty(nameof(InteractionSettingsVersion), out var version)
                    || version.GetInt32() < 2;
                if (legacy)
                {
                    // v0.2 wrote SingleClickPaste=true by default; migrate existing users
                    // to the new v0.3 default while preserving all other preferences.
                    loaded.SingleClickPaste = false;
                    loaded.InteractionSettingsVersion = 2;
                    loaded.Save();
                }
                return loaded;
            }
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
