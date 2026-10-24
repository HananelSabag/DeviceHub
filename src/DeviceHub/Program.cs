using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;

namespace DeviceHub;

internal static class Program
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Hebrew readable in dumps
        WriteIndented = true,
    };

    /// <summary>
    /// DeviceHub.exe                              → the app (in a window unless full screen is on in Settings)
    /// DeviceHub.exe --windowed                   → the app, always in a window
    /// DeviceHub.exe --dump-json out.json [en]    → read every device and the sound devices once, save them (read-only)
    /// DeviceHub.exe --audio-switch-test out.json → switch the default output to another device and straight back
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--dump-json", var dumpPath, ..])
        {
            I18n.English = args.Length > 2 && args[2] == "en";
            var devices = DeviceReader.ReadAllAsync().GetAwaiter().GetResult();
            File.WriteAllText(dumpPath, JsonSerializer.Serialize(new { devices, audio = AudioDevices.List() }, Json));
            return 0;
        }

        if (args is ["--audio-switch-test", var testPath, ..])
        {
            var result = AudioSwitchTest();
            File.WriteAllText(testPath, JsonSerializer.Serialize(result, Json));
            return result.Ok ? 0 : 1;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(windowed: args.Contains("--windowed")));
        return 0;
    }

    private sealed record SwitchTest(bool Ok, string? Message, Dictionary<Role, string?> Before, string? Target, Dictionary<Role, string?>? During, Dictionary<Role, string?>? After);

    /// <summary>Proves switching works on this PC: one other active output for a moment, then every role exactly as it was.</summary>
    private static SwitchTest AudioSwitchTest()
    {
        I18n.English = true;
        var before = AudioDevices.Defaults(DataFlow.Render);
        var target = AudioDevices.List().Outputs.FirstOrDefault(o => o.Active && o.Id != before[Role.Console]);
        if (target is null) return new(false, "only one active output, nothing to switch to", before, null, null, null);
        AudioDevices.SetDefault(target.Id, calls: false);
        var during = AudioDevices.Defaults(DataFlow.Render);
        foreach (var (role, id) in before)
            if (id is not null) AudioDevices.SetDefault(id, role);
        var after = AudioDevices.Defaults(DataFlow.Render);
        var ok = during[Role.Console] == target.Id && during[Role.Multimedia] == target.Id && before.All(kv => after[kv.Key] == kv.Value);
        return new(ok, ok ? $"switched to {target.Name} and back" : "the default did not follow", before, target.Id, during, after);
    }
}
