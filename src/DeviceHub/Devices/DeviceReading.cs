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
    /// <summary>mouse | headset | keyboard | controller | laptop</summary>
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
    // The laptop
    /// <summary>CPU temperature, °C.</summary>
    public int? Temperature { get; init; }
    public int? GpuTemperature { get; init; }
    /// <summary>CPU fan (fan 1) and GPU fan (fan 2): duty 0-100 % and speed.</summary>
    public int? FanDuty { get; init; }
    public int? Fan2Duty { get; init; }
    public int? FanRpm { get; init; }
    public int? Fan2Rpm { get; init; }
    /// <summary>Use 0-100 %, as Task Manager shows it; CpuSpeed is the CPU's speed as a % of its base clock.</summary>
    public int? CpuLoad { get; init; }
    public int? CpuSpeed { get; init; }
    public int? GpuLoad { get; init; }
    public double? MemoryUsed { get; init; }
    public double? MemoryTotal { get; init; }
    public int? LaptopBattery { get; init; }
    public bool? OnCharger { get; init; }
    /// <summary>Battery power: + charging, - running on battery, in watts.</summary>
    public double? Watts { get; init; }
    /// <summary>Windows' power mode: efficiency | balanced | performance</summary>
    public string? PowerMode { get; init; }
    /// <summary>A technical note for errors and limits, in English.</summary>
    public string? Detail { get; init; }
    public DateTime At { get; init; } = DateTime.Now;
}
