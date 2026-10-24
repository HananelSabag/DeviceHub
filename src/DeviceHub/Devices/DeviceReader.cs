using DeviceHub.Interop;

namespace DeviceHub.Devices;

/// <summary>Reads every device once. The HID list is taken fresh each time, so plugging in or out is picked up.</summary>
internal static class DeviceReader
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    public static async Task<List<DeviceReading>> ReadAllAsync()
    {
        await gate.WaitAsync();
        try
        {
            var hid = await Task.Run(() => Hid.Enumerate(RazerMouse.Vendor, ArctisHeadset.Vendor, DualSense.Vendor));
            var mouse = Task.Run(() => Safe("mouse", RazerMouse.Model, () => RazerMouse.Read(hid)));
            var headset = SafeAsync("headset", ArctisHeadset.Model, () => ArctisHeadset.ReadAsync(hid));
            var controller = SafeAsync("controller", "DualSense Edge", () => DualSense.ReadAsync(hid));
            var laptop = Task.Run(ReadLaptop);
            return [await mouse, await headset, await controller, await laptop];
        }
        finally { gate.Release(); }
    }

    public static DeviceReading ReadLaptop() => Safe("laptop", "Laptop", LaptopSensors.Read);

    private static DeviceReading Safe(string id, string model, Func<DeviceReading> read)
    {
        try { return read(); }
        catch (Exception ex) { return new() { Id = id, Model = model, State = DeviceState.Error, Detail = ex.Message }; }
    }

    private static async Task<DeviceReading> SafeAsync(string id, string model, Func<Task<DeviceReading>> read)
    {
        try { return await Task.Run(read); }
        catch (Exception ex) { return new() { Id = id, Model = model, State = DeviceState.Error, Detail = ex.Message }; }
    }
}
