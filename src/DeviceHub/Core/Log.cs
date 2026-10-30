namespace DeviceHub.Core;

/// <summary>
/// A short text log in %LOCALAPPDATA%\DeviceHub\log.txt: when DeviceHub started and how it ended, and every error,
/// so a DeviceHub that stopped can say why. Kept under 512 KB (the older half is dropped). The same error is
/// written at most once a minute.
/// </summary>
internal static class Log
{
    private static readonly Lock gate = new();
    private static readonly Dictionary<string, DateTime> lastSeen = [];
    public static string FilePath => Path.Combine(AppSettings.DataDir, "log.txt");

    public static string? LastError { get; private set; }
    public static int Errors { get; private set; }

    public static void Info(string message) => Write("    ", message);

    public static void Error(string where, Exception ex) => Error(where, $"{ex.GetType().Name}: {ex.Message}");

    public static void Error(string where, string message)
    {
        var line = $"{where}: {message}";
        lock (gate)
        {
            Errors++;
            LastError = $"{DateTime.Now:dd.MM HH:mm} {line}";
            if (lastSeen.TryGetValue(line, out var at) && DateTime.Now - at < TimeSpan.FromMinutes(1)) return;
            lastSeen[line] = DateTime.Now;
        }
        Write("ERR ", line);
    }

    private static void Write(string level, string message)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.DataDir);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 512 * 1024)
                {
                    var text = File.ReadAllText(file.FullName);
                    File.WriteAllText(file.FullName, text[(text.Length / 2)..]);
                }
                File.AppendAllText(file.FullName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level}{message}{Environment.NewLine}");
            }
            catch (IOException) { } // a log must never take the app down
            catch (UnauthorizedAccessException) { }
        }
    }
}
