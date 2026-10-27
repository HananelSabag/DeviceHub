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

/// <summary>Starting with Windows: one value under HKCU\…\Run that starts DeviceHub quietly in the tray.</summary>
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "DeviceHub";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    public static void Set(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(Name, $"\"{Application.ExecutablePath}\" --tray");
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
