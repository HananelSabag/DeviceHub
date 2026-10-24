using System.Runtime.InteropServices;

namespace DeviceHub.Audio;

// Windows Core Audio (MMDevice API) and IPolicyConfig, the interface Windows' own sound settings use to change
// the default device. Only the methods DeviceHub calls are declared with real signatures.

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class MMDeviceEnumeratorCom { }
[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] internal class PolicyConfigCom { }

internal enum DataFlow { Render = 0, Capture = 1 }
internal enum Role { Console = 0, Multimedia = 1, Communications = 2 }

[Flags]
internal enum EndpointState { Active = 0x1, Disabled = 0x2, NotPresent = 0x4, Unplugged = 0x8 }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, EndpointState stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int Item(int index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out EndpointState state);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetAt(int index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
}

[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, EndpointState state);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    void OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
}

/// <summary>Undocumented but stable since Windows 7; SetDefaultEndpoint is its eleventh method.</summary>
[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDeviceFormat();
    [PreserveSig] int ResetDeviceFormat();
    [PreserveSig] int SetDeviceFormat();
    [PreserveSig] int GetProcessingPeriod();
    [PreserveSig] int SetProcessingPeriod();
    [PreserveSig] int GetShareMode();
    [PreserveSig] int SetShareMode();
    [PreserveSig] int GetPropertyValue();
    [PreserveSig] int SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
}

[StructLayout(LayoutKind.Sequential)] internal struct PropertyKey { public Guid Fmtid; public int Pid; }
[StructLayout(LayoutKind.Explicit, Size = 24)] internal struct PropVariant { [FieldOffset(0)] public short Vt; [FieldOffset(8)] public IntPtr Pointer; }

internal static class PropKeys
{
    public static readonly PropertyKey FriendlyName = new() { Fmtid = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 14 };
    public static readonly PropertyKey Description = new() { Fmtid = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 2 };
    public static readonly PropertyKey AdapterName = new() { Fmtid = new("026e516e-b814-414b-83cd-856d6fef4822"), Pid = 2 };

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant pv);

    public static string Read(IPropertyStore props, PropertyKey key)
    {
        if (props.GetValue(ref key, out var v) != 0) return "";
        try { return v.Vt == 31 ? Marshal.PtrToStringUni(v.Pointer) ?? "" : ""; } // VT_LPWSTR
        finally { PropVariantClear(ref v); }
    }
}
