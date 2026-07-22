using System.Runtime.InteropServices;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed class DdcCiApi : IDdcCiApi
{
    private const uint McCapsBrightness = 0x00000002;

    public async ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        return await Task.Run(() => TrySetBrightness(monitor, brightnessPercent), cancellationToken).ConfigureAwait(false);
    }

    private static bool TrySetBrightness(MonitorModel monitor, int brightnessPercent)
    {
        if (!TryGetHMonitor(monitor, out var hMonitor))
        {
            return false;
        }

        if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
        {
            return false;
        }

        var physical = new NativeMethods.PhysicalMonitor[count];
        if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
        {
            return false;
        }

        try
        {
            var wrote = false;
            foreach (var item in physical)
            {
                if (NativeMethods.GetMonitorCapabilities(item.hPhysicalMonitor, out var capabilities, out _) && (capabilities & McCapsBrightness) == McCapsBrightness)
                {
                    wrote |= NativeMethods.SetMonitorBrightness(item.hPhysicalMonitor, (uint)Math.Clamp(brightnessPercent, 0, 100));
                }
            }

            return wrote;
        }
        finally
        {
            NativeMethods.DestroyPhysicalMonitors(count, physical);
        }
    }

    private static bool TryGetHMonitor(MonitorModel monitor, out nint hMonitor)
    {
        hMonitor = monitor.NativeHandle == 0 ? nint.Zero : new nint(monitor.NativeHandle);
        if (hMonitor != nint.Zero)
        {
            return true;
        }

        const string prefix = "hmonitor:";
        if (!monitor.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(monitor.Id[prefix.Length..], System.Globalization.NumberStyles.HexNumber, null, out var value)
            && (hMonitor = new nint(value)) != nint.Zero;
    }
}
