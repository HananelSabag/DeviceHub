using System.Text.Json;

namespace DeviceHub.Core;

/// <summary>The few things worth changing. Kept short on purpose.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "he";
    public bool Sounds { get; init; } = true;
    public bool StartFullscreen { get; init; }
    /// <summary>Switching the sound also moves calls (Discord, Teams…) to the same device.</summary>
    public bool SwitchCalls { get; init; } = true;

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
