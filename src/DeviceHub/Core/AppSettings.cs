using System.Text.Json;
using Microsoft.Win32;

namespace DeviceHub.Core;

/// <summary>The few things worth changing. Kept short on purpose.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "he";
    /// <summary>Switching the sound also moves calls (Discord, Teams…) to the same device.</summary>
    public bool SwitchCalls { get; init; } = true;
    /// <summary>Closing the window leaves DeviceHub in the tray, so keyboard effects and the screen keep going.</summary>
    public bool KeepInTray { get; init; } = true;
    /// <summary>A note from the tray when the mouse, headset or controller drops below 15 %.</summary>
    public bool BatteryAlerts { get; init; } = true;
    /// <summary>The minimize button sends DeviceHub to the tray (next to the clock) instead of the taskbar.</summary>
    public bool MinimizeToTray { get; init; } = true;

    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeviceHub");

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static AppSettings Load(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            return File.Exists(file) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), Json) ?? new() : new();
        }
        catch
        {
            return new(); // a broken settings file should never stop the app from starting
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
    }
}

/// <summary>
/// Starting with Windows: a shortcut in the user's Startup folder that starts DeviceHub quietly in the tray.
/// (A Run-key entry was tried first; on this PC Windows ran the Startup-folder shortcut and skipped the Run entry,
/// so the shortcut is the one way, and an old Run entry is removed.)
/// </summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "DeviceHub";
    private static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DeviceHub.lnk");

    public static bool Enabled => File.Exists(Shortcut) || RunEntry() is not null;

    public static void Set(bool on)
    {
        RemoveRunEntry();
        if (on) WriteShortcut();
        else if (File.Exists(Shortcut)) File.Delete(Shortcut);
    }

    /// <summary>On every start: if starting with Windows is on in any form, make it the one right form (tray, this exe).</summary>
    public static void Tidy()
    {
        try
        {
            if (!Enabled) return;
            var exe = Application.ExecutablePath;
            var (target, args) = ReadShortcut();
            if (RunEntry() is null && string.Equals(target, exe, StringComparison.OrdinalIgnoreCase) && args == "--tray") return;
            Set(true);
            Log.Info($"start with Windows: shortcut set to \"{exe}\" --tray");
        }
        catch (Exception ex) { Log.Error("startup shortcut", ex); }
    }

    private static string? RunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) as string;
    }

    private static void RemoveRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(Name, throwOnMissingValue: false);
    }

    private static void WriteShortcut()
    {
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [Shortcut])!;
        link.TargetPath = Application.ExecutablePath;
        link.Arguments = "--tray";
        link.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        link.IconLocation = $"{Application.ExecutablePath},0";
        link.Description = "DeviceHub, in the tray";
        link.Save();
    }

    private static (string? Target, string? Args) ReadShortcut()
    {
        if (!File.Exists(Shortcut)) return (null, null);
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [Shortcut])!;
        return ((string)link.TargetPath, (string)link.Arguments);
    }
}