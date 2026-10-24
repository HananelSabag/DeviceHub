using System.Runtime.InteropServices;

namespace DeviceHub.Audio;

public sealed record AudioEndpoint
{
    public required string Id { get; init; }
    /// <summary>output | input</summary>
    public required string Flow { get; init; }
    public required string Name { get; init; }
    public string? Sub { get; init; }
    /// <summary>headset | speakers | monitor | controller | laptop | earbuds | other</summary>
    public required string Kind { get; init; }
    public bool Active { get; init; }
    /// <summary>The default device for games, music and video.</summary>
    public bool Default { get; init; }
    /// <summary>The default device for calls.</summary>
    public bool Calls { get; init; }
    /// <summary>The name Windows shows, for reference.</summary>
    public required string Windows { get; init; }
}

public sealed record AudioState(List<AudioEndpoint> Outputs, List<AudioEndpoint> Inputs);

/// <summary>Lists the sound devices and changes the default one, the same way Windows' sound settings do.</summary>
internal static class AudioDevices
{
    public static AudioState List() => new(List(DataFlow.Render), List(DataFlow.Capture));

    private static List<AudioEndpoint> List(DataFlow flow)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try
        {
            var defaultId = DefaultId(en, flow, Role.Console);
            var callsId = DefaultId(en, flow, Role.Communications);
            var raw = new List<AudioEndpoint>();
            var handsFree = new HashSet<string>();
            Marshal.ThrowExceptionForHR(en.EnumAudioEndpoints(flow, EndpointState.Active | EndpointState.Unplugged | EndpointState.NotPresent, out var devices));
            try
            {
                devices.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    if (devices.Item(i, out var d) != 0) continue;
                    try
                    {
                        d.GetId(out var id);
                        d.GetState(out var state);
                        if (d.OpenPropertyStore(0, out var props) != 0) continue; // STGM_READ
                        try
                        {
                            var look = AudioNames.Describe(PropKeys.Read(props, PropKeys.Description), PropKeys.Read(props, PropKeys.AdapterName), flow == DataFlow.Render);
                            raw.Add(new AudioEndpoint
                            {
                                Id = id, Flow = flow == DataFlow.Render ? "output" : "input",
                                Name = look.Name, Sub = look.Sub, Kind = look.Kind,
                                Active = state == EndpointState.Active, Default = id == defaultId, Calls = id == callsId,
                                Windows = PropKeys.Read(props, PropKeys.FriendlyName),
                            });
                            if (look.HandsFree) handsFree.Add(id);
                        }
                        finally { Marshal.ReleaseComObject(props); }
                    }
                    finally { Marshal.ReleaseComObject(d); }
                }
            }
            finally { Marshal.ReleaseComObject(devices); }
            return Tidy(raw, handsFree);
        }
        finally { Marshal.ReleaseComObject(en); }
    }

    /// <summary>
    /// What is worth a tile: every active device except Bluetooth's phone-call profile (unless it is in use),
    /// plus one tile for each headset, speaker or controller that is away right now. Windows keeps an old endpoint for every
    /// USB port a device was ever in; those collapse into the one that is active, or into a single "not connected".
    /// </summary>
    internal static List<AudioEndpoint> Tidy(IEnumerable<AudioEndpoint> all, IReadOnlySet<string> handsFree)
    {
        var list = new List<AudioEndpoint>();
        foreach (var group in all.Where(e => !handsFree.Contains(e.Id) || (e.Active && (e.Default || e.Calls))).GroupBy(e => e.Name))
        {
            var active = group.Where(e => e.Active).ToList();
            if (active.Count > 0) list.AddRange(active);
            else if (group.FirstOrDefault(e => AudioNames.ShownWhenAway.Contains(e.Kind)) is { } offline) list.Add(offline);
        }
        return list
            .OrderBy(e => Array.IndexOf(AudioNames.Order, e.Kind) is var i and >= 0 ? i : 99)
            .ThenByDescending(e => e.Active)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string? DefaultId(IMMDeviceEnumerator en, DataFlow flow, Role role)
    {
        if (en.GetDefaultAudioEndpoint(flow, role, out var d) != 0 || d is null) return null; // no device at all
        try { return d.GetId(out var id) == 0 ? id : null; }
        finally { Marshal.ReleaseComObject(d); }
    }

    /// <summary>Makes a device the default for games, music and video, and with <paramref name="calls"/> for calls too.</summary>
    public static void SetDefault(string id, bool calls)
    {
        var policy = (IPolicyConfig)new PolicyConfigCom();
        try
        {
            foreach (var role in calls ? [Role.Console, Role.Multimedia, Role.Communications] : new[] { Role.Console, Role.Multimedia })
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role));
        }
        finally { Marshal.ReleaseComObject(policy); }
    }

    /// <summary>The current default for each role, to put things back exactly (used by the switch self-test).</summary>
    public static Dictionary<Role, string?> Defaults(DataFlow flow)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try { return Enum.GetValues<Role>().ToDictionary(r => r, r => DefaultId(en, flow, r)); }
        finally { Marshal.ReleaseComObject(en); }
    }

    public static void SetDefault(string id, Role role)
    {
        var policy = (IPolicyConfig)new PolicyConfigCom();
        try { Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, role)); }
        finally { Marshal.ReleaseComObject(policy); }
    }
}

/// <summary>
/// Tells DeviceHub when a sound device comes or goes or the default changes, from anywhere (Windows' own
/// settings included), so the page never polls. Windows calls this on its own threads: keep the handler short.
/// </summary>
internal sealed class AudioWatcher : IDisposable
{
    private readonly IMMDeviceEnumerator enumerator;
    private readonly Client client;

    public AudioWatcher(Action changed)
    {
        enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        client = new Client(changed);
        Marshal.ThrowExceptionForHR(enumerator.RegisterEndpointNotificationCallback(client));
    }

    public void Dispose()
    {
        enumerator.UnregisterEndpointNotificationCallback(client);
        Marshal.ReleaseComObject(enumerator);
    }

    private sealed class Client(Action changed) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string id, EndpointState state) => changed();
        public void OnDeviceAdded(string id) => changed();
        public void OnDeviceRemoved(string id) => changed();
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string? id) => changed();
        public void OnPropertyValueChanged(string id, PropertyKey key) { } // fires often (formats, levels); names rarely change
    }
}
