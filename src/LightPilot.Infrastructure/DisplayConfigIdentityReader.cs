using System.Management;
using System.Runtime.InteropServices;

namespace LightPilot.Infrastructure;

internal sealed record DisplayConfigIdentity(string? DevicePath, string? EdidIdentity, string? WmiIdentity, string? FriendlyName);

internal static class DisplayConfigIdentityReader
{
    public static DisplayConfigIdentity? TryResolve(string deviceName)
    {
        try
        {
            var target = NativeMethods.TryGetDisplayConfigTarget(deviceName);
            var wmi = ReadWmiIdentities();
            var match = wmi.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(target?.DevicePath) &&
                target.DevicePath.Contains(item.ProductToken, StringComparison.OrdinalIgnoreCase));
            return new DisplayConfigIdentity(
                target?.DevicePath,
                match?.EdidIdentity ?? target?.EdidIdentity,
                match?.EdidIdentity,
                First(target?.FriendlyName, match?.FriendlyName, deviceName));
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }

    private static IReadOnlyList<WmiIdentity> ReadWmiIdentities()
    {
        var values = new List<WmiIdentity>();
        using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT ManufacturerName, ProductCodeID, SerialNumberID, UserFriendlyName FROM WmiMonitorID");
        foreach (ManagementObject monitor in searcher.Get())
        {
            var manufacturer = Decode(monitor["ManufacturerName"]);
            var product = Decode(monitor["ProductCodeID"]);
            var serial = Decode(monitor["SerialNumberID"]);
            var friendly = Decode(monitor["UserFriendlyName"]);
            if (!string.IsNullOrWhiteSpace(manufacturer) || !string.IsNullOrWhiteSpace(product))
            {
                values.Add(new WmiIdentity(product, $"{manufacturer}:{product}:{serial}", friendly));
            }
        }
        return values;
    }

    private static string Decode(object? value) => value is ushort[] codes
        ? new string(codes.Where(code => code != 0).Select(code => (char)code).ToArray()).Trim()
        : string.Empty;

    private static string? First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record WmiIdentity(string ProductToken, string EdidIdentity, string FriendlyName);
}
