namespace DeviceHub.Devices;

public enum DeviceState
{
    /// <summary>Answered: the values below are fresh.</summary>
    Ready,
    /// <summary>The receiver answered but the device behind it did not (mouse asleep, headset switched off).</summary>
    Asleep,
    /// <summary>Not plugged in / not found.</summary>
    Missing,
    /// <summary>Found, but this connection does not report these values (for example the controller over Bluetooth).</summary>
    Limited,
    /// <summary>Found, but the query failed. Detail says why.</summary>
    Error,
}

/// <summary>What one device reported. Only the fields it knows are filled; the page formats them in its language.</summary>
public sealed record DeviceReading
{
    /// <summary>mouse | headset | controller | laptop</summary>
    public required string Id { get; init; }
    public required string Model { get; init; }
    public required DeviceState State { get; init; }
    /// <summary>usb | bluetooth | dongle</summary>
    public string? Link { get; init; }
    public int? Battery { get; init; }
    public bool Charging { get; init; }
    public bool Full { get; init; }
    public int? PollingHz { get; init; }
    public int? Dpi { get; init; }
    public int? DpiY { get; init; }
    /// <summary>The headset's ChatMix dial: game and chat volume, 0-100 each (both 100 = centred).</summary>
    public int? ChatGame { get; init; }
    public int? ChatChat { get; init; }
    public int? Temperature { get; init; }
    public int? FanDuty { get; init; }
    /// <summary>A technical note for errors and limits, in English.</summary>
    public string? Detail { get; init; }
    public DateTime At { get; init; } = DateTime.Now;
}
