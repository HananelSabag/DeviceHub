using System.Runtime.InteropServices;

namespace DeviceHub.Audio;

/// <summary>
/// How loud the default output plays, from the same peak meter as Windows' volume mixer. Nothing is recorded:
/// only the level is read. Rises at once and falls back slowly, the way a VU meter's needle moves.
/// Use from one thread (the lighting engine's).
/// </summary>
internal sealed class SoundLevel
{
    private static readonly Guid MeterId = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
    private IAudioMeterInformation? meter;
    private long nextLook;
    private float level;

    /// <summary>0-1, smoothed.</summary>
    public float Read()
    {
        var now = Environment.TickCount64;
        // The default device can change (headset on, speakers off): look it up again every few seconds.
        if (meter is null || now >= nextLook)
        {
            meter = Open();
            nextLook = now + 3000;
        }
        var peak = 0f;
        if (meter is not null && meter.GetPeakValue(out var p) == 0) peak = p;
        level = peak >= level ? peak : level * 0.86f + peak * 0.14f;
        return level;
    }

    private static IAudioMeterInformation? Open()
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device) != 0 || device is null) return null;
            var id = MeterId;
            return device.Activate(ref id, 0x17 /* CLSCTX_ALL */, IntPtr.Zero, out var o) == 0 ? o as IAudioMeterInformation : null;
        }
        catch (COMException) { return null; }
    }
}
